import { Component, computed, effect, inject, input, model, output, signal, untracked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective } from '@jsverse/transloco';
import { finalize } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { TagModule } from 'primeng/tag';

import {
  FsmsDataMigrationClient,
  MigrationRunListItemDto,
  MigrationRunRecordDto,
} from '../../../../core/api/api-client.generated';
import {
  MIGRATION_MODE,
  MIGRATION_RECORD_STATUS,
  formatDuration,
  recordStatusKey,
  recordStatusSeverity,
  runStatusKey,
  runStatusSeverity,
} from '../migration-status';

/**
 * One entry in the outcome filter. `missingMedia` is not a status — a record can import perfectly
 * and still be short a photo — so it filters on the file counts instead.
 */
interface RecordFilterOption {
  readonly labelKey: string;
  readonly value: string;
  readonly status: string | null;
  readonly missingMedia: boolean;
}

const ALL_RECORDS = 'ALL';
const MISSING_MEDIA = 'MISSING_MEDIA';

const DEFAULT_PAGE_SIZE = 20;

/**
 * What one import run did, record by record. Its own component rather than markup inside the page,
 * and it pages server-side: a run holds a row per source record, so a file of several hundred would
 * otherwise arrive whole to show twenty.
 *
 * The "failures only" filter is the reason this screen exists at all. An operator does not read 948
 * successful rows — they open this to find the handful that did not land and read the message
 * against each.
 */
@Component({
  selector: 'app-migration-run-detail-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslocoDirective,
    ButtonModule,
    DialogModule,
    SelectModule,
    TableModule,
    TagModule,
  ],
  templateUrl: './migration-run-detail-dialog.component.html',
})
export class MigrationRunDetailDialogComponent {
  private readonly migrationClient = inject(FsmsDataMigrationClient);

  readonly visible = model(false);

  /** The run to show. Changing it reloads from page one. */
  readonly runId = input<number | null>(null);

  readonly closed = output<void>();

  readonly run = signal<MigrationRunListItemDto | null>(null);
  readonly records = signal<MigrationRunRecordDto[]>([]);
  readonly totalRecords = signal(0);
  readonly loading = signal(false);

  /** The selected filter's key — see {@link filterOptions}. */
  readonly filter = signal<string>(ALL_RECORDS);

  readonly pageSize = DEFAULT_PAGE_SIZE;

  readonly filterOptions: RecordFilterOption[] = [
    { labelKey: 'dataMigration.detail.allRecords', value: ALL_RECORDS, status: null, missingMedia: false },
    { labelKey: 'dataMigration.detail.missingMedia', value: MISSING_MEDIA, status: null, missingMedia: true },
    { labelKey: 'dataMigration.recordStatus.failed', value: MIGRATION_RECORD_STATUS.failed, status: MIGRATION_RECORD_STATUS.failed, missingMedia: false },
    { labelKey: 'dataMigration.recordStatus.imported', value: MIGRATION_RECORD_STATUS.imported, status: MIGRATION_RECORD_STATUS.imported, missingMedia: false },
    { labelKey: 'dataMigration.recordStatus.skipped', value: MIGRATION_RECORD_STATUS.skipped, status: MIGRATION_RECORD_STATUS.skipped, missingMedia: false },
    { labelKey: 'dataMigration.recordStatus.validated', value: MIGRATION_RECORD_STATUS.validated, status: MIGRATION_RECORD_STATUS.validated, missingMedia: false },
  ];

  /** A validate run reports what it checked; an import reports what it wrote. */
  readonly isValidateRun = computed(() => this.run()?.mode === MIGRATION_MODE.validate);

  /**
   * A backfill wrote no surveys — only media onto surveys that already existed. Its Imported column
   * therefore counts surveys that gained a file, and its Skipped column counts the far larger number
   * that needed nothing, so both want different words from an import's.
   */
  readonly isBackfillRun = computed(() => this.run()?.mode === MIGRATION_MODE.backfillMedia);

  /** The ignored-column list, collapsed by default — it is the longest of the three and usually noise. */
  readonly showIgnored = signal(false);

  readonly runStatusKey = runStatusKey;
  readonly runStatusSeverity = runStatusSeverity;
  readonly recordStatusKey = recordStatusKey;
  readonly recordStatusSeverity = recordStatusSeverity;
  readonly formatDuration = formatDuration;


  private page = 1;

  constructor() {
    // A different run means a different table: reset to page one rather than asking for page four
    // of a run that may not have four pages.
    //
    // The reset runs untracked because `load()` reads the `filter` signal. Tracked, that made the
    // filter a dependency of this effect, so choosing "Skipped" re-ran the effect, which set the
    // filter straight back to ALL and reloaded unfiltered — the filtered request went out and was
    // then overwritten by an unfiltered one, which is why the table came back full of IMPORTED rows.
    effect(() => {
      const id = this.runId();
      const open = this.visible();

      if (!open || id === null) {
        return;
      }

      untracked(() => {
        this.page = 1;
        this.filter.set(ALL_RECORDS);
        this.showIgnored.set(false);
        this.load();
      });
    });
  }

  onLazyLoad(event: TableLazyLoadEvent): void {
    const rows = event.rows ?? this.pageSize;
    this.page = event.first !== undefined && rows ? Math.floor(event.first / rows) + 1 : 1;
    this.load(rows);
  }

  /**
   * Takes the new value from the event rather than re-reading the signal. The previous version read
   * the signal inside the change handler, which PrimeNG had not necessarily written yet — so the
   * request carried the *previous* selection and the table came back unfiltered.
   */
  toggleIgnored(): void {
    this.showIgnored.update((open) => !open);
  }

  onFilterChange(value: string): void {
    this.filter.set(value);
    this.page = 1;
    this.load();
  }

  close(): void {
    this.visible.set(false);
    this.closed.emit();
  }

  private load(rows = this.pageSize): void {
    const id = this.runId();
    if (id === null) {
      return;
    }

    this.loading.set(true);

    const selected = this.filterOptions.find((option) => option.value === this.filter());

    this.migrationClient
      .fsmsDataMigration_GetRunById(
        id,
        this.page,
        rows,
        selected?.status ?? undefined,
        selected?.missingMedia ? true : undefined,
      )
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (result) => {
          this.run.set(result.data?.run ?? null);
          this.records.set(result.data?.records?.items ?? []);
          this.totalRecords.set(result.data?.records?.totalCount ?? 0);
        },
        error: () => {
          this.records.set([]);
          this.totalRecords.set(0);
        },
      });
  }
}
