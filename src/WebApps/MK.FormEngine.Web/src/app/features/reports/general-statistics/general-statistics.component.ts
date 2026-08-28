import { Component, DestroyRef, OnInit, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe, DecimalPipe } from '@angular/common';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { Observable, finalize, forkJoin } from 'rxjs';

import { MessageService } from 'primeng/api';
import { SkeletonModule } from 'primeng/skeleton';
import { TableModule } from 'primeng/table';
import { Tag } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { UIChart } from 'primeng/chart';

import { apiErrorMessage } from '../../../core/api/api-error';
import {
  FsmsBranchDto,
  FsmsOperationAreaDto,
  FsmsReportsClient,
  GeneralStatisticsReportDto,
} from '../../../core/api/api-client.generated';
import { FsmsLookupService } from '../../../core/lookups/fsms-lookup.service';
import { LanguageService } from '../../../core/i18n/language.service';
import { ThemeService } from '../../../core/theme/theme.service';
import { ReportFilterValue, EMPTY_REPORT_FILTER, ReportSelectOption } from '../models/report-filter.model';
import { SURVEY_STATUSES, SurveyStatus, surveyStatusSeverity } from '../../surveys/survey-status';
import { ReportFilterBarComponent } from '../shared/report-filter-bar/report-filter-bar.component';
import { ExportFormat, ReportExportActionsComponent } from '../shared/report-export-actions/report-export-actions.component';
import { ReportExportService } from '../shared/report-export.service';
import { localizedName } from '../shared/report-format.util';
import { FALLBACK_SURVEY_STATUS_COLOR, SURVEY_STATUS_COLORS } from '../shared/survey-status-colors';

interface KpiCard {
  readonly key: string;
  readonly labelKey: string;
  readonly value: number;
  readonly icon: string;
  readonly accent: string;
  readonly isPercent?: boolean;
  readonly isHours?: boolean;
  readonly deltaPercent?: number;
  /** A drop reads as an improvement (e.g. Overdue, Return rate) — the delta arrow flips accordingly. */
  readonly inverse?: boolean;
}

const OVERDUE_LATENESS_KIND = 'OVERDUE';

