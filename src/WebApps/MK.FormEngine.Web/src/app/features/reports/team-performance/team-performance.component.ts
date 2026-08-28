import { Component, DestroyRef, OnInit, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DecimalPipe } from '@angular/common';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { Observable, finalize, forkJoin } from 'rxjs';

import { MessageService } from 'primeng/api';
import { SkeletonModule } from 'primeng/skeleton';
import { TableModule } from 'primeng/table';
import { Tag } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { UIChart } from 'primeng/chart';

import { apiErrorMessage } from '../../../core/api/api-error';
import {
  FsmsBranchDto,
  FsmsOperationAreaDto,
  FsmsReportsClient,
  TeamDto,
  TeamPerformanceReportDto,
} from '../../../core/api/api-client.generated';
import { FsmsLookupService } from '../../../core/lookups/fsms-lookup.service';
import { LanguageService } from '../../../core/i18n/language.service';
import { ThemeService } from '../../../core/theme/theme.service';
import { EMPTY_REPORT_FILTER, ReportFilterValue, ReportSelectOption } from '../models/report-filter.model';
import { SURVEY_STATUSES } from '../../surveys/survey-status';
import { ReportFilterBarComponent } from '../shared/report-filter-bar/report-filter-bar.component';
import { ExportFormat, ReportExportActionsComponent } from '../shared/report-export-actions/report-export-actions.component';
import { ReportExportService } from '../shared/report-export.service';
import { localizedName } from '../shared/report-format.util';

const TREND_PALETTE = ['#157591', '#10b981', '#f59e0b', '#7c3aed'];

