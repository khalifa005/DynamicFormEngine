import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { finalize } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { MenuModule } from 'primeng/menu';
import { ContextMenuModule } from 'primeng/contextmenu';
import { MenuItem, MessageService } from 'primeng/api';
import { SelectModule } from 'primeng/select';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';

import {
  FsmsSurveysClient,
  SurveyListItemDto,
  API_BASE_URL,
} from '../../../core/api/api-client.generated';
import { LanguageService } from '../../../core/i18n/language.service';
import {
  EMPTY_ORG_LOCATION,
  OrgLocation,
} from '../../../shared/components/org-scope/org-scope.model';
import {
  SURVEY_RETURN_REASONS,
  SURVEY_SOURCES,
  SURVEY_STATUSES,
  canMigrateVersion,
  canReallocate,
  canRunSurveyAction,
  surveyReturnReasonSeverity,
  surveyStatusSeverity,
  wasSubmittedAgain,
} from '../survey-status';
import { surveyErrorMessage } from '../survey-error';
import { SurveyCreateDialogComponent } from './modals/survey-create-dialog.component';
import { SurveyAllocateDialogComponent } from './modals/survey-allocate-dialog.component';
import { SurveyBulkAllocateDialogComponent } from './modals/survey-bulk-allocate-dialog.component';
import {
  SurveyActionDialogComponent,
  SurveyNoteAction,
} from './modals/survey-action-dialog.component';
import { SurveyDetailDialogComponent } from './modals/survey-detail-dialog.component';
import { SurveyEditDialogComponent } from './modals/survey-edit-dialog.component';
import { SurveyFillDialogComponent } from './modals/survey-fill-dialog.component';
import { SurveyReturnDialogComponent } from './modals/survey-return-dialog.component';
import { SurveyOrgFilterDialogComponent } from './modals/survey-org-filter-dialog.component';

interface FilterOption {
  readonly label: string;
  readonly value: string;
}

/**
 * The survey worklist. Every filter and the paging are server-side — the list spans the whole
 * back office, so it is never safe to assume a page holds everything worth filtering.
 */
@Component({
  selector: 'app-survey-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslocoDirective,
    ButtonModule,
    DatePickerModule,
    IconFieldModule,
    InputIconModule,
    InputTextModule,
    MenuModule,
    ContextMenuModule,
    SelectModule,
    TableModule,
    TagModule,
    ToastModule,
    TooltipModule,
    SurveyCreateDialogComponent,
    SurveyAllocateDialogComponent,
    SurveyBulkAllocateDialogComponent,
    SurveyActionDialogComponent,
    SurveyDetailDialogComponent,
    SurveyFillDialogComponent,
    SurveyReturnDialogComponent,
    SurveyEditDialogComponent,
    SurveyOrgFilterDialogComponent,
  ],
  providers: [MessageService],
  templateUrl: './survey-list.component.html',
})
export class SurveyListComponent implements OnInit {
  private readonly surveysClient = inject(FsmsSurveysClient);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly surveys = signal<SurveyListItemDto[]>([]);
  protected readonly totalRecords = signal(0);
  protected readonly loading = signal(false);
  protected readonly statusSeverity = surveyStatusSeverity;
  protected readonly returnReasonSeverity = surveyReturnReasonSeverity;
  protected readonly wasSubmittedAgain = wasSubmittedAgain;

  // Server-side filters.
  protected search = '';
  protected status: string | null = null;
  protected source: string | null = null;
  protected returnReasonCode: string | null = null;
  protected createdFrom: Date | null = null;
  protected createdTo: Date | null = null;

  /**
   * Where in the geography to narrow to. Held as one object so the cascade picker can be seeded
   * from it; the API is sent whichever levels are set, and the scope of the signed-in user still
   * applies on top — these filters can only narrow what they were already allowed to see.
   */
  protected orgFilter: OrgLocation = { ...EMPTY_ORG_LOCATION };

  /**
   * The narrowest code in force, or null when the filter is off. Drives the
   * trigger button's badge so the applied scope stays visible while the dialog
   * that owns the cascade is closed.
   */
  protected get orgFilterLabel(): string | null {
    return (
      this.orgFilter.operationAreaCode ??
      this.orgFilter.branchCode ??
      this.orgFilter.cbuCode ??
      this.orgFilter.clusterCode ??
      null
    );
  }

