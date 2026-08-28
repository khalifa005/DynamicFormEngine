import { Component, computed, effect, inject, input, model, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { TranslocoDirective } from '@jsverse/transloco';
import { catchError, finalize, forkJoin, of } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { MessageModule } from 'primeng/message';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TagModule } from 'primeng/tag';

import {
  FsmsSurveysClient,
  SurveyDetailDto,
  SurveyLatestFillDto,
} from '../../../../core/api/api-client.generated';
import { LanguageService } from '../../../../core/i18n/language.service';
import { DynamicFormRendererComponent } from '../../../../shared/components/dynamic-form/dynamic-form-renderer.component';
import { surveyStatusSeverity } from '../../survey-status';

/**
 * The filled form, shown exactly as it was filled and with nothing editable.
 *
 * Separate from {@link SurveyFillDialogComponent} rather than a flag on it, because the two answer
 * different questions and are reachable at different times. Filling is only open while the survey is
 * in flight; reading is open forever, and most often wanted precisely when it is not — an approved
 * survey, an imported one, a returned one whose reviewer wants to see what was actually submitted.
 *
 * The detail dialog's Records tab already lists the answers as label/value pairs. This shows the
 * form instead: the grouping, the ordering and the conditional sections are part of what the crew
 * saw, and an answer read outside them can be read wrongly.
 *
 * The definition comes off the survey, not the template — the survey pins the version it was raised
 * against, so a template republished since cannot redraw a form that was filled under the old one.
 */
@Component({
  selector: 'app-survey-preview-dialog',
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
  templateUrl: './survey-preview-dialog.component.html',
})
export class SurveyPreviewDialogComponent {
  readonly visible = model.required<boolean>();
  readonly surveyId = input<number | null>(null);

  private readonly surveysClient = inject(FsmsSurveysClient);
  private readonly language = inject(LanguageService);

  protected readonly loading = signal(false);
  protected readonly loadFailed = signal(false);
  protected readonly survey = signal<SurveyDetailDto | null>(null);
  protected readonly definition = signal<Record<string, unknown> | null>(null);
  protected readonly fill = signal<SurveyLatestFillDto | null>(null);

  protected readonly answers = computed<Record<string, unknown> | null>(
    () => this.fill()?.answers ?? null,
  );

  /** A survey that was never filled gets a message rather than an empty form pretending to be one. */
  protected readonly hasFill = computed(() => this.fill() !== null);

  protected readonly statusSeverity = surveyStatusSeverity;

  protected readonly templateName = computed(() => {
    const detail = this.survey();
    return this.language.current() === 'ar'
      ? detail?.templateNameAr || detail?.templateNameEn || ''
      : detail?.templateNameEn || detail?.templateNameAr || '';
  });

  constructor() {
    effect(() => {
      const id = this.surveyId();
      if (this.visible() && id) {
        this.load(id);
      }
    });
  }

  private load(id: number): void {
    this.survey.set(null);
    this.definition.set(null);
    this.fill.set(null);
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

          const found = latestFill?.data ?? null;
          this.fill.set(found?.hasFill ? found : null);
        },
        error: () => this.loadFailed.set(true),
      });
  }

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

  protected close(): void {
    this.visible.set(false);
  }
}