@Component({
  selector: 'app-team-performance-report',
  providers: [MessageService],
  imports: [
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
  templateUrl: './team-performance.component.html',
  styleUrl: './team-performance.component.scss',
})
export class TeamPerformanceComponent implements OnInit {
  private readonly reportsClient = inject(FsmsReportsClient);
  private readonly lookups = inject(FsmsLookupService);
  private readonly reportExport = inject(ReportExportService);
  private readonly messageService = inject(MessageService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly language = inject(LanguageService);
  private readonly transloco = inject(TranslocoService);
  private readonly theme = inject(ThemeService);

  protected readonly loading = signal(false);
  protected readonly report = signal<TeamPerformanceReportDto | null>(null);
  protected readonly filter = signal<ReportFilterValue>(EMPTY_REPORT_FILTER);

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

  private readonly branchNameByCode = computed(() => {
    const lang = this.language.current();
    return new Map(this.branches().map((b) => [b.code ?? '', localizedName(b.nameEn, b.nameAr, lang)]));
  });

  protected readonly activeFilterCount = computed(() => {
    const f = this.filter();
    return [f.fromDate, f.toDate, f.status, f.teamId, f.branchCode, f.operationAreaCode].filter(Boolean).length;
  });

  /** One row per team — the API already computes every column, so the table just renders it. */
  protected readonly teamRows = computed(() => this.report()?.teams ?? []);

  protected readonly summaryCards = computed(() => {
    const k = this.report()?.kpis;
    return [
      { key: 'assigned', labelKey: 'reports.kpi.assignedTasks', value: k?.assignedTasks ?? 0, icon: 'pi pi-briefcase', accent: '#145e77' },
      { key: 'completed', labelKey: 'reports.kpi.completedTasks', value: k?.completedTasks ?? 0, icon: 'pi pi-check-circle', accent: '#10b981' },
      { key: 'pending', labelKey: 'reports.kpi.pendingTasks', value: k?.pendingTasks ?? 0, icon: 'pi pi-clock', accent: '#f59e0b' },
      { key: 'overdue', labelKey: 'dashboard.kpiOverdueSurveys', value: k?.overdueTasks ?? 0, icon: 'pi pi-exclamation-triangle', accent: '#dc2626' },
      { key: 'completionRate', labelKey: 'reports.kpi.completionRate', value: k?.completionRatePercent ?? 0, icon: 'pi pi-percentage', accent: '#f97316', isPercent: true },
      { key: 'avgCompletion', labelKey: 'reports.kpi.avgCompletionHours', value: k?.avgCompletionHours ?? 0, icon: 'pi pi-hourglass', accent: '#7c3aed', isHours: true },
    ];
  });

  /** Grouped bars: assigned vs completed per team. */
  protected readonly comparisonData = computed(() => {
    const rows = this.teamRows();
    return {
      labels: rows.map((r) => r.teamName || '—'),
      datasets: [
        { label: this.transloco.translate('reports.kpi.assignedTasks'), data: rows.map((r) => r.assigned ?? 0), backgroundColor: '#94a3b8', borderRadius: 4 },
        { label: this.transloco.translate('reports.kpi.completedTasks'), data: rows.map((r) => r.completed ?? 0), backgroundColor: '#157591', borderRadius: 4 },
      ],
    };
  });

  /** Completion % per team, colour-coded green/amber/red by threshold. */
  protected readonly completionByTeamData = computed(() => {
    const rows = this.teamRows();
    return {
      labels: rows.map((r) => r.teamName || '—'),
      datasets: [
        {
          label: this.transloco.translate('reports.kpi.completionRate'),
          data: rows.map((r) => r.completionPercent ?? 0),
          backgroundColor: rows.map((r) => completionColor(r.completionPercent ?? 0)),
          borderRadius: 4,
        },
      ],
    };
  });

  /** Weekly completed-task count, one line per top team by volume — the API already picks the top 4. */
  protected readonly completedOverTimeData = computed(() => {
    const rows = this.report()?.weeklyCompletionTrend ?? [];
    const weekTimes = [...new Set(rows.map((r) => (r.weekStart ? new Date(r.weekStart).getTime() : 0)))].sort((a, b) => a - b);
    const teamIds = [...new Set(rows.map((r) => r.teamId))];

    return {
      labels: weekTimes.map((w) => formatShortDate(new Date(w))),
      datasets: teamIds.map((teamId, index) => {
        const teamRows = rows.filter((r) => r.teamId === teamId);
        return {
          label: teamRows[0]?.teamName ?? '—',
          data: weekTimes.map(
            (w) => teamRows.find((r) => r.weekStart && new Date(r.weekStart).getTime() === w)?.completedCount ?? 0,
          ),
          borderColor: TREND_PALETTE[index % TREND_PALETTE.length],
          backgroundColor: 'transparent',
          tension: 0.35,
          borderWidth: 2,
        };
      }),
    };
  });

  /** Average tasks completed per active team per week, across the whole filtered scope. */
  protected readonly productivityTrendData = computed(() => {
    const rows = this.report()?.productivityTrend ?? [];
    return {
      labels: rows.map((r) => formatShortDate(r.weekStart)),
      datasets: [
        {
          label: this.transloco.translate('reports.kpi.dailyProductivity'),
          data: rows.map((r) => r.avgCompletedPerTeam ?? 0),
          borderColor: '#248fac',
          backgroundColor: 'rgba(36, 143, 172, 0.14)',
          fill: true,
          tension: 0.35,
          borderWidth: 2,
        },
      ],
    };
  });

  protected readonly groupedBarOptions = signal<Record<string, unknown>>({});
  protected readonly horizontalBarOptions = signal<Record<string, unknown>>({});
  protected readonly lineOptionsWithLegend = signal<Record<string, unknown>>({});
  protected readonly lineOptionsNoLegend = signal<Record<string, unknown>>({});

  protected readonly exportFn = (format: ExportFormat): Observable<unknown> => {
    const f = this.filter();
    return this.reportExport.download(
      `/api/v1/fsms/reports/team-performance/export/${format}`,
      {
        FromDate: f.fromDate?.toISOString(),
        ToDate: f.toDate?.toISOString(),
        Status: f.status ?? undefined,
        TeamId: f.teamId != null ? String(f.teamId) : undefined,
        BranchCode: f.branchCode ?? undefined,
        OperationAreaCode: f.operationAreaCode ?? undefined,
      },
      `TeamPerformance.${format === 'excel' ? 'xlsx' : 'pdf'}`,
    );
  };

  constructor() {
    effect(() => {
      this.theme.mode();
      this.groupedBarOptions.set(buildBarOptions(false, true));
      this.horizontalBarOptions.set(buildBarOptions(true, false, 100));
      this.lineOptionsWithLegend.set(buildLineOptions(true));
      this.lineOptionsNoLegend.set(buildLineOptions(false));
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

    this.load(EMPTY_REPORT_FILTER);
  }

  protected onApply(value: ReportFilterValue): void {
    this.load(value);
  }

  protected onReset(): void {
    this.load(EMPTY_REPORT_FILTER);
  }

  protected branchDisplay(branchCode?: string): string {
    if (!branchCode) {
      return '—';
    }
    return this.branchNameByCode().get(branchCode) || branchCode;
  }

  protected completionSeverity(percent: number): 'success' | 'warn' | 'danger' {
    if (percent >= 80) return 'success';
    if (percent >= 50) return 'warn';
    return 'danger';
  }

  private load(filter: ReportFilterValue): void {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);
    this.reportsClient
      .fsmsReports_GetTeamPerformance(
        filter.fromDate ?? undefined,
        filter.toDate ?? undefined,
        filter.status ?? undefined,
        filter.teamId ?? undefined,
        filter.branchCode ?? undefined,
        filter.operationAreaCode ?? undefined,
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

function completionColor(percent: number): string {
  if (percent >= 80) return '#10b981';
  if (percent >= 50) return '#f59e0b';
  return '#ef4444';
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

function buildBarOptions(horizontal: boolean, showLegend = false, max?: number): Record<string, unknown> {
  const { text, muted, border } = readChartColors();
  return {
    maintainAspectRatio: false,
    indexAxis: horizontal ? 'y' : 'x',
    plugins: { legend: { display: showLegend, labels: { color: text, usePointStyle: true } } },
    scales: {
      x: {
        ticks: { color: muted },
        grid: { color: horizontal ? border : 'transparent' },
        beginAtZero: true,
        ...(horizontal && max ? { max } : {}),
      },
      y: {
        ticks: { color: muted },
        grid: { color: horizontal ? 'transparent' : border },
        beginAtZero: true,
      },
    },
  };
}

function buildLineOptions(showLegend: boolean): Record<string, unknown> {
  const { text, muted, border } = readChartColors();
  return {
    maintainAspectRatio: false,
    plugins: { legend: { display: showLegend, labels: { color: text, usePointStyle: true } } },
    scales: {
      x: { ticks: { color: muted }, grid: { color: 'transparent' } },
      y: { ticks: { color: muted }, grid: { color: border }, beginAtZero: true },
    },
  };
}