@Component({
  selector: 'app-general-statistics',
  providers: [MessageService],
  imports: [
    DatePipe,
    DecimalPipe,
    TranslocoModule,
    SkeletonModule,
    TableModule,
    Tag,
    ToastModule,
    TooltipModule,
    UIChart,
    ReportFilterBarComponent,
    ReportExportActionsComponent,
  ],
  templateUrl: './general-statistics.component.html',
  styleUrl: './general-statistics.component.scss',
})
export class GeneralStatisticsComponent implements OnInit {
  private readonly reportsClient = inject(FsmsReportsClient);
  private readonly lookups = inject(FsmsLookupService);
  private readonly reportExport = inject(ReportExportService);
  private readonly messageService = inject(MessageService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly language = inject(LanguageService);
  private readonly transloco = inject(TranslocoService);
  private readonly theme = inject(ThemeService);

  protected readonly loading = signal(false);
  protected readonly report = signal<GeneralStatisticsReportDto | null>(null);
  protected readonly filter = signal<ReportFilterValue>(EMPTY_REPORT_FILTER);

  private readonly branches = signal<FsmsBranchDto[]>([]);
  private readonly operationAreas = signal<FsmsOperationAreaDto[]>([]);

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

  protected readonly activeFilterCount = computed(() => {
    const f = this.filter();
    return [f.fromDate, f.toDate, f.status, f.branchCode, f.operationAreaCode].filter(Boolean).length;
  });

  protected readonly kpiCards = computed<KpiCard[]>(() => {
    const k = this.report()?.kpis;
    return [
      { key: 'total', labelKey: 'reports.kpi.totalTasks', value: k?.totalTasks ?? 0, icon: 'pi pi-list-check', accent: '#145e77', deltaPercent: k?.totalTasksDelta },
      { key: 'overdue', labelKey: 'dashboard.kpiOverdueSurveys', value: k?.overdueTasks ?? 0, icon: 'pi pi-exclamation-triangle', accent: '#dc2626', deltaPercent: k?.overdueTasksDelta, inverse: true },
      { key: 'totalTeams', labelKey: 'reports.kpi.totalTeams', value: k?.totalTeams ?? 0, icon: 'pi pi-sitemap', accent: '#2563eb' },
      { key: 'activeTeams', labelKey: 'reports.kpi.activeTeams', value: k?.activeTeams ?? 0, icon: 'pi pi-users', accent: '#0891b2', deltaPercent: k?.activeTeamsDelta },
      { key: 'avgCompletionHours', labelKey: 'reports.kpi.avgCompletionHours', value: k?.avgCompletionHours ?? 0, icon: 'pi pi-hourglass', accent: '#7c3aed', isHours: true },
      { key: 'completionRate', labelKey: 'reports.kpi.completionRate', value: k?.completionRatePercent ?? 0, icon: 'pi pi-percentage', accent: '#f97316', isPercent: true, deltaPercent: k?.completionRateDelta },
      { key: 'returnRate', labelKey: 'reports.kpi.returnRate', value: k?.returnRatePercent ?? 0, icon: 'pi pi-replay', accent: '#ef4444', isPercent: true, deltaPercent: k?.returnRateDelta, inverse: true },
      { key: 'onTimeRate', labelKey: 'reports.kpi.onTimeRate', value: k?.onTimeRatePercent ?? 0, icon: 'pi pi-clock', accent: '#10b981', isPercent: true, deltaPercent: k?.onTimeRateDelta },
    ];
  });

  protected readonly trendData = computed(() => {
    this.language.current();
    const trend = this.report()?.trend ?? [];
    return {
      labels: trend.map((p) => p.bucketKey ?? ''),
      datasets: [
        {
          label: this.transloco.translate('reports.charts.tasksOverTime'),
          data: trend.map((p) => p.createdCount ?? 0),
          borderColor: '#157591',
          backgroundColor: 'rgba(21, 117, 145, 0.14)',
          fill: true,
          tension: 0.4,
          borderWidth: 2,
        },
        {
          label: this.transloco.translate(`surveys.status.${SurveyStatus.Approved}`),
          data: trend.map((p) => p.approvedCount ?? 0),
          borderColor: '#10b981',
          backgroundColor: 'rgba(16, 185, 129, 0.14)',
          fill: true,
          tension: 0.4,
          borderWidth: 2,
        },
      ],
    };
  });

  protected readonly doughnutData = computed(() => {
    this.language.current();
    const slices = [...(this.report()?.statusDistribution ?? [])].sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0));

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

  protected readonly teamOverviewData = computed(() => {
    const rows = this.report()?.teamCompletion ?? [];
    return {
      labels: rows.map((r) => r.teamName ?? '—'),
      datasets: [
        {
          label: this.transloco.translate('reports.kpi.completionRate'),
          data: rows.map((r) => r.completionPercent ?? 0),
          backgroundColor: '#248fac',
          borderRadius: 4,
        },
      ],
    };
  });

  protected readonly recentActivity = computed(() => (this.report()?.recentActivity ?? []).slice(0, 6));

  /** Row count shown on the export buttons — the report's total task count, not the trimmed activity list. */
  protected readonly kpiTotalTasks = computed(() => this.report()?.kpis?.totalTasks ?? 0);

  /** Passed straight through — still-open overdue work first, then work that finished late, exactly
   *  as the API already orders it. */
  protected readonly lateTasks = computed(() => this.report()?.lateTasks ?? []);

  protected readonly lateTeams = computed(() => this.report()?.lateTeams ?? []);

  protected readonly lineChartOptions = signal<Record<string, unknown>>({});
  protected readonly doughnutChartOptions = signal<Record<string, unknown>>({});
  protected readonly barChartOptions = signal<Record<string, unknown>>({});

  protected readonly exportFn = (format: ExportFormat): Observable<unknown> => {
    const f = this.filter();
    return this.reportExport.download(
      `/api/v1/fsms/reports/general-statistics/export/${format}`,
      {
        FromDate: f.fromDate?.toISOString(),
        ToDate: f.toDate?.toISOString(),
        BranchCode: f.branchCode ?? undefined,
        OperationAreaCode: f.operationAreaCode ?? undefined,
        Status: f.status ?? undefined,
      },
      `GeneralStatistics.${format === 'excel' ? 'xlsx' : 'pdf'}`,
    );
  };

  constructor() {
    effect(() => {
      this.theme.mode();
      this.lineChartOptions.set(buildLineChartOptions());
      this.doughnutChartOptions.set(buildDoughnutChartOptions());
      this.barChartOptions.set(buildBarChartOptions());
    });
  }

  ngOnInit(): void {
    forkJoin({
      branches: this.lookups.getBranches(),
      operationAreas: this.lookups.getOperationAreas(),
    })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(({ branches, operationAreas }) => {
        this.branches.set(branches);
        this.operationAreas.set(operationAreas);
      });

    this.load(EMPTY_REPORT_FILTER);
  }

  protected onApply(value: ReportFilterValue): void {
    this.load(value);
  }

  protected onReset(): void {
    this.load(EMPTY_REPORT_FILTER);
  }

  protected statusKey(status?: string): string {
    return `surveys.status.${status}`;
  }

  protected statusSeverity(status?: string) {
    return surveyStatusSeverity(status);
  }

  protected latenessKey(kind?: string): string {
    return `dashboard.lateness.${kind}`;
  }

  /** Still-open lateness reads as danger; work that finished late is history, so it reads as a
   *  warning — same convention the dashboard uses for the identical distinction. */
  protected latenessSeverity(kind?: string): 'danger' | 'warn' {
    return kind === OVERDUE_LATENESS_KIND ? 'danger' : 'warn';
  }

  protected activityIconClass(status?: string): string {
    switch (status) {
      case SurveyStatus.Approved:
        return 'success';
      case SurveyStatus.Returned:
        return 'danger';
      case SurveyStatus.InProgress:
      case SurveyStatus.Assigned:
        return 'info';
      default:
        return 'warn';
    }
  }

  protected activityIcon(status?: string): string {
    switch (status) {
      case SurveyStatus.Approved:
        return 'pi pi-check-circle';
      case SurveyStatus.Returned:
        return 'pi pi-replay';
      case SurveyStatus.InProgress:
      case SurveyStatus.Assigned:
        return 'pi pi-spinner';
      case SurveyStatus.Expired:
        return 'pi pi-ban';
      default:
        return 'pi pi-clock';
    }
  }

  protected templateDisplay(nameEn?: string, nameAr?: string): string {
    return localizedName(nameEn, nameAr, this.language.current());
  }

  private load(filter: ReportFilterValue): void {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.reportsClient
      .fsmsReports_GetGeneralStatistics(
        filter.fromDate ?? undefined,
        filter.toDate ?? undefined,
        undefined,
        undefined,
        filter.branchCode ?? undefined,
        filter.operationAreaCode ?? undefined,
        filter.status ?? undefined,
        undefined,
      )
      .pipe(finalize(() => this.loading.set(false)), takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (res) => {
          this.report.set(res.data ?? null);
          this.filter.set(filter);
        },
        error: (error: unknown) => {
          this.report.set(null);
          this.filter.set(filter);
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: apiErrorMessage(error, this.transloco.translate('reports.messages.loadFailed')),
          });
        },
      });
  }
}

