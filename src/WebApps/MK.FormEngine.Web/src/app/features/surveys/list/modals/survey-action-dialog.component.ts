import { Component, effect, inject, input, model, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { Observable, finalize } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { TextareaModule } from 'primeng/textarea';

import {
  FsmsSurveysClient,
  SurveyDetailDtoResult,
  SurveyListItemDto,
} from '../../../../core/api/api-client.generated';
import { SurveyAction } from '../../survey-status';
import { surveyErrorMessage } from '../../survey-error';

/**
 * Allocation needs a team picker, filling needs the whole template form, and a return needs a
 * reason code plus an optional hand-over to another crew — each gets a dialog of its own and is
 * excluded here.
 */
export type SurveyNoteAction = Exclude<SurveyAction, 'allocate' | 'fill' | 'return'>;

/**
 * Result message per action, spelled out rather than derived from the action name — a wrong key
 * would surface to the user as raw key text.
 */
const RESULT_KEYS: Record<SurveyNoteAction, { success: string; failure: string }> = {
  complete: { success: 'surveys.messages.completed', failure: 'surveys.messages.completeFailed' },
  expire: { success: 'surveys.messages.expired', failure: 'surveys.messages.expireFailed' },
};

/**
 * The note-only lifecycle transitions — complete and expire. They differ only in wording and which
 * endpoint they call, so one dialog covers both rather than two near-identical copies.
 */
@Component({
  selector: 'app-survey-action-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslocoDirective,
    ButtonModule,
    DialogModule,
    TextareaModule,
  ],
  templateUrl: './survey-action-dialog.component.html',
})
export class SurveyActionDialogComponent {
  readonly visible = model.required<boolean>();
  readonly survey = input<SurveyListItemDto | null>(null);
  readonly action = input.required<SurveyNoteAction>();
  readonly completed = output<void>();

  private readonly surveysClient = inject(FsmsSurveysClient);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly fb = inject(FormBuilder);

  protected readonly saving = signal(false);

  protected readonly form = this.fb.group({
    note: this.fb.control<string>('', Validators.maxLength(1000)),
  });

  constructor() {
    effect(() => {
      if (this.visible()) {
        this.form.reset({ note: '' });
      }
    });
  }

  protected confirm(): void {
    const surveyId = this.survey()?.surveyId;
    if (this.form.invalid || this.saving() || !surveyId) {
      this.form.markAllAsTouched();
      return;
    }

    const note = this.form.getRawValue().note?.trim() || undefined;
    const action = this.action();
    const keys = RESULT_KEYS[action];
    this.saving.set(true);

    this.request(action, surveyId, note)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('common.success'),
            detail: this.transloco.translate(keys.success),
          });
          this.visible.set(false);
          this.completed.emit();
        },
        error: (error: unknown) => {
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: surveyErrorMessage(error, this.transloco.translate(keys.failure)),
          });
        },
      });
  }

  protected cancel(): void {
    this.visible.set(false);
  }

  private request(
    action: SurveyNoteAction,
    surveyId: number,
    note: string | undefined,
  ): Observable<SurveyDetailDtoResult> {
    switch (action) {
      case 'complete':
        return this.surveysClient.fsmsSurveys_Complete(surveyId, { surveyId, note });
      default:
        return this.surveysClient.fsmsSurveys_Expire(surveyId, { surveyId, note });
    }
  }
}
