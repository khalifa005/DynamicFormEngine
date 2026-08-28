import { InjectionToken } from '@angular/core';

export type AppLanguage = 'ar' | 'en';

/**
 * Which dashboard sections are shown. Every flag defaults to `true`, so omitting one — or shipping
 * an older config file — shows the section rather than blanking it. Set a flag to `false` to hide a
 * section without a rebuild; set it back to `true` to bring it home.
 *
 * This is presentation only. Hiding a section does not stop the API returning its data, so it is
 * not a substitute for the `ViewDashboard` policy or for org scoping.
 */
export interface DashboardSectionFlags {
  readonly filters: boolean;
  readonly kpis: boolean;
  readonly trend: boolean;
  readonly statusDistribution: boolean;
  readonly transactions: boolean;
  readonly myPerformance: boolean;
  readonly peerComparison: boolean;
  readonly teamsByOrg: boolean;
  readonly teamsByDepartment: boolean;
  readonly lateSurveys: boolean;
  readonly lateTeams: boolean;
  readonly recentSurveys: boolean;
}

/**
 * Which individual KPI cards are shown. Keys match the `key` of each card built in
 * `DashboardService.toKpiCards`, and follow the same default-to-visible rule as the sections.
 */
export interface DashboardKpiFlags {
  readonly totalSurveys: boolean;
  readonly approvedSurveys: boolean;
  readonly underReview: boolean;
  readonly returnedSurveys: boolean;
  readonly overdueSurveys: boolean;
  readonly activeTeams: boolean;
  readonly completionRate: boolean;
  readonly onTimeRate: boolean;
  readonly returnRate: boolean;
  readonly avgCompletion: boolean;
}

/**
 * How this deployment's client behaves around corporate SSO.
 *
 * The server still decides whether SSO actually works — these only control what the sign-in page
 * does about it.
 */
export interface SsoClientFlags {
  /**
   * Overrides the server's `Sso:Enabled` for this client.
   *
   * `null` (the default) means "ask the server", which keeps the two from drifting apart. Set it to
   * `true` or `false` when the client must not depend on that probe — notably so a temporarily
   * unreachable API cannot make a working SSO deployment fall back to the password form.
   */
  readonly enabled: boolean | null;

  /**
   * When SSO is on, go straight to the identity provider instead of drawing a sign-in page at all.
   * The credentials form is still reachable at `/login?local=1` for administrator break-glass.
   */
  readonly autoRedirect: boolean;
}

export interface AppConfig {
  readonly apiBaseUrl: string;
  readonly defaultLanguage: AppLanguage;
  readonly availableLanguages: readonly AppLanguage[];
  readonly tokenRefreshEnabled: boolean;
  readonly appName: string;
  /** When true, login is faked client-side and the backend is not called. */
  readonly fakeLogin: boolean;
  /**
   * Browser key for the Google Maps JS API. Browser keys are necessarily public
   * (they ship in the page), so restrict this one by HTTP referrer in the Google
   * Cloud console rather than treating it as a secret.
   */
  readonly googleMapsApiKey?: string;
  readonly dashboardSections: DashboardSectionFlags;
  readonly dashboardKpis: DashboardKpiFlags;
  readonly sso: SsoClientFlags;
}

export const DEFAULT_SSO: SsoClientFlags = {
  enabled: null,
  autoRedirect: true,
};

export const DEFAULT_DASHBOARD_SECTIONS: DashboardSectionFlags = {
  filters: true,
  kpis: true,
  trend: true,
  statusDistribution: true,
  transactions: true,
  myPerformance: true,
  peerComparison: true,
  teamsByOrg: true,
  teamsByDepartment: true,
  lateSurveys: true,
  lateTeams: true,
  recentSurveys: true,
};

export const DEFAULT_DASHBOARD_KPIS: DashboardKpiFlags = {
  totalSurveys: true,
  approvedSurveys: true,
  underReview: true,
  returnedSurveys: true,
  overdueSurveys: true,
  activeTeams: true,
  completionRate: true,
  onTimeRate: true,
  returnRate: true,
  avgCompletion: true,
};

/** Runtime configuration resolved from public/config/app-config.json before bootstrap. */
export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG');
