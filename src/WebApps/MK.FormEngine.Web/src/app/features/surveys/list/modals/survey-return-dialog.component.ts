import { Component, inject, input, model, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { finalize } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { MessageModule } from 'primeng/message';
import { MessageService } from 'primeng/api';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';

import {
  FsmsSurveysClient,
  FsmsTeamsClient,
  SurveyListItemDto,
  ReturnSurveyCommand,
} from '../../../../core/api/api-client.generated';
import { FsmsLookupService } from '../../../../core/lookups/fsms-lookup.service';
import { LanguageService } from '../../../../core/i18n/language.service';
import { surveyErrorMessage } from '../../survey-error';

interface Option<T> {
  readonly label: string;
  readonly value: T;
}

/**
 * Sends a reviewed survey back for rework. Split out from the shared note-only action dialog
 * because a return carries two things the others do not: a structured reason code the crew's
 * worklist tags the row with, and an optional hand-over to a different team.
 */
@Component({
  selector: 'app-survey-return-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslocoDirective,
    ButtonModule,
    DialogModule,
    MessageModule,
    SelectModule,
    TextareaModule,
  ],
  templateUrl: './survey-return-dialog.component.html',
})
export class SurveyReturnDialogComponent {
  readonly visible = model.required<boolean>();
  readonly survey = input<SurveyListItemDto | null>(null);
  readonly completed = output<void>();

  private readonly surveysClient = inject(FsmsSurveysClient);
  private readonly teamsClient = inject(FsmsTeamsClient);
  private readonly lookups = inject(FsmsLookupService);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);
  private readonly fb = inject(FormBuilder);

  protected readonly saving = signal(false);
  protected readonly loadingOptions = signal(false);
  protected readonly reasons = signal<Option<string>[]>([]);
  protected readonly teams = signal<Option<number>[]>([]);

  protected readonly form = this.fb.group({
    reasonCode: this.fb.control<string | null>(null, Validators.required),
    reason: this.fb.control<string>('', [Validators.required, Validators.maxLength(1000)]),
    // Left null the survey stays with the crew that filled it; naming a team hands the rework over.
    reassignToFieldTeamId: this.fb.control<number | null>(null),
  });

  protected onShow(): void {
    this.form.reset({ reasonCode: null, reason: '', reassignToFieldTeamId: null });
    this.loadOptions();
  }

  protected confirm(): void {
    const surveyId = this.survey()?.surveyId;

    if (this.form.invalid || this.saving() || !surveyId) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.saving.set(true);

    this.surveysClient
      .fsmsSurveys_Return(surveyId, {
        surveyId,
        reasonCode: value.reasonCode!,
        reason: (value.reason ?? '').trim(),
        reassignToFieldTeamId: value.reassignToFieldTeamId ?? undefined,
      } as ReturnSurveyCommand)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('common.success'),
            detail: this.transloco.translate(
              value.reassignToFieldTeamId
                ? 'surveys.messages.returnedToOtherTeam'
                : 'surveys.messages.returned',
            ),
          });
          this.visible.set(false);
          this.completed.emit();
        },
        error: (error: unknown) => {
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: surveyErrorMessage(
              error,
              this.transloco.translate('surveys.messages.returnFailed'),
            ),
          });
        },
      });
  }

  protected cancel(): void {
    this.visible.set(false);
  }

  private loadOptions(): void {
    this.loadingOptions.set(true);

    this.lookups.getReturnReasons().subscribe({
      next: (items) =>
        this.reasons.set(
          items.map((reason) => ({
            label: this.language.isRtl() ? (reason.nameAr ?? '') : (reason.nameEn ?? ''),
            value: reason.code!,
          })),
        ),
      error: () => this.reasons.set([]),
    });

    // The crew currently holding the survey is dropped from the list: picking it would be a
    // same-team return, which is what leaving the field empty already does.
    const currentTeamId = this.survey()?.allocatedFieldTeamId;

    this.teamsClient
      .fsmsTeams_GetTeamsPaged(1, 500, undefined, true)
      .pipe(finalize(() => this.loadingOptions.set(false)))
      .subscribe({
        next: (res) =>
          this.teams.set(
            (res.data?.items ?? [])
              .filter((team) => team.teamId !== currentTeamId)
              .map((team) => ({
                label: `${team.userCode ?? ''} — ${team.name ?? ''}`.trim(),
                value: team.teamId!,
              })),
          ),
        error: () => this.teams.set([]),
      });
  }
}
