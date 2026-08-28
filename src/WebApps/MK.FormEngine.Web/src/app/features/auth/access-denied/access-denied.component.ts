import { Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoModule } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';

import { SsoService } from '../../../core/auth/sso.service';

/** Reason codes the API sends back; anything else falls through to the generic message. */
const KNOWN_REASONS = [
  'notProvisioned',
  'inactive',
  'noScope',
  'crewAccount',
  'exchangeFailed',
  'noNameId',
] as const;

type DenialReason = (typeof KNOWN_REASONS)[number] | 'unknown';

/**
 * Shown when corporate sign-in succeeded but FSMS will not admit the account.
 *
 * The point is to say which of those two things happened, and name the account, so the user can ask
 * an administrator for the right fix instead of retrying a sign-in that will keep working.
 */
@Component({
  selector: 'app-access-denied',
  imports: [TranslocoModule, ButtonModule],
  templateUrl: './access-denied.component.html',
  styleUrl: './access-denied.component.scss',
})
export class AccessDeniedComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sso = inject(SsoService);

  protected readonly retrying = signal(false);

  protected readonly username = this.route.snapshot.queryParamMap.get('username');

  protected readonly reason = computed<DenialReason>(() => {
    const raw = this.route.snapshot.queryParamMap.get('reason');
    return KNOWN_REASONS.includes(raw as (typeof KNOWN_REASONS)[number])
      ? (raw as DenialReason)
      : 'unknown';
  });

  /** Translation key for the explanation paragraph, one per reason. */
  protected readonly messageKey = computed(() => `auth.sso.denied.${this.reason()}`);

  protected retry(): void {
    if (this.retrying()) {
      return;
    }
    this.retrying.set(true);

    if (this.sso.status().enabled) {
      this.sso.startLogin(null);
      return;
    }

    this.retrying.set(false);
    void this.router.navigate(['/login']);
  }
}
