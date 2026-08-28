import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { EventEmitter, Input, Output } from '@angular/core';
import { RouterModule } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import {
  FsmsTemplatesClient,
  FsmsDepartmentDto,
  FsmsFaTypeDto,
  CreateTemplateCommand,
  UpdateTemplateCommand,
} from '../../../core/api/api-client.generated';
import { FsmsLookupService } from '../../../core/lookups/fsms-lookup.service';
import { LanguageService } from '../../../core/i18n/language.service';
import { OrgScopeSelectorComponent } from '../../../shared/components/org-scope/org-scope-selector.component';
import { OrgScopeAssignment } from '../../../shared/components/org-scope/org-scope.model';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { CardModule } from 'primeng/card';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';

interface SelectOption<TValue> {
  label: string;
  value: TValue;
}

@Component({
  selector: 'app-template-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    ButtonModule,
    CheckboxModule,
    InputTextModule,
    SelectModule,
    MultiSelectModule,
    ToastModule,
    CardModule,
    ProgressSpinnerModule,
    TranslocoDirective,
    OrgScopeSelectorComponent
  ],
  providers: [MessageService],
  templateUrl: './template-form.html',
  styleUrl: './template-form.scss',
})
export class TemplateForm implements OnInit {
  private fb = inject(FormBuilder);
  private templatesClient = inject(FsmsTemplatesClient);
  private messageService = inject(MessageService);
  private lookups = inject(FsmsLookupService);
  private language = inject(LanguageService);
  private transloco = inject(TranslocoService);

  @Input() templateId?: number;
  @Output() saved = new EventEmitter<any>();
  @Output() canceled = new EventEmitter<void>();

  form!: FormGroup;
  isEditMode = false;

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly lookupsLoading = signal(false);

  private readonly departments = signal<FsmsDepartmentDto[]>([]);
  private readonly faTypes = signal<FsmsFaTypeDto[]>([]);

  /**
   * Where the template may be used. Replaces the comma-separated branch list: a template that covers
   * a whole CBU used to mean naming every branch in it, and nothing checked that those codes existed.
   */
  readonly scopes = signal<OrgScopeAssignment[]>([]);
  readonly initialScopes = signal<readonly OrgScopeAssignment[]>([]);

  /** Labels depend on the active language, so switching it relabels an open dialog. */
  readonly departmentOptions = computed<SelectOption<number>[]>(() =>
    this.departments()
      .filter(department => department.id != null)
      .map(department => ({
        label: this.localizedName(department.nameEn, department.nameAr),
        value: department.id!,
      }))
  );

  readonly faTypeOptions = computed<SelectOption<string>[]>(() =>
    this.faTypes()
      .filter(faType => !!faType.faTypeCode)
      .map(faType => ({
        label: `${faType.faTypeCode} — ${this.localizedName(faType.nameEn, faType.nameAr)}`,
        value: faType.faTypeCode!,
      }))
  );

  categories = [
    { label: 'Asset Inspection', value: 'ASSET_INSPECTION' },
    { label: 'Quality', value: 'QUALITY' },
    { label: 'Customer', value: 'CUSTOMER' },
    { label: 'Environmental', value: 'ENVIRONMENTAL' },
    { label: 'Safety', value: 'SAFETY' },
    { label: 'Custom', value: 'CUSTOM' }
  ];

  ngOnInit() {
    this.initForm();
    this.loadLookups();

    if (this.templateId) {
      this.isEditMode = true;
      this.loadTemplate();
    }
  }

  initForm() {
    this.form = this.fb.group({
      templateCode: ['', Validators.required],
      templateNameEn: ['', Validators.required],
      templateNameAr: ['', Validators.required],
      category: [null, Validators.required],
      departmentId: [null],
      faTypeCode: [null as string | null],
      teamFillSlaHours: [72, [Validators.required, Validators.min(1)]],
      completionSlaHours: [48, [Validators.required, Validators.min(1)]],
      // Off by default: pinning is what stops a republish changing the form under a crew mid-job,
      // so moving surveys forward automatically has to be asked for.
      autoMigrateSurveysOnPublish: [false],
    });
  }