function readChartColors(): { text: string; muted: string; border: string } {
  const styles = getComputedStyle(document.documentElement);
  return {
    text: styles.getPropertyValue('--p-text-color').trim() || '#334155',
    muted: styles.getPropertyValue('--p-text-muted-color').trim() || '#64748b',
    border: styles.getPropertyValue('--app-border').trim() || '#e2e8f0',
  };
}

function buildLineChartOptions(): Record<string, unknown> {
  const { text, muted, border } = readChartColors();
  return {
    maintainAspectRatio: false,
    plugins: { legend: { labels: { color: text, usePointStyle: true } } },
    scales: {
      x: { ticks: { color: muted }, grid: { color: 'transparent' } },
      y: { ticks: { color: muted }, grid: { color: border }, beginAtZero: true },
    },
  };
}

function buildDoughnutChartOptions(): Record<string, unknown> {
  const { text } = readChartColors();
  return {
    maintainAspectRatio: false,
    plugins: {
      legend: { position: 'right', labels: { color: text, usePointStyle: true, boxWidth: 10, padding: 12 } },
    },
  };
}

function buildBarChartOptions(): Record<string, unknown> {
  const { muted, border } = readChartColors();
  return {
    maintainAspectRatio: false,
    indexAxis: 'y',
    plugins: { legend: { display: false } },
    scales: {
      x: { ticks: { color: muted }, grid: { color: border }, beginAtZero: true, max: 100 },
      y: { ticks: { color: muted }, grid: { color: 'transparent' } },
    },
  };
}
