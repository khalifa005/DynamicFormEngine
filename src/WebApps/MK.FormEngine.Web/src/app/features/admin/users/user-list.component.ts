import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { finalize } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';

import { FsmsUsersClient, UserListItemDto } from '../../../core/api/api-client.generated';
import { UserFormDialogComponent } from './modals/user-form-dialog.component';
import { UserResetPasswordDialogComponent } from './modals/user-reset-password-dialog.component';

/** Back-office account register — server-side paged, searched and filtered. */
@Component({
  selector: 'app-user-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslocoDirective,
    ButtonModule,
    ConfirmDialogModule,
    IconFieldModule,
    InputIconModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
    ToastModule,
    TooltipModule,
    UserFormDialogComponent,
    UserResetPasswordDialogComponent,
  ],
  providers: [MessageService, ConfirmationService],
  templateUrl: './user-list.component.html',
})
export class UserListComponent {
  private static readonly DEFAULT_PAGE_SIZE = 10;

  private readonly usersClient = inject(FsmsUsersClient);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly transloco = inject(TranslocoService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly users = signal<UserListItemDto[]>([]);
  protected readonly totalRecords = signal(0);
  protected readonly loading = signal(false);
  protected readonly busyUserId = signal<string | null>(null);

  protected searchTerm = '';
  protected statusFilter: boolean | null = null;

  protected readonly formVisible = signal(false);
  protected readonly resetVisible = signal(false);
  protected readonly selectedUser = signal<UserListItemDto | null>(null);

  /** Rebuilt on every language change — translating once at construction can outrun the loader. */
  protected readonly statusOptions = signal<{ label: string; value: boolean }[]>([]);

  private page = 1;
  private pageSize = UserListComponent.DEFAULT_PAGE_SIZE;

  constructor() {
    this.buildStatusOptions();
    this.transloco.langChanges$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.buildStatusOptions());
  }

  private buildStatusOptions(): void {
    this.statusOptions.set([
      { label: this.transloco.translate('users.enabled'), value: true },
      { label: this.transloco.translate('users.disabled'), value: false },
    ]);
  }

  protected load(event?: TableLazyLoadEvent): void {
    if (event) {
      this.pageSize = event.rows ?? UserListComponent.DEFAULT_PAGE_SIZE;
      this.page = event.first !== undefined ? Math.floor(event.first / this.pageSize) + 1 : 1;
    }

    this.loading.set(true);

    this.usersClient
      .fsmsUsers_GetUsers(
        this.page,
        this.pageSize,
        this.searchTerm.trim() || undefined,
        undefined,
        this.statusFilter ?? undefined
      )
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: res => {
          this.users.set(res.data?.items ?? []);
          this.totalRecords.set(res.data?.totalCount ?? 0);
        },
        error: () => {
          this.users.set([]);
          this.totalRecords.set(0);
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: this.transloco.translate('users.loadError'),
          });
        },
      });
  }

  protected onSearch(): void {
    this.page = 1;
    this.load();
  }

  protected clearSearch(): void {
    this.searchTerm = '';
    this.onSearch();
  }

  protected openNew(): void {
    this.selectedUser.set(null);
    this.formVisible.set(true);
  }

  protected openEdit(user: UserListItemDto): void {
    this.selectedUser.set(user);
    this.formVisible.set(true);
  }

  protected openReset(user: UserListItemDto): void {
    this.selectedUser.set(user);
    this.resetVisible.set(true);
  }

  protected toggleStatus(user: UserListItemDto): void {
    if (!user.id || this.busyUserId()) {
      return;
    }

    const enabling = !user.isEnabled;

    this.confirmationService.confirm({
      message: this.transloco.translate(enabling ? 'users.confirmEnable' : 'users.confirmDisable'),
      header: this.transloco.translate(enabling ? 'users.enable' : 'users.disable'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.transloco.translate('common.confirm'),
      rejectLabel: this.transloco.translate('common.cancel'),
      accept: () => this.setStatus(user.id!, enabling),
    });
  }

  private setStatus(userId: string, isEnabled: boolean): void {
    this.busyUserId.set(userId);

    this.usersClient
      .fsmsUsers_SetStatus(userId, { userId, isEnabled })
      .pipe(finalize(() => this.busyUserId.set(null)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('common.success'),
            detail: this.transloco.translate('users.statusUpdated'),
          });
          this.load();
        },
        error: () =>
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: this.transloco.translate('users.statusError'),
          }),
      });
  }
}
