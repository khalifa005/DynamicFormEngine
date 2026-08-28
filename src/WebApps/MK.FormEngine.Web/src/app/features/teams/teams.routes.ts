import { Routes } from '@angular/router';
import { ngxPermissionsGuard } from 'ngx-permissions';
import { ADMINISTRATOR_ROLE, PERMISSIONS } from '../../core/auth/permissions';

export const routes: Routes = [
  {
    path: '',
    canActivate: [ngxPermissionsGuard],
    data: {
      permissions: {
        only: [PERMISSIONS.manageTeams, ADMINISTRATOR_ROLE],
        redirectTo: '/login',
      },
      titleKey: 'teams.management',
      subtitleKey: 'teams.subtitle',
    },
    loadComponent: () =>
      import('./list/team-list.component').then((m) => m.TeamListComponent),
  }
];
