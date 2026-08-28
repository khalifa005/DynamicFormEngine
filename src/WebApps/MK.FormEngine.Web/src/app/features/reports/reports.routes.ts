import { Routes } from '@angular/router';
import { ngxPermissionsGuard } from 'ngx-permissions';

import { ADMINISTRATOR_ROLE, PERMISSIONS } from '../../core/auth/permissions';

const REPORTS_PERMISSIONS = { only: [PERMISSIONS.viewReports, ADMINISTRATOR_ROLE], redirectTo: '/login' };

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'general-statistics' },
  {
    path: 'general-statistics',
    canActivate: [ngxPermissionsGuard],
    data: {
      permissions: REPORTS_PERMISSIONS,
      titleKey: 'nav.reportsGeneralStatistics',
      subtitleKey: 'reports.generalStatistics.subtitle',
    },
    loadComponent: () =>
      import('./general-statistics/general-statistics.component').then((m) => m.GeneralStatisticsComponent),
  },
  {
    path: 'survey-tasks',
    canActivate: [ngxPermissionsGuard],
    data: {
      permissions: REPORTS_PERMISSIONS,
      titleKey: 'nav.reportsSurveyTasks',
      subtitleKey: 'reports.surveyTasks.subtitle',
    },
    loadComponent: () => import('./survey-tasks/survey-tasks.component').then((m) => m.SurveyTasksComponent),
  },
  {
    path: 'team-performance',
    canActivate: [ngxPermissionsGuard],
    data: {
      permissions: REPORTS_PERMISSIONS,
      titleKey: 'nav.reportsTeamPerformance',
      subtitleKey: 'reports.teamPerformance.subtitle',
    },
    loadComponent: () =>
      import('./team-performance/team-performance.component').then((m) => m.TeamPerformanceComponent),
  },
];
