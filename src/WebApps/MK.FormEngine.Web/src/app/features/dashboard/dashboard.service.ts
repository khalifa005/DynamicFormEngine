import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { DashboardDto, FsmsReportsClient } from '../../core/api/api-client.generated';
import {
  DashboardFilter,
  DashboardView,
  KpiCard,
  StatusDistribution,
  TrendSeries,
} from './dashboard.model';

/** Status slice colours, keyed by the API's status vocabulary. */
const STATUS_COLORS: Readonly<Record<string, string>> = {
  CREATED: '#64748b',
  ASSIGNED: '#06b6d4',
  IN_PROGRESS: '#3b82f6',
  SUBMITTED: '#8b5cf6',
  UNDER_REVIEW: '#f59e0b',
  APPROVED: '#10b981',
  RETURNED: '#ef4444',
  EXPIRED: '#94a3b8',
};

const FALLBACK_STATUS_COLOR = '#94a3b8';

/**
 * The dashboard's single read. One call returns every widget's data, because the server computes
 * them all from the same filtered survey set — splitting them into separate requests would mean
 * paying for that filter once per widget.
 */
@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly reportsClient = inject(FsmsReportsClient);

  load(filter: DashboardFilter): Observable<DashboardView> {
    return this.reportsClient.fsmsReports_GetDashboard(
        filter.fromDate ?? undefined,
        filter.toDate ?? undefined,
        filter.org.clusterCode ?? undefined,
        filter.org.cbuCode ?? undefined,
        filter.org.branchCode ?? undefined,
        filter.org.operationAreaCode ?? undefined,
        filter.departmentId ?? undefined,
        filter.templateId ?? undefined,
        filter.faTypeCode ?? undefined,
        filter.status ?? undefined,
        filter.source ?? undefined,
        filter.trendGrain,
        filter.peerScope,
        undefined,
        10,
      )
      .pipe(map((result) => this.toView(result.data)));
  }

  private toView(dto: DashboardDto | undefined): DashboardView {
    const kpis = dto?.kpis ?? {};

    return {
      kpis,
      kpiCards: this.toKpiCards(dto),
      trend: this.toTrend(dto),
      statusDistribution: this.toStatusDistribution(dto),
      teamsByOrg: dto?.teamsByOrg ?? [],
      teamsByDepartment: dto?.teamsByDepartment ?? [],
      myStats: dto?.myStats ?? {},
      peers: dto?.peerComparison ?? [],
      transactions: dto?.transactions ?? [],
      recentSurveys: dto?.recentSurveys ?? [],
      breakdowns: dto?.breakdowns ?? [],
      lateSurveys: dto?.lateSurveys ?? [],
      lateTeams: dto?.lateTeams ?? [],
    };
  }

  private toKpiCards(dto: DashboardDto | undefined): KpiCard[] {
    const k = dto?.kpis ?? {};

    return [
      {
        key: 'totalSurveys',
        labelKey: 'dashboard.kpiTotalSurveys',
        value: k.totalSurveys ?? 0,
        deltaPercent: k.totalSurveysDelta ?? 0,
        icon: 'pi pi-file-edit',
        accent: '#145e77',
      },
      {
        key: 'approvedSurveys',
        labelKey: 'dashboard.kpiApprovedSurveys',
        value: k.approvedSurveys ?? 0,
        deltaPercent: k.approvedSurveysDelta ?? 0,
        icon: 'pi pi-check-circle',
        accent: '#10b981',
      },
      {
        // Filled work waiting on a reviewer. SUBMITTED is that state outright — a survey is
        // completed or returned straight from it. The card key stays `underReview` because the
        // app config's KPI visibility map is keyed on it.
        key: 'underReview',
        labelKey: 'dashboard.kpiUnderReviewSurveys',
        value: k.submittedSurveys ?? 0,
        deltaPercent: k.submittedSurveysDelta ?? 0,
        icon: 'pi pi-clock',
        accent: '#f59e0b',
      },
      {
        key: 'returnedSurveys',
        labelKey: 'dashboard.kpiReturnedSurveys',
        value: k.returnedSurveys ?? 0,
        deltaPercent: k.returnedSurveysDelta ?? 0,
        icon: 'pi pi-exclamation-circle',
        accent: '#ef4444',
        inverse: true,
      },
      {
        key: 'overdueSurveys',
        labelKey: 'dashboard.kpiOverdueSurveys',
        value: k.overdueSurveys ?? 0,
        deltaPercent: k.overdueSurveysDelta ?? 0,
        icon: 'pi pi-exclamation-triangle',
        accent: '#dc2626',
        inverse: true,
      },
      {
        key: 'activeTeams',
        labelKey: 'dashboard.kpiActiveTeams',
        value: k.activeTeams ?? 0,
        deltaPercent: k.activeTeamsDelta ?? 0,
        icon: 'pi pi-users',
        accent: '#2563eb',
      },
      {
        key: 'completionRate',
        labelKey: 'dashboard.kpiCompletionRate',
        value: k.completionRatePercent ?? 0,
        deltaPercent: k.completionRateDelta ?? 0,
        icon: 'pi pi-verified',
        accent: '#059669',
        isPercent: true,
      },
      {
        key: 'onTimeRate',
        labelKey: 'dashboard.kpiOnTimeRate',
        value: k.onTimeRatePercent ?? 0,
        deltaPercent: k.onTimeRateDelta ?? 0,
        icon: 'pi pi-stopwatch',
        accent: '#0891b2',
        isPercent: true,
      },
      {
        key: 'returnRate',
        labelKey: 'dashboard.kpiReturnRate',
        value: k.returnRatePercent ?? 0,
        deltaPercent: k.returnRateDelta ?? 0,
        icon: 'pi pi-replay',
        accent: '#f97316',
        isPercent: true,
        inverse: true,
      },
      {
        key: 'avgCompletion',
        labelKey: 'dashboard.kpiAvgCompletionHours',
        value: k.avgCompletionHours ?? 0,
        deltaPercent: 0,
        icon: 'pi pi-hourglass',
        accent: '#7c3aed',
        isHours: true,
      },
    ];
  }

  /** Pivots the trend rows into the parallel arrays Chart.js expects. */
  private toTrend(dto: DashboardDto | undefined): TrendSeries {
    const points = dto?.trend ?? [];

    return {
      labels: points.map((p) => p.bucketKey ?? ''),
      submitted: points.map((p) => p.submittedCount ?? 0),
      approved: points.map((p) => p.approvedCount ?? 0),
      returned: points.map((p) => p.returnedCount ?? 0),
    };
  }

  /**
   * Drops the empty slices. The API returns every status so the ordering stays stable, but a
   * doughnut with six zero-width wedges is just a cluttered legend.
   */
  private toStatusDistribution(dto: DashboardDto | undefined): StatusDistribution {
    const slices = (dto?.statusDistribution ?? []).filter((s) => (s.count ?? 0) > 0);

    return {
      statusKeys: slices.map((s) => `surveys.status.${s.status}`),
      counts: slices.map((s) => s.count ?? 0),
      colors: slices.map((s) => STATUS_COLORS[s.status ?? ''] ?? FALLBACK_STATUS_COLOR),
    };
  }
}
