import { Routes } from '@angular/router';
import { ngxPermissionsGuard } from 'ngx-permissions';
import { ADMINISTRATOR_ROLE, PERMISSIONS } from '../../core/auth/permissions';

export const routes: Routes = [
  {
    path: '',
    canActivate: [ngxPermissionsGuard],
    data: {
      permissions: {
        only: [PERMISSIONS.viewSurveys, ADMINISTRATOR_ROLE],
        redirectTo: '/login',
      },
      titleKey: 'monitoring.title',
      subtitleKey: 'monitoring.subtitle',
    },
    loadComponent: () =>
      import('./live/live-monitoring.component').then((m) => m.LiveMonitoringComponent),
  },
];
