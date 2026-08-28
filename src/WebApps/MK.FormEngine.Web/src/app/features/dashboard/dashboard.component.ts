import { Component, DestroyRef, OnInit, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { finalize } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { UIChart } from 'primeng/chart';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectButtonModule } from 'primeng/selectbutton';
import { SelectModule } from 'primeng/select';
import { SkeletonModule } from 'primeng/skeleton';
import { TableModule } from 'primeng/table';
import { Tag } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';

import {
  DashboardOrgTeamStatsDto,
  DashboardTransactionDto,
} from '../../core/api/api-client.generated';
import { AuthStore } from '../../core/auth/auth.store';
import { AppConfigService } from '../../core/config/app-config.service';
import { DashboardKpiFlags } from '../../core/config/app-config.model';
import { LanguageService } from '../../core/i18n/language.service';
import { PageHeaderService } from '../../core/layout/page-header.service';
import { ThemeService } from '../../core/theme/theme.service';
import { OrgScopeSelectorComponent } from '../../shared/components/org-scope/org-scope-selector.component';
import {
  EMPTY_ORG_LOCATION,
  OrgLocation,
} from '../../shared/components/org-scope/org-scope.model';
import { DashboardService } from './dashboard.service';
import { DashboardFilter, DashboardView, LATENESS_KINDS, OrgLevel } from './dashboard.model';
import { surveyStatusSeverity, SurveyTagSeverity } from '../surveys/survey-status';

/** Default window: the last six months, matching the procedure's own default. */
const DEFAULT_MONTHS_BACK = 6;

