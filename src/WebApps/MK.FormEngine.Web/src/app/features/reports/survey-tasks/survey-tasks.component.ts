import { Component, DestroyRef, OnInit, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe, DecimalPipe } from '@angular/common';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { Observable, finalize, forkJoin } from 'rxjs';

import { MessageService } from 'primeng/api';
import { SkeletonModule } from 'primeng/skeleton';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { Tag } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { UIChart } from 'primeng/chart';

import { apiErrorMessage } from '../../../core/api/api-error';
import {
  FsmsBranchDto,
  FsmsOperationAreaDto,
  FsmsReportsClient,
  SurveyTaskReportRowDto,
  SurveyTasksReportSummaryDto,
  TeamDto,
} from '../../../core/api/api-client.generated';
import { FsmsLookupService } from '../../../core/lookups/fsms-lookup.service';
import { LanguageService } from '../../../core/i18n/language.service';
import { ThemeService } from '../../../core/theme/theme.service';
import { EMPTY_REPORT_FILTER, ReportFilterValue, ReportSelectOption } from '../models/report-filter.model';
import { SURVEY_SOURCES, SURVEY_STATUSES, surveyStatusSeverity } from '../../surveys/survey-status';
import { ReportFilterBarComponent } from '../shared/report-filter-bar/report-filter-bar.component';
import { ExportFormat, ReportExportActionsComponent } from '../shared/report-export-actions/report-export-actions.component';
import { ReportExportService } from '../shared/report-export.service';
import { localizedName } from '../shared/report-format.util';
import { FALLBACK_SURVEY_STATUS_COLOR, SURVEY_STATUS_COLORS } from '../shared/survey-status-colors';

const OVERDUE_LATENESS_KIND = 'OVERDUE';
const DEFAULT_SORT_FIELD = 'Created';
const DEFAULT_PAGE_SIZE = 10;

