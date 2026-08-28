import { Routes } from '@angular/router';
import { ngxPermissionsGuard } from 'ngx-permissions';
import { ADMINISTRATOR_ROLE, PERMISSIONS } from '../../core/auth/permissions';

export const routes: Routes = [
  {
    path: '',
    canActivate: [ngxPermissionsGuard],
    data: {
      // The route is a view over surveys, so it is gated the same way the worklist is; the API
      // narrows what any one caller can actually see to their own territory on top of this.
      permissions: {
        only: [PERMISSIONS.viewSurveys, ADMINISTRATOR_ROLE],
        redirectTo: '/login',
      },
      titleKey: 'tracking.title',
      subtitleKey: 'tracking.subtitle',
    },
    loadComponent: () =>
      import('./route/team-route.component').then((m) => m.TeamRouteComponent),
  },
];
