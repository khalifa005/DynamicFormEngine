import { Component, effect, inject, input, model, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { finalize } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { MessageModule } from 'primeng/message';
import { MessageService } from 'primeng/api';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';

import {
  FsmsSurveysClient,
  SurveyListItemDto,
  AllocateSurveyCommand,
} from '../../../../core/api/api-client.generated';
import { surveyErrorMessage } from '../../survey-error';

interface TeamOption {
  readonly label: string;
  readonly value: number;

  /** How many surveys the crew already holds — shown as a badge beside its name. */
  readonly activeAssignmentCount: number;
}

/**
 * Allocates a pending survey to a field team. This is the only transition that also creates a
 * record of its own — the API creates the assignment inside the same call.
 */
@Component({
  selector: 'app-survey-allocate-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslocoDirective,
    ButtonModule,
    DatePickerModule,
    DialogModule,
    MessageModule,
    SelectModule,
    TagModule,
    TextareaModule,
    TooltipModule,
  ],
  templateUrl: './survey-allocate-dialog.component.html',
})
export class SurveyAllocateDialogComponent {
  readonly visible = model.required<boolean>();
  readonly survey = input<SurveyListItemDto | null>(null);
  readonly allocated = output<void>();

  private readonly surveysClient = inject(FsmsSurveysClient);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly fb = inject(FormBuilder);

  protected readonly saving = signal(false);
  protected readonly loadingTeams = signal(false);
  protected readonly teams = signal<TeamOption[]>([]);

  protected readonly form = this.fb.group({
    fieldTeamId: this.fb.control<number | null>(null, Validators.required),
    dueDate: this.fb.control<Date | null>(null),
    completionDueDate: this.fb.control<Date | null>(null),
    note: this.fb.control<string>('', Validators.maxLength(1000)),
  });

  constructor() {}

  protected onShow(): void {
    const current = this.survey();
    const defaults = slaDeadlineDefaults(current);
    this.form.reset({
      fieldTeamId: null,
      dueDate: current?.dueDate ? new Date(current.dueDate) : defaults.fillDue,
      completionDueDate: current?.completionDueDate
        ? new Date(current.completionDueDate)
        : defaults.completionDue,
      note: '',
    });
    this.loadTeams(current?.surveyId);
  }

  protected allocate(): void {
    const surveyId = this.survey()?.surveyId;
    if (this.form.invalid || this.saving() || !surveyId) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);

    this.surveysClient
      .fsmsSurveys_Allocate(surveyId, {
        surveyId,
        fieldTeamId: value.fieldTeamId!,
        dueDate: value.dueDate ?? undefined,
        completionDueDate: value.completionDueDate ?? undefined,
        note: value.note?.trim() || undefined,
      } as AllocateSurveyCommand)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('common.success'),
            detail: this.transloco.translate('surveys.messages.allocated'),
          });
          this.visible.set(false);
          this.allocated.emit();
        },
        error: (error: unknown) => {
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: surveyErrorMessage(
              error,
              this.transloco.translate('surveys.messages.allocateFailed'),
            ),
          });
        },
      });
  }

  protected cancel(): void {
    this.visible.set(false);
  }

  /**
   * Only the crews whose territory actually covers this survey — the same rule the allocate
   * endpoint enforces, so the picker cannot offer a choice that is guaranteed to be refused.
   */
  private loadTeams(surveyId?: number): void {
    if (surveyId == null) {
      this.teams.set([]);
      return;
    }

    this.loadingTeams.set(true);
    this.surveysClient
      .fsmsSurveys_GetEligibleTeams(surveyId)
      .pipe(finalize(() => this.loadingTeams.set(false)))
      .subscribe({
        next: (res) =>
          this.teams.set(
            (res.data ?? []).map((team) => ({
              label: `${team.userCode ?? ''} — ${team.name ?? ''}`.trim(),
              value: team.teamId!,
              activeAssignmentCount: team.activeAssignmentCount ?? 0,
            })),
          ),
        error: () => this.teams.set([]),
      });
  }
}

/** Prefills allocate deadlines from snapshotted SLA hours when the survey has no dates yet. */
function slaDeadlineDefaults(survey: SurveyListItemDto | null): {
  fillDue: Date | null;
  completionDue: Date | null;
} {
  const fillHours = survey?.teamFillSlaHours;
  const completionHours = survey?.completionSlaHours;
  if (fillHours == null || fillHours <= 0) {
    return { fillDue: null, completionDue: null };
  }

  const now = Date.now();
  const fillDue = new Date(now + fillHours * 60 * 60 * 1000);
  const completionDue =
    completionHours != null && completionHours > 0
      ? new Date(now + (fillHours + completionHours) * 60 * 60 * 1000)
      : null;

  return { fillDue, completionDue };
}