  /**
   * Departments and branches come from the shared lookup cache, so reopening the dialog
   * does not hit the API again.
   */
  loadLookups() {
    this.lookupsLoading.set(true);

    forkJoin({
      departments: this.lookups.getDepartments(),
      faTypes: this.lookups.getFaTypes(),
    })
      .pipe(finalize(() => this.lookupsLoading.set(false)))
      .subscribe({
        next: ({ departments, faTypes }) => {
          this.departments.set(departments);
          this.faTypes.set(faTypes);
        },
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: this.transloco.translate('templates.lookupsLoadError'),
          });
        }
      });
  }

  loadTemplate() {
    if (!this.templateId) return;

    this.loading.set(true);
    this.templatesClient
      .fsmsTemplates_GetTemplateById(this.templateId)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          if (res.data) {
            this.form.patchValue({
              templateCode: res.data.templateCode,
              templateNameEn: res.data.templateNameEn,
              templateNameAr: res.data.templateNameAr,
              category: res.data.category,
              departmentId: res.data.departmentId ?? null,
              faTypeCode: res.data.faTypeCode ?? null,
              teamFillSlaHours: res.data.teamFillSlaHours ?? 72,
              completionSlaHours: res.data.completionSlaHours ?? 48,
              autoMigrateSurveysOnPublish: res.data.autoMigrateSurveysOnPublish ?? false,
            });

            const loaded = res.data.scopes ?? [];
            this.scopes.set([...loaded]);
            this.initialScopes.set([...loaded]);
          }
        },
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: this.transloco.translate('templates.loadError'),
          });
        }
      });
  }

  save() {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    const formValue = this.form.value;
    const departmentId: number | undefined = formValue.departmentId ?? undefined;
    const faTypeCode: string | undefined = formValue.faTypeCode?.trim() || undefined;
    const scopes = this.scopes();

    this.saving.set(true);

    if (this.isEditMode && this.templateId) {
      const command: UpdateTemplateCommand = {
        templateNameEn: formValue.templateNameEn,
        templateNameAr: formValue.templateNameAr,
        category: formValue.category,
        departmentId,
        faTypeCode,
        teamFillSlaHours: Number(formValue.teamFillSlaHours),
        completionSlaHours: Number(formValue.completionSlaHours),
        autoMigrateSurveysOnPublish: !!formValue.autoMigrateSurveysOnPublish,
        scopes,
      };

      this.templatesClient
        .fsmsTemplates_UpdateTemplate(this.templateId, command)
        .pipe(finalize(() => this.saving.set(false)))
        .subscribe({
          next: (res) => {
            this.messageService.add({
              severity: 'success',
              summary: this.transloco.translate('common.success'),
              detail: this.transloco.translate('templates.updateSuccess'),
            });
            this.saved.emit(res.data);
          },
          error: () => {
            this.messageService.add({
              severity: 'error',
              summary: this.transloco.translate('common.error'),
              detail: this.transloco.translate('templates.updateError'),
            });
          }
        });

      return;
    }

    const command: CreateTemplateCommand = {
      templateCode: formValue.templateCode,
      templateNameEn: formValue.templateNameEn,
      templateNameAr: formValue.templateNameAr,
      category: formValue.category,
      departmentId,
      faTypeCode,
      teamFillSlaHours: Number(formValue.teamFillSlaHours),
      completionSlaHours: Number(formValue.completionSlaHours),
      autoMigrateSurveysOnPublish: !!formValue.autoMigrateSurveysOnPublish,
      scopes,
    };

    this.templatesClient
      .fsmsTemplates_CreateTemplate(command)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (res) => {
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('common.success'),
            detail: this.transloco.translate('templates.createSuccess'),
          });
          this.saved.emit(res.data);
        },
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: this.transloco.translate('templates.createError'),
          });
        }
      });
  }

  cancel() {
    this.canceled.emit();
  }

  private localizedName(nameEn?: string, nameAr?: string): string {
    const preferred = this.language.current() === 'ar' ? nameAr : nameEn;
    return preferred?.trim() || nameEn?.trim() || nameAr?.trim() || '';
  }

  onScopesChange(scopes: OrgScopeAssignment[]): void {
    this.scopes.set(scopes);
  }
}
