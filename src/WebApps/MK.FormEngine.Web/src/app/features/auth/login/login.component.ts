import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { NgClass } from '@angular/common';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';

import { InputText } from 'primeng/inputtext';
import { Password } from 'primeng/password';
import { Checkbox } from 'primeng/checkbox';
import { ButtonModule } from 'primeng/button';
import { ProgressSpinner } from 'primeng/progressspinner';
import { MessageService } from 'primeng/api';

import { AuthService } from '../../../core/auth/auth.service';
import { SsoService } from '../../../core/auth/sso.service';
import { LanguageService } from '../../../core/i18n/language.service';

@Component({
  selector: 'app-login',
  imports: [
    ReactiveFormsModule,
    NgClass,
    TranslocoModule,
    InputText,
    Password,
    Checkbox,
    ButtonModule,
    ProgressSpinner,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly sso = inject(SsoService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly messages = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  protected readonly language = inject(LanguageService);

  protected readonly submitting = signal(false);
  protected readonly redirecting = signal(false);

  /** Where the guard wanted to send them before it found no session. */
  private readonly returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');

  /**
   * `?local=1` asks for the credentials form even when SSO is on — the way an administrator gets in
   * when the identity provider is unreachable. Without it there would be no route past an automatic
   * hand-off.
   */
  private readonly localRequested = this.route.snapshot.queryParamMap.get('local') === '1';

  protected readonly ssoEnabled = computed(() => this.sso.enabled());

  /**
   * With SSO on, the credentials form is only still useful to an Administrator signing in as a
   * fallback, so it starts collapsed behind a link instead of being the first thing on the page.
   */
  protected readonly credentialsFormVisible = signal(this.localRequested);

  protected readonly showCredentialsForm = computed(
    () => !this.ssoEnabled() || this.credentialsFormVisible(),
  );

  protected readonly localLoginOffered = computed(
    () => !this.ssoEnabled() || this.sso.status().administratorLocalLoginAllowed,
  );

  /**
   * True while the page is handing over to the identity provider without being asked. Nothing but a
   * spinner renders in that state — showing a form for a moment and then yanking it away reads as a
   * glitch.
   */
  protected readonly handingOver = signal(false);

  ngOnInit(): void {
    if (this.localRequested || !this.sso.canAutoRedirect()) {
      return;
    }

    this.handingOver.set(true);
    this.redirecting.set(true);
    this.sso.startLogin(this.returnUrl);
  }

  protected readonly form = this.fb.nonNullable.group({
    userName: ['', [Validators.required]],
    password: ['', [Validators.required]],
    rememberMe: [true],
  });

  protected get f() {
    return this.form.controls;
  }

  protected toggleLanguage(): void {
    this.language.toggle();
  }

  protected revealCredentialsForm(): void {
    this.credentialsFormVisible.set(true);
  }

  /** Hands the browser to the identity provider; the page is replaced, so the flag never clears. */
  protected signInWithSso(): void {
    if (this.redirecting()) {
      return;
    }
    this.redirecting.set(true);
    this.sso.startLogin(this.returnUrl);
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { userName, password, rememberMe } = this.form.getRawValue();
    this.submitting.set(true);

    this.auth.login({ userName, password }, rememberMe).subscribe({
      next: () => {
        this.submitting.set(false);
        this.messages.add({
          severity: 'success',
          summary: this.transloco.translate('auth.loginSuccess'),
        });
        void this.router.navigateByUrl(this.returnUrl ?? '/dashboard');
      },
      error: (error: Error) => {
        this.submitting.set(false);
        this.messages.add({
          severity: 'error',
          summary: this.transloco.translate('auth.loginFailed'),
          detail: error.message,
          life: 5000,
        });
      },
    });
  }
}