  // Table sorting
  protected sortField: string | null = null;
  protected sortOrder = 1;

  protected page = 1;
  protected pageSize = 10;

  protected readonly statusOptions = signal<FilterOption[]>([]);
  protected readonly sourceOptions = signal<FilterOption[]>([]);
  protected readonly returnReasonOptions = signal<FilterOption[]>([]);

  // Dialog state — each dialog is its own component; the page only owns visibility + subject.
  protected readonly createVisible = signal(false);
  protected readonly allocateVisible = signal(false);
  protected readonly bulkAllocateVisible = signal(false);
  protected readonly actionVisible = signal(false);
  protected readonly returnVisible = signal(false);
  protected readonly detailVisible = signal(false);
  protected readonly fillVisible = signal(false);
  protected readonly editVisible = signal(false);
  protected readonly orgFilterVisible = signal(false);
  protected readonly selectedSurvey = signal<SurveyListItemDto | null>(null);
  protected readonly selectedSurveyId = signal<number | null>(null);
  protected readonly currentAction = signal<SurveyNoteAction>('complete');

  protected menuItems: MenuItem[] = [];

  /**
   * The same actions as the row's ellipsis menu, reached by right-clicking the
   * row. Built per row rather than once, because which transitions are offered
   * depends on the survey's status.
   */
  protected contextMenuItems: MenuItem[] = [];

  /** Highlights the row the context menu belongs to; separate from the tick-box selection. */
  protected contextSurvey: SurveyListItemDto | null = null;

  /**
   * Rows ticked for a bulk allocation. Held here rather than on the table so the count survives a
   * dialog opening over it; the table's own selection is bound two-way at `selectedSurveys`.
   */
  protected selectedSurveys: SurveyListItemDto[] = [];
  protected readonly selectedCount = signal(0);
  protected readonly selectedSurveyIds = signal<number[]>([]);

  /** The survey whose version migration is in flight; drives the busy state on its control. */
  protected readonly migratingSurveyId = signal<number | null>(null);