@Component({
  selector: 'app-survey-tasks-report',
  providers: [MessageService],
  imports: [
    DatePipe,
    DecimalPipe,
    TranslocoModule,
    SkeletonModule,
    TableModule,
    Tag,
    ToastModule,
    UIChart,
    ReportFilterBarComponent,
    ReportExportActionsComponent,
  ],
  templateUrl: './survey-tasks.component.html',
  styleUrl: './survey-tasks.component.scss',
})
export class SurveyTasksComponent implements OnInit {
  private readonly reportsClient = inject(FsmsReportsClient);
  private readonly lookups = inject(FsmsLookupService);
  private readonly reportExport = inject(ReportExportService);
  private readonly messageService = inject(MessageService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly language = inject(LanguageService);
  private readonly transloco = inject(TranslocoService);
  private readonly theme = inject(ThemeService);

  protected readonly summaryLoading = signal(false);
  protected readonly tableLoading = signal(false);
  protected readonly loading = computed(() => this.summaryLoading() || this.tableLoading());

  protected readonly summary = signal<SurveyTasksReportSummaryDto | null>(null);
  protected readonly tasks = signal<SurveyTaskReportRowDto[]>([]);
  protected readonly totalRecords = signal(0);
  protected readonly filter = signal<ReportFilterValue>(EMPTY_REPORT_FILTER);

  private page = 1;
  private pageSize = DEFAULT_PAGE_SIZE;
  private sortField = DEFAULT_SORT_FIELD;
  private sortDescending = true;

  private readonly teams = signal<TeamDto[]>([]);
  private readonly branches = signal<FsmsBranchDto[]>([]);
  private readonly operationAreas = signal<FsmsOperationAreaDto[]>([]);

  protected readonly teamOptions = computed<ReportSelectOption<number>[]>(() =>
    this.teams().map((t) => ({ label: t.userCode ? `${t.name} (${t.userCode})` : (t.name ?? ''), value: t.teamId ?? 0 })),
  );

  protected readonly branchOptions = computed<ReportSelectOption<string>[]>(() => {
    const lang = this.language.current();
    return this.branches().map((b) => ({ label: `${localizedName(b.nameEn, b.nameAr, lang)} (${b.code})`, value: b.code ?? '' }));
  });

  protected readonly operationAreaOptions = computed<ReportSelectOption<string>[]>(() => {
    const lang = this.language.current();
    return this.operationAreas().map((a) => ({ label: localizedName(a.nameEn, a.nameAr, lang), value: a.code ?? '' }));
  });

  protected readonly statusOptions = computed<ReportSelectOption<string>[]>(() => {
    this.language.current();
    return SURVEY_STATUSES.map((status) => ({ label: this.transloco.translate(`surveys.status.${status}`), value: status }));
  });

  protected readonly sourceOptions = computed<ReportSelectOption<string>[]>(() => {
    this.language.current();
    return SURVEY_SOURCES.map((source) => ({ label: this.transloco.translate(`surveys.source.${source}`), value: source }));
  });

  protected readonly activeFilterCount = computed(() => {
    const f = this.filter();
    return [f.fromDate, f.toDate, f.status, f.teamId, f.branchCode, f.operationAreaCode, f.source].filter(Boolean).length;
  });

  protected readonly summaryCards = computed(() => {
    const k = this.summary()?.kpis;
    return [
      { key: 'total', labelKey: 'reports.kpi.totalTasks', value: k?.totalTasks ?? 0, icon: 'pi pi-list-check', accent: '#145e77' },
      { key: 'pending', labelKey: 'reports.kpi.pendingTasks', value: k?.pendingTasks ?? 0, icon: 'pi pi-clock', accent: '#64748b' },
      { key: 'inProgress', labelKey: 'reports.kpi.inProgressTasks', value: k?.inProgressTasks ?? 0, icon: 'pi pi-spinner', accent: '#3b82f6' },
      { key: 'submitted', labelKey: 'reports.kpi.submittedTasks', value: k?.submittedTasks ?? 0, icon: 'pi pi-file-edit', accent: '#8b5cf6' },
      { key: 'approved', labelKey: 'reports.kpi.approvedTasks', value: k?.approvedTasks ?? 0, icon: 'pi pi-check-circle', accent: '#10b981' },
      { key: 'returned', labelKey: 'reports.kpi.returnedTasks', value: k?.returnedTasks ?? 0, icon: 'pi pi-replay', accent: '#ef4444' },
      { key: 'overdue', labelKey: 'dashboard.kpiOverdueSurveys', value: k?.overdueTasks ?? 0, icon: 'pi pi-exclamation-triangle', accent: '#dc2626' },
      { key: 'completionRate', labelKey: 'reports.kpi.completionRate', value: k?.completionRatePercent ?? 0, icon: 'pi pi-percentage', accent: '#f97316', isPercent: true },
    ];
  });

  /** Passed straight through — still-open overdue work first, then work filled late, as the API
   *  already orders it. */
  protected readonly lateTasks = computed(() => this.summary()?.lateTasks ?? []);

  protected readonly statusDoughnutData = computed(() => {
    this.language.current();
    const slices = this.summary()?.statusDistribution ?? [];
    return {
      labels: slices.map((s) => this.transloco.translate(`surveys.status.${s.status}`)),
      datasets: [
        {
          data: slices.map((s) => s.count ?? 0),
          backgroundColor: slices.map((s) => SURVEY_STATUS_COLORS[s.status ?? ''] ?? FALLBACK_SURVEY_STATUS_COLOR),
          hoverOffset: 6,
        },
      ],
    };
  });

  protected readonly byTeamData = computed(() => {
    const rows = this.summary()?.tasksByTeam ?? [];
    return {
      labels: rows.map((r) => r.teamName || '—'),
      datasets: [{ label: this.transloco.translate('reports.kpi.totalTasks'), data: rows.map((r) => r.count ?? 0), backgroundColor: '#157591', borderRadius: 4 }],
    };
  });

  protected readonly byBranchData = computed(() => {
    const rows = this.summary()?.tasksByBranch ?? [];
    return {
      labels: rows.map((r) => r.branchName || r.branchCode || '—'),
      datasets: [{ label: this.transloco.translate('reports.kpi.totalTasks'), data: rows.map((r) => r.count ?? 0), backgroundColor: '#45adc7', borderRadius: 4 }],
    };
  });

  protected readonly trendData = computed(() => {
    const points = this.summary()?.dailyTrend ?? [];
    return {
      labels: points.map((p) => formatShortDate(p.date)),
      datasets: [
        {
          label: this.transloco.translate('reports.charts.tasksOverTime'),
          data: points.map((p) => p.createdCount ?? 0),
          borderColor: '#157591',
          backgroundColor: 'rgba(21, 117, 145, 0.14)',
          fill: true,
          tension: 0.35,
          borderWidth: 2,
          pointRadius: 0,
        },
      ],
    };
  });

  protected readonly doughnutChartOptions = signal<Record<string, unknown>>({});
  protected readonly horizontalBarOptions = signal<Record<string, unknown>>({});
  protected readonly verticalBarOptions = signal<Record<string, unknown>>({});
  protected readonly lineChartOptions = signal<Record<string, unknown>>({});

  protected readonly exportFn = (format: ExportFormat): Observable<unknown> => {
    const f = this.filter();
    return this.reportExport.download(
      `/api/v1/fsms/reports/survey-tasks/export/${format}`,
      {
        FromDate: f.fromDate?.toISOString(),
        ToDate: f.toDate?.toISOString(),
        Status: f.status ?? undefined,
        TeamId: f.teamId != null ? String(f.teamId) : undefined,
        BranchCode: f.branchCode ?? undefined,
        OperationAreaCode: f.operationAreaCode ?? undefined,
        Source: f.source ?? undefined,
      },
      `SurveyTasks.${format === 'excel' ? 'xlsx' : 'pdf'}`,
    );
  };

  constructor() {
    effect(() => {
      this.theme.mode();
      this.doughnutChartOptions.set(buildDoughnutOptions());
      this.horizontalBarOptions.set(buildBarOptions(true));
      this.verticalBarOptions.set(buildBarOptions(false));
      this.lineChartOptions.set(buildLineOptions());
    });
  }

  ngOnInit(): void {
    forkJoin({
      teams: this.lookups.getTeams(),
      branches: this.lookups.getBranches(),
      operationAreas: this.lookups.getOperationAreas(),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ teams, branches, operationAreas }) => {
        this.teams.set(teams);
        this.branches.set(branches);
        this.operationAreas.set(operationAreas);
      });

    // Only the summary needs an explicit first call — the detail table has `[lazy]="true"` with
    // `lazyLoadOnInit` on by default, so PrimeNG fires its own initial `onLazyLoad` once the view is
    // ready, using whatever `this.filter()` already holds (set synchronously below).
    this.loadSummary(EMPTY_REPORT_FILTER);
  }

