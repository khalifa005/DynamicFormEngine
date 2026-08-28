import { Component, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map } from 'rxjs';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { Menu } from 'primeng/menu';
import { MenuItem } from 'primeng/api';
import { NgxPermissionsModule } from 'ngx-permissions';

import { AuthService } from '../../core/auth/auth.service';
import { AuthStore } from '../../core/auth/auth.store';
import { ADMINISTRATOR_ROLE, PERMISSIONS } from '../../core/auth/permissions';
import { LanguageService } from '../../core/i18n/language.service';
import { PageHeaderService } from '../../core/layout/page-header.service';
import { ThemeService } from '../../core/theme/theme.service';

interface NavChild {
  readonly labelKey: string;
  readonly route: string;
  /** Any one of these grants the item. Required — nothing in the sidebar is ungated. */
  readonly permissions: string[];
}

interface NavItem {
  readonly labelKey: string;
  readonly icon: string;
  /** Absent for a group — a group renders its `children` instead of navigating directly. */
  readonly route?: string;
  /** Any one of these grants the item. Required — nothing in the sidebar is ungated. */
  readonly permissions: string[];
  readonly children?: readonly NavChild[];
}

@Component({
  selector: 'app-dashboard-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslocoModule, Menu, NgxPermissionsModule],
  templateUrl: './dashboard-layout.component.html',
  styleUrl: './dashboard-layout.component.scss',
})
export class DashboardLayoutComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  protected readonly store = inject(AuthStore);
  protected readonly language = inject(LanguageService);
  protected readonly theme = inject(ThemeService);
  protected readonly pageHeader = inject(PageHeaderService);
  private readonly transloco = inject(TranslocoService);

  protected readonly sidebarOpen = signal(true);

  /** Which nav groups the operator has manually expanded — a group whose child route is active is
   *  always expanded too (see `isGroupExpanded`), independent of this set. */
  private readonly expandedGroups = signal<ReadonlySet<string>>(new Set());

  private readonly currentUrl = toSignal(
    this.router.events.pipe(
      filter((event): event is NavigationEnd => event instanceof NavigationEnd),
      map(() => this.router.url),
    ),
    { initialValue: this.router.url },
  );

  // Every entry is gated, and each `permissions` list must mirror its route guard in app.routes.ts
  // — an item that renders for someone the route then redirects away from is worse than no item.
  protected readonly navItems: readonly NavItem[] = [
    {
      labelKey: 'nav.dashboard',
      icon: 'pi pi-th-large',
      route: '/dashboard',
      permissions: [PERMISSIONS.viewDashboard, ADMINISTRATOR_ROLE],
    },
    {
      labelKey: 'nav.templates',
      icon: 'pi pi-file-edit',
      route: '/templates',
      permissions: [PERMISSIONS.viewTemplates, ADMINISTRATOR_ROLE],
    },
    {
      labelKey: 'nav.surveys',
      icon: 'pi pi-clipboard',
      route: '/surveys',
      permissions: [PERMISSIONS.viewSurveys, ADMINISTRATOR_ROLE],
    },
    {
      labelKey: 'nav.reports',
      icon: 'pi pi-chart-bar',
      // Mirrors reports.routes.ts, which every child route below must match.
      permissions: [PERMISSIONS.viewReports, ADMINISTRATOR_ROLE],
      children: [
        {
          labelKey: 'nav.reportsGeneralStatistics',
          route: '/reports/general-statistics',
          permissions: [PERMISSIONS.viewReports, ADMINISTRATOR_ROLE],
        },
        {
          labelKey: 'nav.reportsSurveyTasks',
          route: '/reports/survey-tasks',
          permissions: [PERMISSIONS.viewReports, ADMINISTRATOR_ROLE],
        },
        {
          labelKey: 'nav.reportsTeamPerformance',
          route: '/reports/team-performance',
          permissions: [PERMISSIONS.viewReports, ADMINISTRATOR_ROLE],
        },
      ],
    },
    // {
    //   labelKey: 'nav.tracking',
    //   icon: 'pi pi-map',
    //   route: '/tracking',
    //   permissions: [PERMISSIONS.viewSurveys, ADMINISTRATOR_ROLE],
    // },
    // {
    //   labelKey: 'nav.monitoring',
    //   icon: 'pi pi-map-marker',
    //   route: '/monitoring',
    //   permissions: [PERMISSIONS.viewSurveys, ADMINISTRATOR_ROLE],
    // },
    // {
    //   labelKey: 'nav.formBuilder',
    //   icon: 'pi pi-objects-column',
    //   route: '/form-builder',
    //   permissions: [PERMISSIONS.manageTemplates, ADMINISTRATOR_ROLE],
    // },
    {
      labelKey: 'nav.lookups',
      icon: 'pi pi-list',
      route: '/lookups',
      permissions: [PERMISSIONS.manageLookups, ADMINISTRATOR_ROLE],
    },
    {
      labelKey: 'nav.teams',
      icon: 'pi pi-users',
      route: '/teams',
      permissions: [PERMISSIONS.manageTeams, ADMINISTRATOR_ROLE],
    },
    {
      labelKey: 'nav.users',
      icon: 'pi pi-user-edit',
      route: '/admin/users',
      permissions: [PERMISSIONS.manageUsers, ADMINISTRATOR_ROLE],
    },
    {
      labelKey: 'nav.rolePermissions',
      icon: 'pi pi-shield',
      route: '/admin/roles',
      permissions: [PERMISSIONS.manageRolePermissions, ADMINISTRATOR_ROLE],
    },
    {
      labelKey: 'nav.dataMigration',
      icon: 'pi pi-database',
      route: '/admin/data-migration',
      permissions: [PERMISSIONS.importData, ADMINISTRATOR_ROLE],
    },
  ];

  protected readonly userMenu = computed<MenuItem[]>(() => {
    // Read the active language so the menu re-translates on language change.
    this.language.current();
    return [
      {
        label: this.store.userName() ?? 'User',
        items: [
          {
            label: this.transloco.translate('common.logout'),
            icon: 'pi pi-sign-out',
            command: () => this.logout(),
          },
        ],
      },
    ];
  });

  protected readonly initials = computed(() => {
    const name = this.store.userName() ?? 'U';
    return name.slice(0, 2).toUpperCase();
  });

  protected toggleSidebar(): void {
    this.sidebarOpen.update((v) => !v);
  }

  /** A group with an active child stays expanded regardless of the manual-toggle set below. */
  protected isGroupActive(item: NavItem): boolean {
    const url = this.currentUrl();
    return (item.children ?? []).some((child) => url === child.route || url.startsWith(`${child.route}/`));
  }

  protected isGroupExpanded(item: NavItem): boolean {
    return this.isGroupActive(item) || this.expandedGroups().has(item.labelKey);
  }

  /** Collapsed sidebar has no room for a submenu, so expand the rail first rather than toggle. */
  protected toggleGroup(item: NavItem): void {
    if (!this.sidebarOpen()) {
      this.sidebarOpen.set(true);
    }

    this.expandedGroups.update((current) => {
      const next = new Set(current);
      if (next.has(item.labelKey)) {
        next.delete(item.labelKey);
      } else {
        next.add(item.labelKey);
      }
      return next;
    });
  }

  protected toggleLanguage(): void {
    this.language.toggle();
  }

  protected toggleTheme(): void {
    this.theme.toggle();
  }

  protected logout(): void {
    this.auth.logout();
  }
}
