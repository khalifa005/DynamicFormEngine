import { Component, OnInit, computed, inject, input, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { finalize } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';
import { SelectModule } from 'primeng/select';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TooltipModule } from 'primeng/tooltip';

import { FsmsContractorDto, FsmsTeamsClient, TeamDto } from '../../../core/api/api-client.generated';
import { FsmsLookupService } from '../../../core/lookups/fsms-lookup.service';
import { OrgScopeSelectorComponent } from '../../../shared/components/org-scope/org-scope-selector.component';
import { OrgScopeAssignment } from '../../../shared/components/org-scope/org-scope.model';

/**
 * Creates and edits a field crew — its coverage, and, on creation, the login it signs in with.
 *
 * The login lives here rather than on the users screen because a crew is both things at once: a
 * team record nobody can sign in as, or an account with no team behind it, is a half-made thing
 * somebody has to notice and finish. The users screen deliberately refuses to create crew accounts.
 *
 * Departments are not a field of their own: they are part of each coverage row, so a crew doing
 * water in one branch and waste-water in another can say exactly that.
 */
@Component({
  selector: 'app-team-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslocoDirective,
    ButtonModule,
    InputTextModule,
    SelectModule,
    ToggleSwitchModule,
    TooltipModule,
    OrgScopeSelectorComponent,
  ],
  templateUrl: './team-form.component.html',
})
export class TeamFormComponent implements OnInit {
  readonly team = input<TeamDto | null>(null);

  readonly saved = output<void>();
  readonly cancelled = output<void>();
  readonly viewLastActive = output<void>();

  private readonly teamsClient = inject(FsmsTeamsClient);
  private readonly lookupService = inject(FsmsLookupService);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly fb = inject(FormBuilder);

  protected readonly submitting = signal(false);
  protected readonly contractorsLoading = signal(false);
  protected readonly contractorOptions = signal<FsmsContractorDto[]>([]);
  protected readonly scopes = signal<OrgScopeAssignment[]>([]);
  protected readonly initialScopes = signal<readonly OrgScopeAssignment[]>([]);

  protected readonly isEdit = computed(() => !!this.team()?.teamId);

  protected readonly hasLastActiveLocation = computed(() => {
    const team = this.team();
    return team?.lastActiveLatitude != null && team?.lastActiveLongitude != null;
  });

  protected readonly hasDeviceSnapshot = computed(() => {
    const team = this.team();
    return !!(
      team?.deviceName ||
      team?.deviceUuid ||
      team?.appVersion ||
      team?.deviceOs ||
      team?.lastActiveAt
    );
  });

  protected readonly form = this.fb.group({
    userCode: this.fb.control<string>('', [Validators.required, Validators.maxLength(50)]),
    name: this.fb.control<string>('', [Validators.required, Validators.maxLength(250)]),
    mobile: this.fb.control<string>('', Validators.maxLength(30)),
    teamType: this.fb.control<string>('', Validators.maxLength(50)),
    contractorId: this.fb.control<number | null>(null),
    email: this.fb.control<string>('', Validators.email),
    password: this.fb.control<string>(''),
    isActive: this.fb.control<boolean>(true),
  });

  ngOnInit(): void {
    const team = this.team();

    this.form.reset({
      userCode: team?.userCode ?? '',
      name: team?.name ?? '',
      mobile: team?.mobile ?? '',
      teamType: team?.teamType ?? '',
      contractorId: team?.contractorId ?? null,
      email: '',
      password: '',
      isActive: team?.isActive ?? true,
    });

    // The login is created once, with the team. Editing a crew changes the team record; the
    // password is reset from the users screen, so it is neither asked for nor required here.
    if (this.isEdit()) {
      this.form.controls.password.clearValidators();
      this.form.controls.userCode.disable();
    } else {
      this.form.controls.password.setValidators([Validators.required, Validators.minLength(8)]);
    }

    this.form.controls.password.updateValueAndValidity({ emitEvent: false });

    const scopes = [...(team?.scopes ?? [])];
    this.scopes.set(scopes);
    this.initialScopes.set(scopes);

    this.contractorsLoading.set(true);
    this.lookupService.getContractors()
      .pipe(finalize(() => this.contractorsLoading.set(false)))
      .subscribe({
        next: contractors => this.contractorOptions.set(contractors),
      });
  }

  protected onScopesChange(scopes: OrgScopeAssignment[]): void {
    this.scopes.set(scopes);
  }

  protected save(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const teamId = this.team()?.teamId;
    this.submitting.set(true);

    const request = teamId
      ? this.teamsClient.fsmsTeams_UpdateTeam(teamId, {
          id: teamId,
          userCode: value.userCode!.trim(),
          name: value.name!.trim(),
          mobile: value.mobile?.trim() || undefined,
          teamType: value.teamType?.trim() || undefined,
          contractorId: value.contractorId ?? undefined,
          scopes: this.scopes(),
          isActive: value.isActive ?? true,
        })
      : this.teamsClient.fsmsTeams_CreateTeam({
          userCode: value.userCode!.trim(),
          name: value.name!.trim(),
          mobile: value.mobile?.trim() || undefined,
          teamType: value.teamType?.trim() || undefined,
          contractorId: value.contractorId ?? undefined,
          email: value.email?.trim() || undefined,
          password: value.password!,
          scopes: this.scopes(),
          isActive: value.isActive ?? true,
        });

    const successKey = teamId ? 'teams.updatedSuccess' : 'teams.createdSuccess';
    const errorKey = teamId ? 'teams.updateError' : 'teams.createError';

    request.pipe(finalize(() => this.submitting.set(false))).subscribe({
      next: () => {
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('common.success'),
          detail: this.transloco.translate(successKey),
        });
        this.saved.emit();
      },
      error: (error: unknown) => {
        // Identity's own rejection — a weak password, a name already taken — says more than ours.
        const apiMessage = (error as { errors?: { message?: string }[] } | null)?.errors?.[0]
          ?.message;

        this.messageService.add({
          severity: 'error',
          summary: this.transloco.translate('common.error'),
          detail: apiMessage ?? this.transloco.translate(errorKey),
        });
      },
    });
  }

  protected cancel(): void {
    this.cancelled.emit();
  }
}
