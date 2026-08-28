import { Component, computed, effect, inject, input, model, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { finalize } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { ToggleSwitchModule } from 'primeng/toggleswitch';

import {
  AdminClient,
  FsmsUsersClient,
  UserListItemDto,
} from '../../../../core/api/api-client.generated';
import { ROLES } from '../../../../core/auth/permissions';
import { OrgScopeSelectorComponent } from '../../../../shared/components/org-scope/org-scope-selector.component';
import { OrgScopeAssignment } from '../../../../shared/components/org-scope/org-scope.model';

interface SelectOption {
  readonly label: string;
  readonly value: string | number;
}

/**
 * Creates and edits a **back-office** login. The username is fixed after creation — it is what the
 * audit trail and every survey's `createdBy` already point at, so renaming it would silently
 * detach an account from its own history.
 *
 * Crew accounts are deliberately absent: they are raised on the teams screen together with their
 * team, so the `FieldTeam` role is filtered out of the picker here and the API rejects it too.
 * Filtering only on the client would be a suggestion, not a rule.
 */
@Component({
  selector: 'app-user-form-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslocoDirective,
    ButtonModule,
    DialogModule,
    InputTextModule,
    MultiSelectModule,
    SelectModule,
    ToggleSwitchModule,
    OrgScopeSelectorComponent,
  ],
  templateUrl: './user-form-dialog.component.html',
})
export class UserFormDialogComponent {
  readonly visible = model.required<boolean>();

  /** Null opens the dialog for a new account. */
  readonly user = input<UserListItemDto | null>(null);

  readonly saved = output<void>();

  private readonly usersClient = inject(FsmsUsersClient);
  private readonly adminClient = inject(AdminClient);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly fb = inject(FormBuilder);

  protected readonly saving = signal(false);
  protected readonly loading = signal(false);
  protected readonly roles = signal<SelectOption[]>([]);
  protected readonly scopes = signal<OrgScopeAssignment[]>([]);
  protected readonly initialScopes = signal<readonly OrgScopeAssignment[]>([]);

  protected readonly isEdit = computed(() => !!this.user()?.id);

  protected readonly form = this.fb.group({
    userName: this.fb.control<string>('', [Validators.required, Validators.maxLength(256)]),
    email: this.fb.control<string>('', [Validators.email, Validators.maxLength(256)]),
    phoneNumber: this.fb.control<string>('', Validators.maxLength(30)),
    password: this.fb.control<string>(''),
    roles: this.fb.control<string[]>([]),
  });

  constructor() {
    // A password is only ever set at creation; editing one goes through the reset dialog, which is
    // the path that does not need the operator to retype an existing secret.
    effect(() => {
      const passwordControl = this.form.controls.password;

      if (this.isEdit()) {
        passwordControl.clearValidators();
        this.form.controls.userName.disable();
      } else {
        passwordControl.setValidators([Validators.required, Validators.minLength(6)]);
        this.form.controls.userName.enable();
      }

      passwordControl.updateValueAndValidity({ emitEvent: false });
    });
  }

  protected onShow(): void {
    this.loadOptions();

    const user = this.user();

    if (!user?.id) {
      this.form.reset({ roles: [] });
      this.scopes.set([]);
      this.initialScopes.set([]);
      return;
    }

    this.form.reset({
      userName: user.userName ?? '',
      email: user.email ?? '',
      phoneNumber: user.phoneNumber ?? '',
      password: '',
      roles: [...(user.roles ?? [])],
    });

    // The list row carries no scopes — only the detail read does, so the dialog fetches them.
    this.loading.set(true);
    this.usersClient
      .fsmsUsers_GetUserById(user.id!)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: res => {
          const loaded = res.data?.scopes ?? [];
          this.scopes.set([...loaded]);
          this.initialScopes.set([...loaded]);
        },
        error: () => {
          this.scopes.set([]);
          this.initialScopes.set([]);
        },
      });
  }

  protected onScopesChange(scopes: OrgScopeAssignment[]): void {
    this.scopes.set(scopes);
  }

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const scopes = this.scopes();
    this.saving.set(true);

    const request = this.isEdit()
      ? this.usersClient.fsmsUsers_UpdateUser(this.user()!.id!, {
          userId: this.user()!.id!,
          email: value.email?.trim() || undefined,
          phoneNumber: value.phoneNumber?.trim() || undefined,
          roles: value.roles ?? [],
          scopes,
        })
      : this.usersClient.fsmsUsers_CreateUser({
          userName: value.userName!.trim(),
          email: value.email?.trim() || undefined,
          phoneNumber: value.phoneNumber?.trim() || undefined,
          password: value.password!,
          roles: value.roles ?? [],
          scopes,
        });

    const successKey = this.isEdit() ? 'users.updatedSuccess' : 'users.createdSuccess';
    const errorKey = this.isEdit() ? 'users.updateError' : 'users.createError';

    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('common.success'),
          detail: this.transloco.translate(successKey),
        });
        this.visible.set(false);
        this.saved.emit();
      },
      error: (error: unknown) => {
        this.messageService.add({
          severity: 'error',
          summary: this.transloco.translate('common.error'),
          detail: this.apiMessage(error) ?? this.transloco.translate(errorKey),
        });
      },
    });
  }

  protected cancel(): void {
    this.visible.set(false);
  }

  private loadOptions(): void {
    this.adminClient.admin_GetRoles().subscribe({
      next: res =>
        this.roles.set(
          (res.data ?? [])
            // A crew login is raised on the teams screen with its team; offering the role here
            // would invite an account the API then refuses.
            .filter(role => !!role.name && role.name !== ROLES.fieldTeam)
            .map(role => ({
              label: this.localized(role.nameEn, role.nameAr) || role.name!,
              value: role.name!,
            })),
        ),
      error: () => this.roles.set([]),
    });
  }

  /** Identity's own rejection (a weak password, a duplicate name) is more useful than ours. */
  private apiMessage(error: unknown): string | null {
    const errors = (error as { errors?: { message?: string }[] } | null)?.errors;

    return errors?.[0]?.message ?? null;
  }

  private localized(nameEn?: string, nameAr?: string): string {
    return this.transloco.getActiveLang() === 'ar'
      ? (nameAr || nameEn || '')
      : (nameEn || nameAr || '');
  }
}
