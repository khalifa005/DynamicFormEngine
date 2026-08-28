import { Component, effect, inject, model, output, signal } from '@angular/core';
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

import {
  FsmsLookupsClient,
  FsmsSurveysClient,
  FsmsTemplatesClient,
} from '../../../../core/api/api-client.generated';
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
  readonly value: string | number;
}

/**
 * Raises a survey by hand. Only Published templates are offered — the API rejects anything else,
 * because a draft has no frozen version for the survey to pin to.
 */
@Component({
  selector: 'app-survey-create-dialog',
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
  templateUrl: './survey-create-dialog.component.html',
})
export class SurveyCreateDialogComponent {
  readonly visible = model.required<boolean>();
  readonly created = output<void>();

  private readonly surveysClient = inject(FsmsSurveysClient);
  private readonly templatesClient = inject(FsmsTemplatesClient);
  private readonly lookupsClient = inject(FsmsLookupsClient);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly fb = inject(FormBuilder);

  protected readonly saving = signal(false);
  protected readonly loadingOptions = signal(false);
  protected readonly templates = signal<SelectOption[]>([]);
  protected readonly departments = signal<SelectOption[]>([]);
  protected readonly faTypes = signal<SelectOption[]>([]);
  protected readonly taskTypes = signal<SelectOption[]>([]);
  protected readonly customerTypes = signal<SelectOption[]>([]);

  protected readonly location = signal<OrgLocation>({ ...EMPTY_ORG_LOCATION });
  protected readonly locationSeed = signal<OrgLocation>({ ...EMPTY_ORG_LOCATION });

  protected readonly locationPoint = signal<GeoPoint | null>(null);
  protected readonly locationTouched = signal(false);

  /**
   * The FA type is optional here on purpose. It exists so the inbound feed can pick a template
   * without knowing which one models the asset; an operator raising work by hand has already named
   * the template outright, and a crew logging something they found on site may have no FA type at
   * all. The API ignores it entirely once a template is named.
   */
  protected readonly form = this.fb.group({
    templateId: this.fb.control<number | null>(null, Validators.required),
    faId: this.fb.control<string>('', Validators.maxLength(100)),
    taskCode: this.fb.control<string>('', Validators.maxLength(100)),
    faTypeCode: this.fb.control<string | null>(null),
    taskTypeId: this.fb.control<number | null>(null),
    customerName: this.fb.control<string>('', Validators.maxLength(250)),
    customerPhone: this.fb.control<string>('', Validators.maxLength(50)),
    customerTypeId: this.fb.control<number | null>(null),
    meterNumber: this.fb.control<string>('', Validators.maxLength(100)),
    hcn: this.fb.control<string>('', Validators.maxLength(50)),
    departmentId: this.fb.control<number | null>(null),
      dueDate: this.fb.control<Date | null>(null),
      completionDueDate: this.fb.control<Date | null>(null),
    });

  protected onShow(): void {
    this.form.reset();
    this.location.set({ ...EMPTY_ORG_LOCATION });
    // A fresh identity is what tells the picker to clear its cascade for the new survey.
    this.locationSeed.set({ ...EMPTY_ORG_LOCATION });
    this.locationPoint.set(null);
    this.locationTouched.set(false);
    this.loadOptions();
  }

  protected onLocationChange(location: OrgLocation): void {
    this.location.set(location);
  }

  protected onLocationPointChange(point: GeoPoint | null): void {
    this.locationPoint.set(point);
    this.locationTouched.set(true);
  }

  protected save(): void {
    const point = this.locationPoint();
    if (this.form.invalid || this.saving() || point == null) {
      this.form.markAllAsTouched();
      this.locationTouched.set(true);
      return;
    }

    const value = this.form.getRawValue();
    const location = this.location();
    this.saving.set(true);

    this.surveysClient
      .fsmsSurveys_CreateSurvey({
        templateId: value.templateId!,
        faId: value.faId?.trim() || undefined,
        taskCode: value.taskCode?.trim() || undefined,
        faTypeCode: value.faTypeCode ?? undefined,
        taskTypeId: value.taskTypeId ?? undefined,
        customerName: value.customerName?.trim() || undefined,
        customerPhone: value.customerPhone?.trim() || undefined,
        customerTypeId: value.customerTypeId ?? undefined,
        meterNumber: value.meterNumber?.trim() || undefined,
        hcn: value.hcn?.trim() || undefined,
        cbuCode: location.cbuCode ?? undefined,
        branchCode: location.branchCode ?? undefined,
        operationAreaCode: location.operationAreaCode ?? undefined,
        departmentId: value.departmentId ?? undefined,
          dueDate: value.dueDate ?? undefined,
          completionDueDate: value.completionDueDate ?? undefined,
          latitude: point.lat,
          longitude: point.lng,
      })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('common.success'),
            detail: this.transloco.translate('surveys.messages.created'),
          });
          this.visible.set(false);
          this.created.emit();
        },
        error: (error: unknown) => {
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: surveyErrorMessage(error, this.transloco.translate('surveys.messages.createFailed')),
          });
        },
      });
  }

  protected cancel(): void {
    this.visible.set(false);
  }

  private loadOptions(): void {
    this.loadingOptions.set(true);
    // The geography is loaded by the scope picker itself, so only the dropdown lists are awaited here.
    let pending = 5;
    const done = () => {
      pending -= 1;
      if (pending === 0) {
        this.loadingOptions.set(false);
      }
    };

    this.templatesClient
      .fsmsTemplates_GetTemplates(1, 500, undefined, 'Published')
      .subscribe({
        next: (res) =>
          this.templates.set(
            (res.data?.items ?? []).map((t) => ({
              label: `${t.templateCode ?? ''} — ${t.templateNameEn ?? ''}`.trim(),
              value: t.templateId!,
            })),
          ),
        error: () => this.templates.set([]),
        complete: done,
      });

    this.lookupsClient.fsmsLookups_GetDepartments(1, 500, undefined, true).subscribe({
      next: (res) =>
        this.departments.set(
          (res.data?.items ?? []).map((d) => ({ label: d.nameEn ?? '', value: d.id! })),
        ),
      error: () => this.departments.set([]),
      complete: done,
    });

    this.lookupsClient.fsmsLookups_GetFaTypes(1, 500, undefined, true).subscribe({
      next: (res) =>
        this.faTypes.set(
          (res.data?.items ?? []).map((f) => ({
            label: `${f.faTypeCode ?? ''} — ${f.nameEn ?? ''}`.trim(),
            value: f.faTypeCode!,
          })),
        ),
      error: () => this.faTypes.set([]),
      complete: done,
    });

    this.lookupsClient.fsmsLookups_GetTaskTypes(1, 500, undefined, true).subscribe({
      next: (res) =>
        this.taskTypes.set(
          (res.data?.items ?? []).map((t) => ({
            label: `${t.code ?? ''} — ${t.nameEn ?? ''}`.trim(),
            value: t.id!,
          })),
        ),
      error: () => this.taskTypes.set([]),
      complete: done,
    });

    this.lookupsClient.fsmsLookups_GetCustomerTypes(1, 500, undefined, true).subscribe({
      next: (res) =>
        this.customerTypes.set(
          (res.data?.items ?? []).map((c) => ({
            label: `${c.code ?? ''} — ${c.nameEn ?? ''}`.trim(),
            value: c.id!,
          })),
        ),
      error: () => this.customerTypes.set([]),
      complete: done,
    });
  }
}