interface SelectOption {
  readonly label: string;
  readonly value: string;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    TranslocoModule,
    ButtonModule,
    DatePickerModule,
    SelectButtonModule,
    SelectModule,
    SkeletonModule,
    TableModule,
    Tag,
    TooltipModule,
    UIChart,
    OrgScopeSelectorComponent,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  private readonly service = inject(DashboardService);
  private readonly theme = inject(ThemeService);
  private readonly pageHeader = inject(PageHeaderService);
  private readonly language = inject(LanguageService);
  private readonly transloco = inject(TranslocoService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly appConfig = inject(AppConfigService);
  protected readonly store = inject(AuthStore);

  /**
   * Which sections to render, from `public/config/app-config.json`. Read once — the config file is
   * fetched before bootstrap and never changes during a session.
   */
  protected readonly sections = this.appConfig.snapshot.dashboardSections;
  private readonly kpiFlags = this.appConfig.snapshot.dashboardKpis;

  /**
   * How many panels each grid row will actually render. The grids declare fixed column templates,
   * so hiding a panel without this would leave an empty column where it used to sit.
   */
  protected readonly midGridCount = [
    this.sections.trend,
    this.sections.statusDistribution,
    this.sections.transactions,
  ].filter(Boolean).length;

  protected readonly peerGridCount = [
    this.sections.myPerformance,
    this.sections.peerComparison,
  ].filter(Boolean).length;

  protected readonly loading = signal(false);
  protected readonly view = signal<DashboardView | null>(null);

  /**
   * Skeleton placeholders while the first load is in flight. Sized to the cards that will actually
   * appear, so the grid does not reflow once the data lands.
   */
  protected readonly skeletonCards = Object.values(this.appConfig.snapshot.dashboardKpis)
    .filter(Boolean)
    .map((_, index) => index);

  // --- Filters -------------------------------------------------------------
  protected fromDate: Date | null = defaultFromDate();
  protected toDate: Date | null = new Date();
  protected orgFilter: OrgLocation = { ...EMPTY_ORG_LOCATION };
  protected readonly orgFilterSeed = signal<OrgLocation>({ ...EMPTY_ORG_LOCATION });
  protected trendGrain = 'MONTH';
  protected peerScope = 'ROLE';

  /** Which level the "teams by org" table is showing. Client-side — all three levels are loaded. */
  protected readonly orgLevel = signal<OrgLevel>('Branch');

  protected readonly trendGrainOptions = computed<SelectOption[]>(() => {
    this.language.current();
    return [
      { label: this.transloco.translate('dashboard.grainDay'), value: 'DAY' },
      { label: this.transloco.translate('dashboard.grainWeek'), value: 'WEEK' },
      { label: this.transloco.translate('dashboard.grainMonth'), value: 'MONTH' },
    ];
  });

  protected readonly peerScopeOptions = computed<SelectOption[]>(() => {
    this.language.current();
    return [
      { label: this.transloco.translate('dashboard.peerScopeRole'), value: 'ROLE' },
      { label: this.transloco.translate('dashboard.peerScopeTeam'), value: 'TEAM' },
      { label: this.transloco.translate('dashboard.peerScopeDepartment'), value: 'DEPARTMENT' },
      { label: this.transloco.translate('dashboard.peerScopeBranch'), value: 'BRANCH' },
    ];
  });

  protected readonly orgLevelOptions = computed<SelectOption[]>(() => {
    this.language.current();
    return [
      { label: this.transloco.translate('dashboard.levelCbu'), value: 'Cbu' },
      { label: this.transloco.translate('dashboard.levelBranch'), value: 'Branch' },
      { label: this.transloco.translate('dashboard.levelArea'), value: 'OperationArea' },
    ];
  });

  // --- Derived views -------------------------------------------------------

  /** The KPI cards left after the config's per-card flags are applied. */
  protected readonly kpiCards = computed(() =>
    (this.view()?.kpiCards ?? []).filter(
      (card) => this.kpiFlags[card.key as keyof DashboardKpiFlags] !== false,
    ),
  );
  protected readonly recentSurveys = computed(() => this.view()?.recentSurveys ?? []);
  protected readonly transactions = computed(() => this.view()?.transactions ?? []);
  protected readonly myStats = computed(() => this.view()?.myStats ?? {});
  protected readonly peers = computed(() => this.view()?.peers ?? []);
  protected readonly teamsByDepartment = computed(() => this.view()?.teamsByDepartment ?? []);
  protected readonly lateSurveys = computed(() => this.view()?.lateSurveys ?? []);
  protected readonly lateTeams = computed(() => this.view()?.lateTeams ?? []);

  /** The org table, narrowed to the level the toggle is on. */
  protected readonly teamsByOrg = computed<DashboardOrgTeamStatsDto[]>(() => {
    const level = this.orgLevel();
    return (this.view()?.teamsByOrg ?? []).filter((row) => row.level === level);
  });

  /** The caller's own row, used to highlight their bar and label their standing. */
  protected readonly myPeerRow = computed(() => this.peers().find((p) => p.isCurrentUser));

  protected readonly chartData = computed(() => {
    const t = this.view()?.trend;
    // Depend on the current language so dataset labels re-translate on switch.
    this.language.current();

    if (!t) {
      return { labels: [], datasets: [] };
    }

    return {
      labels: [...t.labels],
      datasets: [
        {
          label: this.transloco.translate('dashboard.chartSubmitted'),
          data: [...t.submitted],
          borderColor: '#157591',
          backgroundColor: 'rgba(21, 117, 145, 0.14)',
          fill: true,
          tension: 0.4,
          borderWidth: 2,
        },
        {
          label: this.transloco.translate('dashboard.chartApproved'),
          data: [...t.approved],
          borderColor: '#10b981',
          backgroundColor: 'rgba(16, 185, 129, 0.14)',
          fill: true,
          tension: 0.4,
          borderWidth: 2,
        },
        {
          label: this.transloco.translate('dashboard.chartReturned'),
          data: [...t.returned],
          borderColor: '#ef4444',
          backgroundColor: 'rgba(239, 68, 68, 0.14)',
          fill: true,
          tension: 0.4,
          borderWidth: 2,
        },
      ],
    };
  });

  protected readonly doughnutData = computed(() => {
    const dist = this.view()?.statusDistribution;
    this.language.current();

    if (!dist) {
      return { labels: [], datasets: [] };
    }

    return {
      labels: dist.statusKeys.map((key) => this.transloco.translate(key)),
      datasets: [
        {
          data: [...dist.counts],
          backgroundColor: [...dist.colors],
          hoverOffset: 6,
        },
      ],
    };
  });

  /**
   * The peer bar chart. The caller's own bar is drawn in the accent colour and everyone else's in
   * grey, so their standing reads at a glance without hunting for their name in the axis.
   */
  protected readonly peerChartData = computed(() => {
    const peers = this.peers();
    this.language.current();

    return {
      labels: peers.map((p) => p.displayName ?? ''),
      datasets: [
        {
          label: this.transloco.translate('dashboard.transactionCount'),
          data: peers.map((p) => p.transactionCount ?? 0),
          backgroundColor: peers.map((p) => (p.isCurrentUser ? '#157591' : '#cbd5e1')),
          borderRadius: 4,
        },
      ],
    };
  });

  protected readonly lineChartOptions = signal<Record<string, unknown>>({});
  protected readonly doughnutChartOptions = signal<Record<string, unknown>>({});
  protected readonly barChartOptions = signal<Record<string, unknown>>({});

  constructor() {
    // Recompute chart theme colors whenever the theme mode changes.
    effect(() => {
      this.theme.mode();
      this.lineChartOptions.set(this.buildLineChartOptions());
      this.doughnutChartOptions.set(this.buildDoughnutChartOptions());
      this.barChartOptions.set(this.buildBarChartOptions());
    });

    // Show topbar title, re-translated on language change.
    effect(() => {
      this.language.current();
      this.pageHeader.set({
        titleText: this.transloco.translate('dashboard.greeting', {
          name: this.store.userName() ?? '',
        }),
        subtitleKey: 'dashboard.subtitle',
      });
    });
  }

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    if (this.loading()) {
      return;
    }

    this.loading.set(true);

    this.service
      .load(this.currentFilter())
      .pipe(
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (view) => this.view.set(view),
      });
  }

  protected resetFilters(): void {
    this.fromDate = defaultFromDate();
    this.toDate = new Date();
    this.orgFilter = { ...EMPTY_ORG_LOCATION };
    this.orgFilterSeed.set({ ...EMPTY_ORG_LOCATION });
    this.trendGrain = 'MONTH';
    this.peerScope = 'ROLE';
    this.load();
  }

  protected onOrgFilterChange(location: OrgLocation): void {
    this.orgFilter = location;
  }

  private currentFilter(): DashboardFilter {
    return {
      fromDate: this.fromDate,
      toDate: this.toDate,
      org: this.orgFilter,
      departmentId: null,
      templateId: null,
      faTypeCode: null,
      status: null,
      source: null,
      trendGrain: this.trendGrain,
      peerScope: this.peerScope,
    };
  }

  // --- Template helpers ----------------------------------------------------

  protected statusSeverity(status?: string): SurveyTagSeverity {
    return surveyStatusSeverity(status);
  }

  /**
   * Translation key for a KPI's "how is this calculated" tooltip. The wording lives in the i18n
   * files under `dashboard.kpiHelp.<key>` so it can be reworded without touching the code.
   */
  protected helpKey(kpiKey: string): string {
    return `dashboard.kpiHelp.${kpiKey}`;
  }

  protected statusKey(status?: string): string {
    return `surveys.status.${status ?? ''}`;
  }

  protected sourceKey(source?: string): string {
    return `surveys.source.${source ?? ''}`;
  }

  protected latenessKey(kind?: string): string {
    return `dashboard.lateness.${kind ?? ''}`;
  }

  protected deadlineKindKey(kind?: string): string {
    return `dashboard.deadlineKind.${kind ?? ''}`;
  }

  /**
   * Still-open lateness reads as danger; work that finished late is history, so it reads as a
   * warning. Keeping them visually distinct is the point of showing both in one table.
   */
  protected latenessSeverity(kind?: string): SurveyTagSeverity {
    return kind === LATENESS_KINDS.overdue ? 'danger' : 'warn';
  }

  /** Picks the name matching the active language, falling back to whichever exists. */
  protected localizedName(nameEn?: string, nameAr?: string): string {
    const preferred = this.language.current() === 'ar' ? nameAr : nameEn;
    return preferred?.trim() || nameEn?.trim() || nameAr?.trim() || '';
  }

  /** Maps a transition to the icon and tone the activity feed shows it with. */
  protected transactionIcon(row: DashboardTransactionDto): string {
    switch (row.toStatus) {
      case 'APPROVED':
        return 'pi pi-check-circle';
      case 'RETURNED':
        return 'pi pi-replay';
      case 'SUBMITTED':
        return 'pi pi-send';
      case 'ASSIGNED':
        return 'pi pi-user-plus';
      case 'UNDER_REVIEW':
        return 'pi pi-search';
      case 'EXPIRED':
        return 'pi pi-ban';
      default:
        return 'pi pi-file-edit';
    }
  }

  protected transactionSeverity(row: DashboardTransactionDto): string {
    switch (row.toStatus) {
      case 'APPROVED':
        return 'success';
      case 'RETURNED':
      case 'EXPIRED':
        return 'danger';
      case 'UNDER_REVIEW':
        return 'warn';
      default:
        return 'info';
    }
  }

  private buildLineChartOptions(): Record<string, unknown> {
    const { text, muted, border } = readChartColors();

    return {
      maintainAspectRatio: false,
      plugins: {
        legend: { labels: { color: text, usePointStyle: true } },
      },
      scales: {
        x: { ticks: { color: muted }, grid: { color: 'transparent' } },
        y: { ticks: { color: muted }, grid: { color: border }, beginAtZero: true },
      },
    };
  }

  private buildDoughnutChartOptions(): Record<string, unknown> {
    const { text } = readChartColors();

    return {
      maintainAspectRatio: false,
      plugins: {
        legend: {
          position: 'right',
          labels: { color: text, usePointStyle: true, boxWidth: 10, padding: 12 },
        },
      },
    };
  }

  private buildBarChartOptions(): Record<string, unknown> {
    const { muted, border } = readChartColors();

    return {
      maintainAspectRatio: false,
      indexAxis: 'y',
      plugins: { legend: { display: false } },
      scales: {
        x: { ticks: { color: muted }, grid: { color: border }, beginAtZero: true },
        y: { ticks: { color: muted }, grid: { color: 'transparent' } },
      },
    };
  }
}

function defaultFromDate(): Date {
  const date = new Date();
  date.setMonth(date.getMonth() - DEFAULT_MONTHS_BACK);
  return date;
}

function readChartColors(): { text: string; muted: string; border: string } {
  const styles = getComputedStyle(document.documentElement);

  return {
    text: styles.getPropertyValue('--p-text-color').trim() || '#334155',
    muted: styles.getPropertyValue('--p-text-muted-color').trim() || '#64748b',
    border: styles.getPropertyValue('--app-border').trim() || '#e2e8f0',
  };
}
