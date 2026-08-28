import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, map, of, throwError } from 'rxjs';
import { catchError, delay } from 'rxjs/operators';
import { AppConfigService } from '../config/app-config.service';
import { ApiResult, firstErrorMessage } from '../api/api-result.model';
import { AuthStore } from './auth.store';
import { SsoService } from './sso.service';
import { AuthToken, LoginRequest, RefreshRequest } from './auth.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(AppConfigService);
  private readonly store = inject(AuthStore);
  private readonly sso = inject(SsoService);
  private readonly router = inject(Router);

  private get baseUrl(): string {
    return this.config.snapshot.apiBaseUrl;
  }

  login(request: LoginRequest, rememberMe: boolean): Observable<AuthToken> {
    if (this.config.snapshot.fakeLogin) {
      return this.fakeLogin(request, rememberMe);
    }

    return this.http
      .post<ApiResult<AuthToken>>(`${this.baseUrl}/api/v1/auth/login`, request)
      .pipe(
        map((result) => this.unwrap(result)),
        map((token) => {
          // Prefer the name the server resolved over whatever casing was typed into the form.
          this.store.setSession(token, token.userName || request.userName, rememberMe);
          return token;
        }),
        catchError((error) => this.toError(error)),
      );
  }

  refresh(): Observable<AuthToken> {
    const refreshToken = this.store.currentRefreshToken;
    if (!refreshToken) {
      return throwError(() => new Error('No refresh token available.'));
    }
    const request: RefreshRequest = { refreshToken };
    return this.http
      .post<ApiResult<AuthToken>>(`${this.baseUrl}/api/v1/auth/refresh`, request)
      .pipe(
        map((result) => this.unwrap(result)),
        map((token) => {
          this.store.updateTokens(token);
          return token;
        }),
        catchError((error) => this.toError(error)),
      );
  }

  /**
   * Ends the session everywhere it exists: the refresh token is revoked server-side first, because
   * clearing local storage alone would leave a usable token behind. With SSO on, the browser then
   * carries on to the identity provider so its session ends too — otherwise the next sign-in would
   * sail straight through without asking for anything.
   */
  logout(): void {
    const refreshToken = this.store.currentRefreshToken;
    const ssoEnabled = this.sso.status().enabled;

    const finish = () => {
      this.store.clear();
      if (ssoEnabled) {
        this.sso.startLogout();
      } else {
        void this.router.navigate(['/login']);
      }
    };

    if (!refreshToken) {
      finish();
      return;
    }

    // Revocation is best-effort: a failure here must not strand the user in a signed-in shell.
    this.http
      .post<ApiResult<boolean>>(`${this.baseUrl}/api/v1/auth/logout`, { refreshToken })
      .pipe(catchError(() => of(null)))
      .subscribe({ next: finish, error: finish });
  }

  /** Bypasses the backend and issues a client-side token for local development. */
  private fakeLogin(request: LoginRequest, rememberMe: boolean): Observable<AuthToken> {
    const token: AuthToken = {
      accessToken: `fake.${btoa(request.userName)}.token`,
      refreshToken: 'fake.refresh.token',
      expiresInSeconds: 3600,
      userName: request.userName || 'demo',
      roles: ['Administrator'],
      permissions: ['*'],
    };
    return of(token).pipe(
      delay(400),
      map((issued) => {
        this.store.setSession(issued, request.userName || 'demo', rememberMe);
        return issued;
      }),
    );
  }

  private unwrap(result: ApiResult<AuthToken>): AuthToken {
    if (!result.isSuccess || !result.data) {
      throw new Error(firstErrorMessage(result) ?? 'Authentication failed.');
    }
    return result.data;
  }

  private toError(error: unknown): Observable<never> {
    if (error instanceof Error) {
      return throwError(() => error);
    }
    const apiResult = (error as { error?: ApiResult<unknown> })?.error;
    const message = firstErrorMessage(apiResult) ?? 'Unable to reach the server.';
    return throwError(() => new Error(message));
  }
}
