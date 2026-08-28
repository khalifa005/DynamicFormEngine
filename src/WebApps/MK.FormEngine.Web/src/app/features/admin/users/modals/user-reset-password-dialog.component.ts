import { Component, inject, input, model, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { finalize } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';

import { FsmsUsersClient, UserListItemDto } from '../../../../core/api/api-client.generated';

/** Sets a new password for an account without asking the operator for the old one. */
@Component({
  selector: 'app-user-reset-password-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslocoDirective,
    ButtonModule,
    DialogModule,
    InputTextModule,
  ],
  templateUrl: './user-reset-password-dialog.component.html',
})
export class UserResetPasswordDialogComponent {
  readonly visible = model.required<boolean>();
  readonly user = input<UserListItemDto | null>(null);
  readonly reset = output<void>();

  private readonly usersClient = inject(FsmsUsersClient);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly fb = inject(FormBuilder);

  protected readonly saving = signal(false);

  protected readonly form = this.fb.group(
    {
      newPassword: this.fb.control<string>('', [Validators.required, Validators.minLength(6)]),
      confirmPassword: this.fb.control<string>('', Validators.required),
    },
    { validators: UserResetPasswordDialogComponent.passwordsMatch },
  );

  protected onShow(): void {
    this.form.reset({ newPassword: '', confirmPassword: '' });
  }

  protected save(): void {
    const userId = this.user()?.id;

    if (!userId || this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);

    this.usersClient.fsmsUsers_ResetPassword(userId, { userId, newPassword: this.form.controls.newPassword.value! })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('common.success'),
            detail: this.transloco.translate('users.passwordReset'),
          });
          this.visible.set(false);
          this.reset.emit();
        },
        error: (error: unknown) => {
          const apiMessage = (error as { errors?: { message?: string }[] } | null)?.errors?.[0]
            ?.message;

          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: apiMessage ?? this.transloco.translate('users.passwordResetError'),
          });
        },
      });
  }

  protected cancel(): void {
    this.visible.set(false);
  }

  /** Group-level so the error surfaces once, under the confirmation field. */
  private static passwordsMatch(group: AbstractControl): ValidationErrors | null {
    const password = group.get('newPassword')?.value;
    const confirmation = group.get('confirmPassword')?.value;

    return password && confirmation && password !== confirmation ? { passwordMismatch: true } : null;
  }
}
