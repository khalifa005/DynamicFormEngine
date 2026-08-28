import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, firstValueFrom, map, of, catchError } from 'rxjs';

import { AppConfigService } from '../config/app-config.service';
import { ApiResult, firstErrorMessage } from '../api/api-result.model';
import { AuthToken, SsoStatus } from './auth.model';

/** Where the intended destination waits while the browser is away at the identity provider. */
const RETURN_URL_KEY = 'fsms.sso.returnUrl';

/** When the last automatic hand-off started, used to break a redirect loop. */
const AUTO_REDIRECT_AT_KEY = 'fsms.sso.autoRedirectAt';

/**
 * Coming straight back to the sign-in page within this window means the round trip failed without
 * reaching our callback. Bouncing the user out again would loop forever, so we stop and show the
 * page instead.
 */
const AUTO_REDIRECT_COOLDOWN_MS = 15_000;

const SSO_OFF: SsoStatus = { enabled: false, administratorLocalLoginAllowed: true };

/**
 * The corporate SSO half of sign-in.
 *
 * The login and logout steps are full-page navigations, not HTTP calls — the browser itself has to
 * travel to the identity provider and back. Only the status probe and the code exchange are XHR.
 */
@Injectable({ providedIn: 'root' })
export class SsoService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(AppConfigService);

  private readonly _status = signal<SsoStatus>(SSO_OFF);

  /** What the server reported. Prefer {@link enabled} for "should we use SSO". */
  readonly status = this._status.asReadonly();

  /**
   * Whether the sign-in page should use SSO.
   *
   * The client config wins when it states an answer, so a deployment that knows it is federated
   * keeps working even while the status probe is failing. Left as `null` — the default — the server
   * decides, and the two cannot drift apart.
   */
  readonly enabled = computed(
    () => this.config.snapshot.sso.enabled ?? this._status().enabled,
  );

  /** Whether to skip the sign-in page entirely and hand straight over to the identity provider. */
  readonly autoRedirect = computed(
    () => this.enabled() && this.config.snapshot.sso.autoRedirect,
  );

  private get baseUrl(): string {
    return this.config.snapshot.apiBaseUrl;
  }

  /**
   * Asked once at startup. A failure here must not stop the app from booting — falling back to
   * "SSO off" leaves the password form, which is the safe thing to show when we cannot tell.
   */
  async loadStatus(): Promise<void> {
    const status = await firstValueFrom(
      this.http.get<ApiResult<SsoStatus>>(`${this.baseUrl}/api/v1/auth/sso/status`).pipe(
        map((result) => (result.isSuccess && result.data ? result.data : SSO_OFF)),
        catchError(() => of(SSO_OFF)),
      ),
    );
    this._status.set(status);
  }

  /** Hands the browser to the identity provider. Does not return — the page is replaced. */
  startLogin(returnUrl?: string | null): void {
    if (returnUrl) {
      localStorage.setItem(RETURN_URL_KEY, returnUrl);
    } else {
      localStorage.removeItem(RETURN_URL_KEY);
    }

    sessionStorage.setItem(AUTO_REDIRECT_AT_KEY, Date.now().toString());

    const query = returnUrl ? `?returnUrl=${encodeURIComponent(returnUrl)}` : '';
    window.location.href = `${this.baseUrl}/api/v1/auth/sso/login${query}`;
  }

  /**
   * Whether the sign-in page may hand over on its own right now.
   *
   * False when a hand-off was attempted moments ago, which means the user is back here without
   * having made it through — redirecting again would trap them in a loop with no way to reach the
   * administrator form.
   */
  canAutoRedirect(): boolean {
    if (!this.autoRedirect()) {
      return false;
    }

    const last = Number(sessionStorage.getItem(AUTO_REDIRECT_AT_KEY) ?? 0);
    return !last || Date.now() - last > AUTO_REDIRECT_COOLDOWN_MS;
  }

  /** Called once a sign-in completes, so the next one is not treated as a loop. */
  clearAutoRedirectGuard(): void {
    sessionStorage.removeItem(AUTO_REDIRECT_AT_KEY);
  }

  /** Trades the single-use code from the callback for the same token a password login returns. */
  exchange(code: string): Observable<AuthToken> {
    return this.http
      .post<ApiResult<AuthToken>>(`${this.baseUrl}/api/v1/auth/sso/exchange`, { code })
      .pipe(
        map((result) => {
          if (!result.isSuccess || !result.data) {
            throw new Error(firstErrorMessage(result) ?? 'Sign-in could not be completed.');
          }
          return result.data;
        }),
      );
  }

  /** Ends the identity provider's session too, so the next sign-in really asks for credentials. */
  startLogout(): void {
    localStorage.removeItem(RETURN_URL_KEY);
    window.location.href = `${this.baseUrl}/api/v1/auth/sso/logout`;
  }

  /**
   * The destination stashed before leaving for the identity provider, used when the callback did not
   * carry one back. Reading it clears it.
   */
  takeStoredReturnUrl(): string | null {
    const returnUrl = localStorage.getItem(RETURN_URL_KEY);
    localStorage.removeItem(RETURN_URL_KEY);
    return returnUrl;
  }
}
