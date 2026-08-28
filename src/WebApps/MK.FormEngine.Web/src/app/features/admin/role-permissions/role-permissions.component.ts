import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { finalize, forkJoin } from 'rxjs';

import { AdminClient, PermissionDto, RoleDto } from '../../../core/api/api-client.generated';
import { apiErrorMessage } from '../../../core/api/api-error';
import { LanguageService } from '../../../core/i18n/language.service';

/** One row of the matrix: a permission, plus which roles currently hold it. */
interface PermissionRow {
  readonly code: string;
  readonly module: string;
  readonly nameEn: string;
  readonly nameAr: string;
}

@Component({
  selector: 'app-role-permissions',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslocoDirective,
    ButtonModule,
    CheckboxModule,
    TableModule,
    TagModule,
    ToastModule,
    TooltipModule,
  ],
  providers: [MessageService],
  templateUrl: './role-permissions.component.html',
})
export class RolePermissionsComponent implements OnInit {
  private readonly adminClient = inject(AdminClient);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  protected readonly language = inject(LanguageService);

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);

  protected readonly roles = signal<RoleDto[]>([]);
  protected readonly rows = signal<PermissionRow[]>([]);

  /**
   * Working copy of the matrix, keyed by role name. Held apart from `baseline` so a save can push
   * only the roles the operator actually touched rather than rewriting all six.
   */
  private readonly grants = signal<Record<string, ReadonlySet<string>>>({});
  private readonly baseline = signal<Record<string, ReadonlySet<string>>>({});

  protected readonly dirtyRoles = computed(() => {
    const current = this.grants();
    const original = this.baseline();

    return Object.keys(current).filter((roleName) => {
      const now = current[roleName];
      const before = original[roleName] ?? new Set<string>();
      return now.size !== before.size || [...now].some((code) => !before.has(code));
    });
  });

  protected readonly hasChanges = computed(() => this.dirtyRoles().length > 0);

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);

    forkJoin({
      roles: this.adminClient.admin_GetRoles(),
      permissions: this.adminClient.admin_GetPermissions(),
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ roles, permissions }) => {
          const roleList = roles.data ?? [];
          this.roles.set(roleList);
          this.rows.set(this.toRows(permissions.data ?? []));

          const snapshot = Object.fromEntries(
            roleList.map((role) => [role.name ?? '', new Set(role.assignedPermissionCodes ?? [])]),
          );

          this.grants.set(snapshot);
          // A second, independent set — sharing the Sets would make every edit look unchanged.
          this.baseline.set(
            Object.fromEntries(
              Object.entries(snapshot).map(([role, codes]) => [role, new Set(codes)]),
            ),
          );
        },
        error: (error: unknown) => {
          this.roles.set([]);
          this.rows.set([]);
          this.toastError(error, 'admin.rolePermissions.messages.loadFailed');
        },
      });
  }

  /** Only active permissions can be granted, so the matrix does not offer retired codes. */
  private toRows(permissions: PermissionDto[]): PermissionRow[] {
    return permissions
      .filter((permission) => permission.isActive && !!permission.code)
      .map((permission) => ({
        code: permission.code!,
        module: permission.module ?? '',
        nameEn: permission.nameEn ?? permission.code!,
        nameAr: permission.nameAr ?? permission.code!,
      }));
  }

  protected label(item: { nameEn: string; nameAr: string }): string {
    return this.language.isRtl() ? item.nameAr : item.nameEn;
  }

  protected roleLabel(role: RoleDto): string {
    return this.language.isRtl() ? (role.nameAr ?? '') : (role.nameEn ?? '');
  }

  protected isGranted(roleName: string, code: string): boolean {
    return this.grants()[roleName]?.has(code) ?? false;
  }

  protected toggle(roleName: string, code: string): void {
    if (this.saving()) {
      return;
    }

    this.grants.update((current) => {
      const next = new Set(current[roleName] ?? []);

      if (next.has(code)) {
        next.delete(code);
      } else {
        next.add(code);
      }

      return { ...current, [roleName]: next };
    });
  }

  protected save(): void {
    const dirty = this.dirtyRoles();

    if (this.saving() || dirty.length === 0) {
      return;
    }

    this.saving.set(true);

    forkJoin(
      dirty.map((roleName) =>
        this.adminClient.admin_AssignRolePermissions(roleName, {
          permissionCodes: [...(this.grants()[roleName] ?? [])],
        }),
      ),
    )
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.baseline.set(
            Object.fromEntries(
              Object.entries(this.grants()).map(([role, codes]) => [role, new Set(codes)]),
            ),
          );

          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('common.success'),
            // Permission claims are baked into the JWT, so a signed-in user keeps the old set
            // until their token is reissued. Saying so here saves a support ticket.
            detail: this.transloco.translate('admin.rolePermissions.messages.saved'),
            life: 6000,
          });
        },
        error: (error: unknown) => this.toastError(error, 'admin.rolePermissions.messages.saveFailed'),
      });
  }

  protected reset(): void {
    if (this.saving()) {
      return;
    }

    this.grants.set(
      Object.fromEntries(
        Object.entries(this.baseline()).map(([role, codes]) => [role, new Set(codes)]),
      ),
    );
  }

  private toastError(error: unknown, fallbackKey: string): void {
    this.messageService.add({
      severity: 'error',
      summary: this.transloco.translate('common.error'),
      detail: apiErrorMessage(error, this.transloco.translate(fallbackKey)),
    });
  }
}
