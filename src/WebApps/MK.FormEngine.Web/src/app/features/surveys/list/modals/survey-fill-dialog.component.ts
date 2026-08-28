import { Component, computed, effect, inject, input, model, output, signal, viewChild } from '@angular/core';
import { DatePipe } from '@angular/common';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { catchError, finalize, forkJoin, of } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { MessageModule } from 'primeng/message';
import { MessageService } from 'primeng/api';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';

import {
  FsmsSurveySubmissionsClient,
  FsmsSurveysClient,
  SurveyDetailDto,
  SurveyLatestFillDto,
} from '../../../../core/api/api-client.generated';
import { LanguageService } from '../../../../core/i18n/language.service';
import { DynamicFormRendererComponent } from '../../../../shared/components/dynamic-form/dynamic-form-renderer.component';
import { surveyStatusSeverity } from '../../survey-status';
import { surveyErrorMessage } from '../../survey-error';

/**
 * The back office's own fill of a survey.
 *
 * The form comes from the survey, not the template: the detail document carries the definition
 * frozen at the version the survey was pinned to, so a template republished since never changes the
 * form under a survey already in flight. Rendering is `app-dynamic-form-renderer` — the same
 * component behind the builder's preview dialog — so a field looks and behaves here exactly as the
 * designer saw it.
 */
@Component({
  selector: 'app-survey-fill-dialog',
  standalone: true,
  imports: [
    DatePipe,
    TranslocoDirective,
    ButtonModule,
    DialogModule,
    MessageModule,
    ProgressSpinnerModule,
    TagModule,
    DynamicFormRendererComponent,
  ],
  templateUrl: './survey-fill-dialog.component.html',
})
export class SurveyFillDialogComponent {
  readonly visible = model.required<boolean>();
  readonly surveyId = input<number | null>(null);
  readonly filled = output<void>();

  private readonly renderer = viewChild(DynamicFormRendererComponent);

  private readonly surveysClient = inject(FsmsSurveysClient);
  private readonly submissionsClient = inject(FsmsSurveySubmissionsClient);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly loadFailed = signal(false);
  protected readonly survey = signal<SurveyDetailDto | null>(null);

  /** The pinned definition, parsed once per load and handed to the renderer. */
  protected readonly definition = signal<Record<string, unknown> | null>(null);

  /** The previous fill, so a second pass edits the answers instead of retyping them. */
  protected readonly previousFill = signal<SurveyLatestFillDto | null>(null);

  protected readonly seedAnswers = computed<Record<string, unknown> | null>(
    () => this.previousFill()?.answers ?? null,
  );

  protected readonly statusSeverity = surveyStatusSeverity;

  protected readonly templateName = computed(() => {
    const detail = this.survey();
    return this.language.current() === 'ar'
      ? detail?.templateNameAr || detail?.templateNameEn || ''
      : detail?.templateNameEn || detail?.templateNameAr || '';
  });

  /** The team currently holding the survey, if any — shown so the reviewer knows whose work this is. */
  protected readonly allocatedTeam = computed(
    () =>
      this.survey()
        ?.assignments?.find((assignment) => assignment.isActive)
        ?.fieldTeamName ?? '',
  );

  constructor() {
    effect(() => {
      const id = this.surveyId();
      if (this.visible() && id) {
        this.load(id);
      }
    });
  }

  /**
   * The form and the answers that go in it arrive together — building the form first and seeding it
   * afterwards would rebuild it under the respondent a moment after it appeared. A previous fill
   * that fails to load is not fatal: the form still opens, just blank.
   */
  private load(id: number): void {
    this.survey.set(null);
    this.definition.set(null);
    this.previousFill.set(null);
    this.loadFailed.set(false);
    this.loading.set(true);

    forkJoin({
      detail: this.surveysClient.fsmsSurveys_GetSurveyById(id),
      latestFill: this.surveysClient.fsmsSurveys_GetLatestFill(id).pipe(catchError(() => of(null))),
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ detail, latestFill }) => {
          const survey = detail.data ?? null;
          this.survey.set(survey);
          this.definition.set(this.parseDefinition(survey?.definitionJson));

          const fill = latestFill?.data ?? null;
          this.previousFill.set(fill?.hasFill ? fill : null);
        },
        error: () => this.loadFailed.set(true),
      });
  }

  /** An unparseable definition is a load failure, not an empty form — say so rather than show nothing. */
  private parseDefinition(definitionJson?: string): Record<string, unknown> | null {
    if (!definitionJson) {
      return null;
    }

    try {
      return JSON.parse(definitionJson) as Record<string, unknown>;
    } catch {
      this.loadFailed.set(true);
      return null;
    }
  }

  protected submit(): void {
    const detail = this.survey();
    const renderer = this.renderer();
    if (this.saving() || !renderer || !detail?.templateId || !detail.surveyId) {
      return;
    }

    if (!renderer.validate()) {
      this.messageService.add({
        severity: 'warn',
        summary: this.transloco.translate('common.error'),
        detail: this.transloco.translate('surveys.fill.invalid'),
      });
      return;
    }

    // No assignmentId: naming one tells the API the allocation was worked, and a back-office fill
    // must not close out the field team's work on its behalf.
    this.saving.set(true);
    this.submissionsClient
      .fsmsSurveySubmissions_Submit(detail.templateId, {
        templateId: detail.templateId,
        surveyId: detail.surveyId,
        answers: renderer.payload(),
      })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('common.success'),
            detail: this.transloco.translate('surveys.messages.filled'),
          });
          this.visible.set(false);
          this.filled.emit();
        },
        error: (error: unknown) => {
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: surveyErrorMessage(
              error,
              this.transloco.translate('surveys.messages.fillFailed'),
            ),
          });
        },
      });
  }

  protected cancel(): void {
    this.visible.set(false);
  }
}