  protected onApply(value: ReportFilterValue): void {
    this.loadSummary(value);
    this.loadDetails();
  }

  protected onReset(): void {
    this.loadSummary(EMPTY_REPORT_FILTER);
    this.loadDetails();
  }

  protected templateDisplay(row: SurveyTaskReportRowDto): string {
    const name = localizedName(row.templateNameEn, row.templateNameAr, this.language.current());
    return `${name} v${row.templateVersionNo}`;
  }

  protected branchDisplay(row: SurveyTaskReportRowDto): string {
    return localizedName(row.branchNameEn, row.branchNameAr, this.language.current()) || row.branchCode || '—';
  }

  protected statusKey(status?: string): string {
    return `surveys.status.${status}`;
  }

  protected sourceKey(source?: string): string {
    return `surveys.source.${source}`;
  }

  protected statusSeverity(status?: string) {
    return surveyStatusSeverity(status);
  }

  protected latenessKey(kind?: string): string {
    return `dashboard.lateness.${kind}`;
  }

  protected latenessSeverity(kind?: string): 'danger' | 'warn' {
    return kind === OVERDUE_LATENESS_KIND ? 'danger' : 'warn';
  }

  /** Server-paginated detail table — same `onLazyLoad` pattern as `SurveyListComponent.loadSurveys`. */
  protected loadDetails(event?: TableLazyLoadEvent): void {
    if (event) {
      const rows = event.rows ?? this.pageSize;
      this.page = event.first !== undefined && rows ? Math.floor(event.first / rows) + 1 : 1;
      this.pageSize = rows;
      if (event.sortField) {
        this.sortField = (Array.isArray(event.sortField) ? event.sortField[0] : event.sortField) ?? DEFAULT_SORT_FIELD;
        this.sortDescending = (event.sortOrder ?? -1) < 0;
      }
    }

    const f = this.filter();
    this.tableLoading.set(true);
    this.reportsClient
      .fsmsReports_GetSurveyTasksDetails(
        f.fromDate ?? undefined,
        f.toDate ?? undefined,
        f.status ?? undefined,
        f.teamId ?? undefined,
        f.branchCode ?? undefined,
        f.operationAreaCode ?? undefined,
        f.source ?? undefined,
        this.page,
        this.pageSize,
        this.sortField,
        this.sortDescending,
      )
      .pipe(finalize(() => this.tableLoading.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.tasks.set(res.data?.items ?? []);
          this.totalRecords.set(res.data?.totalCount ?? 0);
        },
        error: (error: unknown) => {
          this.tasks.set([]);
          this.totalRecords.set(0);
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: apiErrorMessage(error, this.transloco.translate('reports.messages.loadFailed')),
          });
        },
      });
  }

  private loadSummary(filter: ReportFilterValue): void {
    if (this.summaryLoading()) {
      return;
    }

    this.filter.set(filter);
    this.page = 1;

    this.summaryLoading.set(true);
    this.reportsClient
      .fsmsReports_GetSurveyTasksSummary(
        filter.fromDate ?? undefined,
        filter.toDate ?? undefined,
        filter.status ?? undefined,
        filter.teamId ?? undefined,
        filter.branchCode ?? undefined,
        filter.operationAreaCode ?? undefined,
        filter.source ?? undefined,
      )
      .pipe(finalize(() => this.summaryLoading.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => this.summary.set(res.data ?? null),
        error: (error: unknown) => {
          this.summary.set(null);
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: apiErrorMessage(error, this.transloco.translate('reports.messages.loadFailed')),
          });
        },
      });
  }
}

