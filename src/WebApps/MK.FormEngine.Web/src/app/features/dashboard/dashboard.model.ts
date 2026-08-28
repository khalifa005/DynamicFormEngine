import {
  DashboardBreakdownDto,
  DashboardDepartmentTeamStatsDto,
  DashboardKpisDto,
  DashboardLateSurveyDto,
  DashboardLateTeamDto,
  DashboardMyStatsDto,
  DashboardOrgTeamStatsDto,
  DashboardPeerStatDto,
  DashboardRecentSurveyDto,
  DashboardTransactionDto,
} from '../../core/api/api-client.generated';
import { OrgLocation } from '../../shared/components/org-scope/org-scope.model';

/** A headline number with its change against the preceding period of equal length. */
export interface KpiCard {
  readonly key: string;
  readonly labelKey: string;
  readonly value: number;
  readonly deltaPercent: number;
  readonly icon: string;
  readonly accent: string;
  /** Renders the value as a percentage rather than a count. */
  readonly isPercent?: boolean;
  /** Renders the value in hours. */
  readonly isHours?: boolean;
  /**
   * True when a rise is bad news — returns and overdue work. The arrow still follows the sign of
   * the change; only its colour flips, so "returns up 40%" never reads as green.
   */
  readonly inverse?: boolean;
}

export interface TrendSeries {
  readonly labels: readonly string[];
  readonly submitted: readonly number[];
  readonly approved: readonly number[];
  readonly returned: readonly number[];
}

export interface StatusDistribution {
  readonly statusKeys: readonly string[];
  readonly counts: readonly number[];
  readonly colors: readonly string[];
}

/** Which level of the geography the "teams by org" table is showing. */
export type OrgLevel = 'Cbu' | 'Branch' | 'OperationArea';

/** Everything the dashboard can be narrowed by. Sent to the API on every load. */
export interface DashboardFilter {
  fromDate: Date | null;
  toDate: Date | null;
  org: OrgLocation;
  departmentId: number | null;
  templateId: number | null;
  faTypeCode: string | null;
  status: string | null;
  source: string | null;
  trendGrain: string;
  peerScope: string;
}

/**
 * The dashboard as the page consumes it: the API shapes, plus the two series the charts need
 * pre-pivoted. Charts want columns; the API returns rows, and pivoting once here keeps that
 * reshaping out of the template.
 */
export interface DashboardView {
  readonly kpis: DashboardKpisDto;
  readonly kpiCards: readonly KpiCard[];
  readonly trend: TrendSeries;
  readonly statusDistribution: StatusDistribution;
  // The list properties are mutable arrays, not `readonly` ones: PrimeNG's `p-table [value]`
  // takes a mutable array, and a `readonly` type will not bind to it.
  readonly teamsByOrg: DashboardOrgTeamStatsDto[];
  readonly teamsByDepartment: DashboardDepartmentTeamStatsDto[];
  readonly myStats: DashboardMyStatsDto;
  readonly peers: DashboardPeerStatDto[];
  readonly transactions: DashboardTransactionDto[];
  readonly recentSurveys: DashboardRecentSurveyDto[];
  readonly breakdowns: DashboardBreakdownDto[];
  readonly lateSurveys: DashboardLateSurveyDto[];
  readonly lateTeams: DashboardLateTeamDto[];
}

/** How a survey came to be late. Mirrors `DashboardLatenessKinds` on the API. */
export const LATENESS_KINDS = {
  overdue: 'OVERDUE',
  completedLate: 'COMPLETED_LATE',
} as const;

/** Which SLA clock was breached. Mirrors `DashboardDeadlineKinds` on the API. */
export const DEADLINE_KINDS = {
  fill: 'FILL',
  completion: 'COMPLETION',
} as const;
