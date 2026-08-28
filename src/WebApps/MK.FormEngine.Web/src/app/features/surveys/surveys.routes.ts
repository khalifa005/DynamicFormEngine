import { Routes } from '@angular/router';
import { ngxPermissionsGuard } from 'ngx-permissions';
import { ADMINISTRATOR_ROLE, PERMISSIONS } from '../../core/auth/permissions';

export const routes: Routes = [
  {
    path: '',
    canActivate: [ngxPermissionsGuard],
    data: {
      // Everyone who touches a survey needs the worklist; the individual actions are gated
      // per-endpoint by the API's CreateSurveys / ManageAssignments / SubmitSurveys / ReviewSurveys policies.
      permissions: {
        only: [PERMISSIONS.viewSurveys, ADMINISTRATOR_ROLE],
        redirectTo: '/login',
      },
      titleKey: 'surveys.title',
      subtitleKey: 'surveys.subtitle',
    },
    loadComponent: () => import('./list/survey-list.component').then((m) => m.SurveyListComponent),
  },
];
