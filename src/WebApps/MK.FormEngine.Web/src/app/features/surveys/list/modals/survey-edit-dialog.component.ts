import { Component, inject, input, model, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { finalize } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';
import { SelectModule } from 'primeng/select';

import { FsmsSurveysClient, SurveyListItemDto } from '../../../../core/api/api-client.generated';
import { FsmsLookupService } from '../../../../core/lookups/fsms-lookup.service';
import { OrgScopeSelectorComponent } from '../../../../shared/components/org-scope/org-scope-selector.component';
import {
  EMPTY_ORG_LOCATION,
  OrgLocation,
} from '../../../../shared/components/org-scope/org-scope.model';
import { GeoMapComponent } from '../../../../shared/components/geo-map/geo-map.component';
import type { GeoPoint } from '../../../../shared/components/geo-map/google-maps.types';
import { surveyErrorMessage } from '../../survey-error';

interface SelectOption {
  readonly label: string;
  readonly value: number;
}

/**
 * Corrects where a survey sits — its place in the geography, its department and its deadline.
 * Only offered before the survey has been filled; the API enforces the same rule, so a survey that
 * moved on between opening this dialog and saving it is refused rather than quietly relocated.
 */
@Component({
  selector: 'app-survey-edit-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    TranslocoDirective,
    ButtonModule,
    DatePickerModule,
    DialogModule,
    InputTextModule,
    SelectModule,
    OrgScopeSelectorComponent,
    GeoMapComponent,
  ],
  templateUrl: './survey-edit-dialog.component.html',
})
export class SurveyEditDialogComponent {
  readonly visible = model.required<boolean>();
  readonly survey = input<SurveyListItemDto | null>(null);
  readonly updated = output<void>();

  private readonly surveysClient = inject(FsmsSurveysClient);
  private readonly lookups = inject(FsmsLookupService);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly fb = inject(FormBuilder);

  protected readonly saving = signal(false);
  protected readonly departments = signal<SelectOption[]>([]);
  protected readonly loadingDepartments = signal(false);

  protected readonly location = signal<OrgLocation>({ ...EMPTY_ORG_LOCATION });
  protected readonly initialLocation = signal<OrgLocation | null>(null);

  protected readonly locationPoint = signal<GeoPoint | null>(null);

  protected readonly form = this.fb.group({
    customerName: this.fb.control<string>('', Validators.maxLength(250)),
    customerPhone: this.fb.control<string>('', Validators.maxLength(50)),
    departmentId: this.fb.control<number | null>(null),
      dueDate: this.fb.control<Date | null>(null),
      completionDueDate: this.fb.control<Date | null>(null),
    });

  protected onShow(): void {
    this.loadDepartments();

    const survey = this.survey();

    const seeded: OrgLocation = {
      // A survey stores no cluster — the picker resolves it from the CBU when restoring.
      clusterCode: null,
      cbuCode: survey?.cbuCode ?? null,
      branchCode: survey?.branchCode ?? null,
      operationAreaCode: survey?.operationAreaCode ?? null,
    };

    this.location.set(seeded);
    this.initialLocation.set(seeded);

    this.locationPoint.set(
      survey?.latitude && survey?.longitude
        ? { lat: survey.latitude, lng: survey.longitude }
        : null
    );

    // Seeded from the row rather than left blank: the API replaces every field it is sent, so an
    // empty customer box would clear a name the dispatcher never meant to touch.
    this.form.reset({
        customerName: survey?.customerName ?? '',
        customerPhone: survey?.customerPhone ?? '',
        departmentId: survey?.departmentId ?? null,
        dueDate: survey?.dueDate ? new Date(survey.dueDate) : null,
        completionDueDate: survey?.completionDueDate
          ? new Date(survey.completionDueDate)
          : null,
      });
    }

  protected onLocationChange(location: OrgLocation): void {
    this.location.set(location);
  }

  protected save(): void {
    const surveyId = this.survey()?.surveyId;

    if (!surveyId || this.saving()) {
      return;
    }

    const value = this.form.getRawValue();
    const location = this.location();
    const point = this.locationPoint();
    this.saving.set(true);

    this.surveysClient
      .fsmsSurveys_UpdateSurvey(surveyId, {
        customerName: value.customerName?.trim() || undefined,
        customerPhone: value.customerPhone?.trim() || undefined,
        cbuCode: location.cbuCode ?? undefined,
        branchCode: location.branchCode ?? undefined,
        operationAreaCode: location.operationAreaCode ?? undefined,
        departmentId: value.departmentId ?? undefined,
          dueDate: value.dueDate ?? undefined,
          completionDueDate: value.completionDueDate ?? undefined,
          latitude: point?.lat ?? undefined,
        longitude: point?.lng ?? undefined,
      })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('common.success'),
            detail: this.transloco.translate('surveys.updated'),
          });
          this.visible.set(false);
          this.updated.emit();
        },
        error: (error: unknown) =>
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: surveyErrorMessage(
              error,
              this.transloco.translate('surveys.updateFailed'),
            ),
          }),
      });
  }

  protected cancel(): void {
    this.visible.set(false);
  }

  private loadDepartments(): void {
    this.loadingDepartments.set(true);

    this.lookups
      .getDepartments()
      .pipe(finalize(() => this.loadingDepartments.set(false)))
      .subscribe({
        next: departments =>
          this.departments.set(
            departments
              .filter(department => department.id != null)
              .map(department => ({
                label: this.localized(department.nameEn, department.nameAr),
                value: department.id!,
              })),
          ),
        error: () => this.departments.set([]),
      });
  }

  private localized(nameEn?: string, nameAr?: string): string {
    return this.transloco.getActiveLang() === 'ar'
      ? (nameAr || nameEn || '')
      : (nameEn || nameAr || '');
  }
}
