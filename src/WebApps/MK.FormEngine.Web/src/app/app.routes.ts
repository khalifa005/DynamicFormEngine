import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards/auth.guard';
import { ngxPermissionsGuard } from 'ngx-permissions';
import { ADMINISTRATOR_ROLE, PERMISSIONS } from './core/auth/permissions';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    loadComponent: () =>
      import('./features/auth/login/login.component').then((m) => m.LoginComponent),
  },
  // Public on purpose: the browser arrives here straight from the identity provider, before there is
  // any session for a guard to inspect.
  {
    path: 'auth/saml-callback',
    loadComponent: () =>
      import('./features/auth/saml-callback/saml-callback.component').then(
        (m) => m.SamlCallbackComponent,
      ),
  },
  {
    path: 'auth/access-denied',
    loadComponent: () =>
      import('./features/auth/access-denied/access-denied.component').then(
        (m) => m.AccessDeniedComponent,
      ),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./layout/dashboard-layout/dashboard-layout.component').then(
        (m) => m.DashboardLayoutComponent,
      ),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        canActivate: [ngxPermissionsGuard],
        data: {
          permissions: {
            only: [PERMISSIONS.viewDashboard, ADMINISTRATOR_ROLE],
            redirectTo: '/login',
          },
          titleKey: 'nav.dashboard',
          subtitleKey: 'dashboard.subtitle',
        },
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'admin/roles',
        canActivate: [ngxPermissionsGuard],
        data: {
          permissions: {
            only: [PERMISSIONS.manageRolePermissions, ADMINISTRATOR_ROLE],
            redirectTo: '/login',
          },
          titleKey: 'admin.rolePermissions.title',
          subtitleKey: 'admin.rolePermissions.subtitle',
        },
        loadComponent: () =>
          import('./features/admin/role-permissions/role-permissions.component').then(
            (m) => m.RolePermissionsComponent,
          ),
      },
      {
        path: 'admin/users',
        canActivate: [ngxPermissionsGuard],
        data: {
          permissions: {
            only: [PERMISSIONS.manageUsers, ADMINISTRATOR_ROLE],
            redirectTo: '/login',
          },
          titleKey: 'users.title',
          subtitleKey: 'users.subtitle',
        },
        loadComponent: () =>
          import('./features/admin/users/user-list.component').then((m) => m.UserListComponent),
      },
      {
        path: 'admin/data-migration',
        canActivate: [ngxPermissionsGuard],
        data: {
          permissions: {
            only: [PERMISSIONS.importData, ADMINISTRATOR_ROLE],
            redirectTo: '/login',
          },
          titleKey: 'dataMigration.title',
          subtitleKey: 'dataMigration.subtitle',
        },
        loadComponent: () =>
          import('./features/admin/data-migration/data-migration.component').then(
            (m) => m.DataMigrationComponent,
          ),
      },
      // {
      //   path: 'form-builder',
      //   canActivate: [ngxPermissionsGuard],
      //   data: {
      //     permissions: {
      //       only: [PERMISSIONS.manageTemplates, ADMINISTRATOR_ROLE],
      //       redirectTo: '/login',
      //     },
      //     titleKey: 'formBuilder.title',
      //     subtitleKey: 'formBuilder.subtitle',
      //   },
      //   loadComponent: () =>
      //     import('./features/form-builder/form-builder.component').then((m) => m.FormBuilderComponent),
      // },
      {
        path: 'lookups',
        canActivate: [ngxPermissionsGuard],
        data: {
          permissions: {
            only: [PERMISSIONS.manageLookups, ADMINISTRATOR_ROLE],
            redirectTo: '/login',
          },
          titleKey: 'lookups.title',
          subtitleKey: 'lookups.subtitle',
        },
        loadComponent: () =>
          import('./features/lookups/lookups.component').then((m) => m.LookupsComponent),
      },
      {
        path: 'teams',
        loadChildren: () => import('./features/teams/teams.routes').then((m) => m.routes),
      },
      {
        path: 'surveys',
        loadChildren: () => import('./features/surveys/surveys.routes').then((m) => m.routes),
      },
      {
        path: 'reports',
        loadChildren: () => import('./features/reports/reports.routes').then((m) => m.routes),
      },
      // {
      //   path: 'tracking',
      //   loadChildren: () => import('./features/tracking/tracking.routes').then((m) => m.routes),
      // },
      // {
      //   path: 'monitoring',
      //   loadChildren: () =>
      //     import('./features/monitoring/monitoring.routes').then((m) => m.routes),
      // },
      {
        path: 'templates',
        canActivate: [ngxPermissionsGuard],
        data: {
          permissions: {
            only: [PERMISSIONS.viewTemplates, ADMINISTRATOR_ROLE],
            redirectTo: '/login',
          },
        },
        children: [
          {
            path: '',
            data: { titleKey: 'templates.management', subtitleKey: 'templates.subtitle' },
            loadComponent: () =>
              import('./features/templates/list/template-list').then((m) => m.TemplateList),
          },
          {
            path: 'new',
            canActivate: [ngxPermissionsGuard],
            data: {
              permissions: {
                only: [PERMISSIONS.manageTemplates, ADMINISTRATOR_ROLE],
                redirectTo: '/login',
              },
              titleKey: 'templates.newTemplate',
              subtitleKey: 'templates.formSubtitle',
            },
            loadComponent: () =>
              import('./features/templates/form/template-form').then((m) => m.TemplateForm),
          },
          {
            path: ':id',
            canActivate: [ngxPermissionsGuard],
            data: {
              permissions: {
                only: [PERMISSIONS.manageTemplates, ADMINISTRATOR_ROLE],
                redirectTo: '/login',
              },
              titleKey: 'templates.editTemplate',
              subtitleKey: 'templates.formSubtitle',
            },
            loadComponent: () =>
              import('./features/templates/form/template-form').then((m) => m.TemplateForm),
          },
          {
            path: ':id/designer',
            canActivate: [ngxPermissionsGuard],
            data: {
              permissions: {
                only: [PERMISSIONS.manageTemplates, ADMINISTRATOR_ROLE],
                redirectTo: '/login',
              },
              titleKey: 'formBuilder.title',
              subtitleKey: 'formBuilder.subtitle',
            },
            loadComponent: () =>
              import('./features/form-builder/form-builder.component').then((m) => m.FormBuilderComponent),
          },
          {
            path: ':id/fill',
            data: { titleKey: 'templates.render.title', subtitleKey: 'templates.render.subtitle' },
            loadComponent: () =>
              import('./features/templates/render/template-render.component').then((m) => m.TemplateRenderComponent),
          }
        ]
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