  ngOnInit(): void {
    this.buildFilterOptions();
    // The language switch re-labels the status/source pickers.
    this.transloco.langChanges$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.buildFilterOptions());
  }

  protected loadSurveys(event?: TableLazyLoadEvent): void {
    if (event) {
      const rows = event.rows ?? this.pageSize;
      this.page = event.first !== undefined && rows ? Math.floor(event.first / rows) + 1 : 1;
      this.pageSize = rows;
      if (event.sortField) {
        this.sortField = Array.isArray(event.sortField) ? event.sortField[0] : event.sortField;
        this.sortOrder = event.sortOrder ?? 1;
      }
    }

    this.loading.set(true);
    this.surveysClient
      .fsmsSurveys_GetSurveys(
        this.page,
        this.pageSize,
        this.status ?? undefined,
        undefined,
        this.source ?? undefined,
        this.orgFilter.clusterCode ?? undefined,
        this.orgFilter.cbuCode ?? undefined,
        this.orgFilter.branchCode ?? undefined,
        this.orgFilter.operationAreaCode ?? undefined,
        // departmentId, faTypeCode, isExternalTask, templateId, fieldTeamId — not filtered from here yet.
        undefined,
        undefined,
        undefined,
        undefined,
        undefined,
        this.returnReasonCode ?? undefined,
        this.createdFrom ?? undefined,
        this.createdTo ?? undefined,
        this.search.trim() || undefined,
      )
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          const rawItems = res.data?.items ?? [];
          const sortedItems = this.applySorting(rawItems);
          this.surveys.set(sortedItems);
          this.totalRecords.set(res.data?.totalCount ?? 0);
          // Selection is per page: the rows it pointed at are gone, and a tick the operator can no
          // longer see is a tick they cannot take back.
          this.clearSelection();
        },
        error: (error: unknown) => {
          this.surveys.set([]);
          this.totalRecords.set(0);
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: surveyErrorMessage(
              error,
              this.transloco.translate('surveys.messages.loadFailed'),
            ),
          });
        },
      });
  }

  protected applyFilters(): void {
    this.page = 1;
    this.loadSurveys();
  }

  /** The dialog stages the cascade and hands over the finished location once. */
  protected onOrgFilterApplied(location: OrgLocation): void {
    this.orgFilter = location;
    this.applyFilters();
  }

  protected clearFilters(): void {
    this.search = '';
    this.status = null;
    this.source = null;
    this.returnReasonCode = null;
    this.createdFrom = null;
    this.createdTo = null;
    this.sortField = null;
    this.sortOrder = 1;
    // The dialog re-seeds its own picker from this on each open, so clearing the
    // filter here is enough — there is no open cascade left to reset.
    this.orgFilter = { ...EMPTY_ORG_LOCATION };
    this.applyFilters();
  }

  protected get hasFilters(): boolean {
    return !!(
      this.search ||
      this.status ||
      this.source ||
      this.returnReasonCode ||
      this.createdFrom ||
      this.createdTo ||
      this.sortField ||
      this.orgFilter.clusterCode ||
      this.orgFilter.cbuCode ||
      this.orgFilter.branchCode ||
      this.orgFilter.operationAreaCode
    );
  }

  private applySorting(items: SurveyListItemDto[]): SurveyListItemDto[] {
    if (!this.sortField) {
      return items;
    }
    const field = this.sortField;
    const order = this.sortOrder;
    return [...items].sort((a, b) => {
      const valA = this.getFieldValue(a, field);
      const valB = this.getFieldValue(b, field);
      if (valA == null && valB == null) return 0;
      if (valA == null) return 1 * order;
      if (valB == null) return -1 * order;
      if (typeof valA === 'string' && typeof valB === 'string') {
        return valA.localeCompare(valB, undefined, { numeric: true, sensitivity: 'base' }) * order;
      }
      return (
        ((valA as number) < (valB as number) ? -1 : (valA as number) > (valB as number) ? 1 : 0) *
        order
      );
    });
  }

  private getFieldValue(item: SurveyListItemDto, field: string): unknown {
    switch (field) {
      case 'surveyCode':
        return item.surveyCode ?? '';
      case 'templateCode':
        return item.templateCode ?? '';
      case 'status':
        return item.status ?? '';
      case 'source':
        return item.source ?? '';
      case 'branchCode':
        return this.branchLabel(item);
      case 'departmentName':
        return this.departmentLabel(item);
      case 'allocatedFieldTeamName':
        return item.allocatedFieldTeamName ?? '';
      case 'faId':
        return item.faId ?? '';
      case 'customerName':
        return item.customerName ?? '';
      case 'isExternalTask':
        return item.isExternalTask ? 1 : 0;
      case 'created':
        return item.created ? new Date(item.created).getTime() : 0;
      case 'dueDate':
        return item.dueDate ? new Date(item.dueDate).getTime() : 0;
      case 'submissionCount':
        return item.submissionCount ?? 0;
      default:
        return (item as Record<string, unknown>)[field] ?? '';
    }
  }

  /** Branch as `code — name`, falling back to whichever side is available. */
  protected branchLabel(survey: SurveyListItemDto): string {
    const name = this.localizedName(survey.branchNameEn, survey.branchNameAr);
    if (survey.branchCode && name) {
      return `${survey.branchCode} — ${name}`;
    }

    return survey.branchCode || name || '-';
  }

  protected departmentLabel(survey: SurveyListItemDto): string {
    return this.localizedName(survey.departmentNameEn, survey.departmentNameAr) || '-';
  }

  private localizedName(nameEn?: string, nameAr?: string): string {
    const preferred = this.language.current() === 'ar' ? nameAr : nameEn;
    return preferred?.trim() || nameEn?.trim() || nameAr?.trim() || '';
  }

  protected openCreate(): void {
    this.createVisible.set(true);
  }

  protected openDetail(survey: SurveyListItemDto): void {
    this.selectedSurveyId.set(survey.surveyId ?? null);
    this.detailVisible.set(true);
  }

  protected openAllocate(survey: SurveyListItemDto): void {
    this.selectedSurvey.set(survey);
    this.allocateVisible.set(true);
  }

  /**
   * Whether the row may be ticked for a bulk allocation. The same rule the single allocate action
   * uses, so a row the batch could never move is never offered to it.
   */
  protected isSelectable(survey: SurveyListItemDto): boolean {
    return canReallocate(survey.status, survey.submissionCount);
  }

  /**
   * Bound at the table so its "select all" checkbox obeys the same rule as the row checkboxes —
   * otherwise one click on the header would tick rows the batch has to throw away again.
   */
  protected readonly isRowSelectable = (event: {
    data: SurveyListItemDto;
    index: number;
  }): boolean => this.isSelectable(event.data);

  /** The table reports the whole selection on every tick; mirror it into the signals. */
  protected onSelectionChange(selection: SurveyListItemDto[]): void {
    this.selectedSurveys = selection;
    this.selectedCount.set(selection.length);
    this.selectedSurveyIds.set(
      selection.map((survey) => survey.surveyId).filter((id): id is number => id != null),
    );
  }

  protected clearSelection(): void {
    this.onSelectionChange([]);
  }

  protected openBulkAllocate(): void {
    if (this.selectedSurveyIds().length === 0) {
      return;
    }

    this.bulkAllocateVisible.set(true);
  }

  protected openEdit(survey: SurveyListItemDto): void {
    this.selectedSurvey.set(survey);
    this.editVisible.set(true);
  }

  /**
   * Relocating is only offered where the API will allow it — before review and before any fill.
   * Moving a survey after data has been collected would hand those answers to an audience that
   * never had access to the work they describe, so the same rule guards the menu and the entity.
   */
  protected canEdit(survey: SurveyListItemDto): boolean {
    return canReallocate(survey.status, survey.submissionCount);
  }

  /** Opens the survey's own template form for the back office to fill. */
  protected openFill(survey: SurveyListItemDto): void {
    this.selectedSurveyId.set(survey.surveyId ?? null);
    this.fillVisible.set(true);
  }

  /**
   * Whether the survey sits on an older version of its template than the one currently published.
   * A survey is pinned at creation so a republish never changes the form under a crew mid-job;
   * moving it forward is a deliberate act, and only worth offering when there is somewhere to move.
   */
  protected isBehindTemplateVersion(survey: SurveyListItemDto): boolean {
    const current = survey.templateCurrentVersionNo;
    const pinned = survey.templateVersionNo;
    return (
      current != null && pinned != null && current > pinned && canMigrateVersion(survey.status)
    );
  }

  /** Moves the survey onto its template's newest published version. */
  protected migrateVersion(survey: SurveyListItemDto): void {
    const surveyId = survey.surveyId;
    if (surveyId == null || this.migratingSurveyId() !== null) {
      return;
    }

    this.migratingSurveyId.set(surveyId);
    this.surveysClient
      .fsmsSurveys_MigrateVersion(surveyId, { surveyId })
      .pipe(finalize(() => this.migratingSurveyId.set(null)))
      .subscribe({
        next: (res) => {
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('common.success'),
            detail: this.transloco.translate('surveys.migrateVersion.success', {
              version: res.data?.templateVersionNo ?? '',
            }),
          });
          this.loadSurveys();
        },
        error: (error: unknown) => {
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: surveyErrorMessage(
              error,
              this.transloco.translate('surveys.migrateVersion.failed'),
            ),
          });
        },
      });
  }

  protected exportToPdf(survey: SurveyListItemDto): void {
    const surveyId = survey.surveyId;
    if (surveyId == null) {
      return;
    }

    // We could use a loading state here per row or globally, but since the menu closes
    // we'll just fire the request. For better UX, a global toast might indicate starting.
    const lang = this.language.current();
    this.http
      .get(`${this.baseUrl}/api/v1/fsms/surveys/${surveyId}/export-pdf?language=${lang}`, {
        responseType: 'blob',
        observe: 'response',
      })
      .subscribe({
        next: (response) => {
          const contentDisposition = response.headers.get('content-disposition') || '';
          // Stop at the `;` — the header also carries a `filename*=UTF-8''…` copy, and swallowing it
          // makes the saved file name (and any later re-upload) carry the whole header fragment.
          const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(contentDisposition);
          const fileName = match
            ? decodeURIComponent(match[1].trim())
            : `Survey_${surveyId}_${new Date().toISOString().split('T')[0]}.pdf`;

          const blob = new Blob([response.body as Blob], { type: 'application/pdf' });
          const url = window.URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = fileName;
          document.body.appendChild(a);
          a.click();
          document.body.removeChild(a);
          window.URL.revokeObjectURL(url);
        },
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: this.transloco.translate('surveys.messages.exportPdfFailed'),
          });
        },
      });
  }

  protected openAction(survey: SurveyListItemDto, action: SurveyNoteAction): void {
    this.selectedSurvey.set(survey);
    this.currentAction.set(action);
    this.actionVisible.set(true);
  }

  /** A return carries a reason code and an optional hand-over, so it has its own dialog. */
  protected openReturn(survey: SurveyListItemDto): void {
    this.selectedSurvey.set(survey);
    this.returnVisible.set(true);
  }

  /** Only transitions the domain would accept are offered; the server still guards them. */
  protected buildMenu(survey: SurveyListItemDto): MenuItem[] {
    return [
      {
        label: this.transloco.translate('surveys.actions.viewDetail'),
        icon: 'pi pi-eye',
        command: () => this.openDetail(survey),
      },
      {
        label: this.transloco.translate('surveys.actions.exportPdf'),
        icon: 'pi pi-file-pdf',
        command: () => this.exportToPdf(survey),
      },
      {
        label: this.transloco.translate('surveys.editSurvey'),
        icon: 'pi pi-map-marker',
        disabled: !this.canEdit(survey),
        command: () => this.openEdit(survey),
      },
      {
        // A survey already out with a crew is being moved, not handed out — say which one this is.
        label: this.transloco.translate(
          survey.allocatedFieldTeamId
            ? 'surveys.actions.allocateAgain'
            : 'surveys.actions.allocate',
        ),
        icon: 'pi pi-send',
        disabled: !canReallocate(survey.status, survey.submissionCount),
        command: () => this.openAllocate(survey),
      },
      {
        label: this.transloco.translate('surveys.actions.fill'),
        icon: 'pi pi-pencil',
        disabled: !canRunSurveyAction('fill', survey.status),
        command: () => this.openFill(survey),
      },
      {
        label: this.transloco.translate('surveys.actions.migrateVersion'),
        icon: 'pi pi-arrow-up-right',
        disabled: !this.isBehindTemplateVersion(survey) || this.migratingSurveyId() !== null,
        command: () => this.migrateVersion(survey),
      },
      {
        label: this.transloco.translate('surveys.actions.complete'),
        icon: 'pi pi-check-circle',
        disabled: !canRunSurveyAction('complete', survey.status),
        command: () => this.openAction(survey, 'complete'),
      },
      {
        label: this.transloco.translate('surveys.actions.return'),
        icon: 'pi pi-replay',
        disabled: !canRunSurveyAction('return', survey.status),
        command: () => this.openReturn(survey),
      },
      {
        label: this.transloco.translate('surveys.actions.expire'),
        icon: 'pi pi-ban',
        disabled: !canRunSurveyAction('expire', survey.status),
        command: () => this.openAction(survey, 'expire'),
      },
    ];
  }

  protected toggleMenu(
    event: Event,
    survey: SurveyListItemDto,
    menu: { toggle: (e: Event) => void },
  ): void {
    this.menuItems = this.buildMenu(survey);
    menu.toggle(event);
  }

  /**
   * The table raises this before it opens the context menu, so assigning the
   * model here is what the menu renders.
   */
  protected onContextMenu(event: { data: SurveyListItemDto }): void {
    this.contextMenuItems = this.buildMenu(event.data);
  }

  private buildFilterOptions(): void {
    this.statusOptions.set(
      SURVEY_STATUSES.map((value) => ({
        label: this.transloco.translate(`surveys.status.${value}`),
        value,
      })),
    );
    this.sourceOptions.set(
      SURVEY_SOURCES.map((value) => ({
        label: this.transloco.translate(`surveys.source.${value}`),
        value,
      })),
    );
    this.returnReasonOptions.set(
      SURVEY_RETURN_REASONS.map((value) => ({
        label: this.transloco.translate(`surveys.returnReasons.${value}`),
        value,
      })),
    );
  }
}