function formatShortDate(date: Date | string | undefined): string {
  if (!date) {
    return '';
  }
  const d = new Date(date);
  return `${d.getMonth() + 1}/${d.getDate()}`;
}

function readChartColors(): { text: string; muted: string; border: string } {
  const styles = getComputedStyle(document.documentElement);
  return {
    text: styles.getPropertyValue('--p-text-color').trim() || '#334155',
    muted: styles.getPropertyValue('--p-text-muted-color').trim() || '#64748b',
    border: styles.getPropertyValue('--app-border').trim() || '#e2e8f0',
  };
}

function buildDoughnutOptions(): Record<string, unknown> {
  const { text } = readChartColors();
  return {
    maintainAspectRatio: false,
    plugins: { legend: { position: 'right', labels: { color: text, usePointStyle: true, boxWidth: 10, padding: 12 } } },
  };
}

function buildBarOptions(horizontal: boolean): Record<string, unknown> {
  const { muted, border } = readChartColors();
  return {
    maintainAspectRatio: false,
    indexAxis: horizontal ? 'y' : 'x',
    plugins: { legend: { display: false } },
    scales: {
      x: { ticks: { color: muted }, grid: { color: horizontal ? border : 'transparent' }, beginAtZero: true },
      y: { ticks: { color: muted }, grid: { color: horizontal ? 'transparent' : border }, beginAtZero: true },
    },
  };
}

function buildLineOptions(): Record<string, unknown> {
  const { muted, border } = readChartColors();
  return {
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: {
      x: { ticks: { color: muted, maxTicksLimit: 10 }, grid: { color: 'transparent' } },
      y: { ticks: { color: muted }, grid: { color: border }, beginAtZero: true },
    },
  };
}
