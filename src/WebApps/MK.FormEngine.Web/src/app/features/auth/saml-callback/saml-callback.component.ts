import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ProgressSpinner } from 'primeng/progressspinner';

import { AuthStore } from '../../../core/auth/auth.store';
import { SsoService } from '../../../core/auth/sso.service';

/**
 * Where the browser lands coming back from corporate sign-in.
 *
 * It carries a single-use code rather than a token: the code is traded here, over a POST, so the
 * session itself never appears in a URL that browser history, proxies and referrer headers would
 * keep. Nothing is rendered but a spinner — the page exists only to make that one call.
 */
@Component({
  selector: 'app-saml-callback',
  imports: [TranslocoModule, ProgressSpinner],
  templateUrl: './saml-callback.component.html',
  styleUrl: './saml-callback.component.scss',
})
export class SamlCallbackComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sso = inject(SsoService);
  private readonly store = inject(AuthStore);

  protected readonly failed = signal(false);

  ngOnInit(): void {
    const code = this.route.snapshot.queryParamMap.get('code');
    // The callback's own returnUrl wins; the stashed one covers a provider that drops query strings.
    const returnUrl =
      this.route.snapshot.queryParamMap.get('returnUrl') ?? this.sso.takeStoredReturnUrl();

    if (!code) {
      this.denied('exchangeFailed');
      return;
    }

    this.sso.exchange(code).subscribe({
      next: (token) => {
        // The round trip worked, so the next sign-in must not be mistaken for a redirect loop.
        this.sso.clearAutoRedirectGuard();
        this.store.setSession(token, token.userName, true);
        void this.router.navigateByUrl(returnUrl && returnUrl !== '/login' ? returnUrl : '/dashboard');
      },
      error: () => this.denied('exchangeFailed'),
    });
  }

  private denied(reason: string): void {
    this.failed.set(true);
    void this.router.navigate(['/auth/access-denied'], { queryParams: { reason } });
  }
}
