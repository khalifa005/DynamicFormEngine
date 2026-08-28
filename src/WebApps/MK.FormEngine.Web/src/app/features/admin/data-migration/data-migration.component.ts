import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { finalize } from 'rxjs';

import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { ProgressBarModule } from 'primeng/progressbar';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { TableModule, TableLazyLoadEvent } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';

import {
  FsmsDataMigrationClient,
  FsmsTeamsClient,
  FsmsTemplatesClient,
  MigrationRunListItemDto,
  MigrationSourceDto,
} from '../../../core/api/api-client.generated';
import { apiErrorMessage } from '../../../core/api/api-error';
import { DataMigrationUploadService } from '../../../core/api/data-migration-upload.service';
import { FsmsLookupService } from '../../../core/lookups/fsms-lookup.service';
import { LanguageService } from '../../../core/i18n/language.service';
import { OrgScopeSelectorComponent } from '../../../shared/components/org-scope/org-scope-selector.component';
import { EMPTY_ORG_LOCATION, OrgLocation } from '../../../shared/components/org-scope/org-scope.model';
import { MigrationRunDetailDialogComponent } from './modals/migration-run-detail-dialog.component';
import {
  MIGRATION_MODE,
  MIGRATION_RUN_STATUS,
  formatDuration,
  modeKey,
  runStatusKey,
  runStatusSeverity,
} from './migration-status';

interface Option<T> {
  readonly label: string;
  readonly value: T;
}

const DEFAULT_PAGE_SIZE = 10;

/** How often a run in flight is re-read. Slow enough not to hammer a run that takes minutes. */
const POLL_INTERVAL_MS = 4000;

/** Reference tables are far smaller than this — one page is always the whole list. */
const LOOKUP_PAGE_SIZE = 1000;

/** Only a published form can accept records, so the picker never offers anything else. */
const PUBLISHED_STATUS = 'PUBLISHED';

/**
 * Imports historical data from an external system — Fulcrum today, others later.
 *
 * The screen is deliberately two steps. An import writes hundreds of surveys and closes most of
 * them, so the operator validates first: the same file, the same checks, the same media lookups,
 * nothing written. Only once that reads clean is the import itself worth starting.
 *
 * The operator chooses the target form, because one source exports many different apps and only they
 * know which of ours a given export answers to. Picking the wrong one is therefore the easiest
 * mistake on this screen — which is why a validate run reports how many of the file's columns landed
 * on a field, and names the ones that did not.
 *
 * Media is never uploaded or copied. The archive is placed on the server once, in bulk, in the one
 * folder this page names; the import references each file where it lies. The path is shown rather
 * than described, because guessing at it is exactly how a run ends up finding nothing.
 */
@Component({
  selector: 'app-data-migration',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslocoDirective,
    ButtonModule,
    CardModule,
    MessageModule,
    ProgressBarModule,
    SelectModule,
    SelectButtonModule,
    InputTextModule,
    TableModule,
    TagModule,
    ToastModule,
    TooltipModule,
    OrgScopeSelectorComponent,
    MigrationRunDetailDialogComponent,
  ],
  providers: [MessageService],
  templateUrl: './data-migration.component.html',
})
export class DataMigrationComponent implements OnInit, OnDestroy {
  private readonly migrationClient = inject(FsmsDataMigrationClient);
  private readonly uploadService = inject(DataMigrationUploadService);
  private readonly teamsClient = inject(FsmsTeamsClient);
  private readonly templatesClient = inject(FsmsTemplatesClient);
  private readonly lookups = inject(FsmsLookupService);
  private readonly language = inject(LanguageService);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);

  readonly sources = signal<MigrationSourceDto[]>([]);
  readonly selectedSourceCode = signal<string | null>(null);

  readonly departments = signal<Option<number>[]>([]);
  readonly teams = signal<Option<number>[]>([]);
  readonly templates = signal<Option<number>[]>([]);

  readonly location = signal<OrgLocation>({ ...EMPTY_ORG_LOCATION });
  readonly departmentId = signal<number | null>(null);
  readonly fieldTeamId = signal<number | null>(null);
  readonly templateId = signal<number | null>(null);
  /**
   * Always IMPORT while the mode picker is hidden (see the commented block in the template). The
   * signal stays because the API still accepts VALIDATE and the picker is meant to come back.
   */
  readonly mode = signal<string>(MIGRATION_MODE.import);

  /** Where migrated media is read from, and whether ops have created it yet. */
  readonly archivePath = signal<string>('');
  readonly archiveExists = signal(true);

  readonly file = signal<File | null>(null);

  readonly runs = signal<MigrationRunListItemDto[]>([]);
  readonly totalRuns = signal(0);

  readonly loadingSources = signal(false);
  readonly loadingRuns = signal(false);
  readonly starting = signal(false);

  /** Upload percent while the workbook is in flight; the file can be tens of megabytes. */
  readonly uploadPercent = signal(0);

  readonly detailVisible = signal(false);
  readonly detailRunId = signal<number | null>(null);

  readonly pageSize = DEFAULT_PAGE_SIZE;

  readonly modeOptions: Option<string>[] = [
    { label: 'dataMigration.mode.validate', value: MIGRATION_MODE.validate },
    { label: 'dataMigration.mode.import', value: MIGRATION_MODE.import },
  ];

  readonly runStatusKey = runStatusKey;
  readonly runStatusSeverity = runStatusSeverity;
  readonly modeKey = modeKey;
  readonly formatDuration = formatDuration;

  /** Exposed so the grid can tell one kind of run's outcome column from another's. */
  readonly validateMode = MIGRATION_MODE.validate;
  readonly importMode = MIGRATION_MODE.import;
  readonly backfillMode = MIGRATION_MODE.backfillMedia;

  /** The run whose media retry is in flight, so only that row's button spins. */
  readonly retryingRunId = signal<number | null>(null);

  /**
   * Whether this run can have its media retried: a finished import that came up short a file.
   * A validate run wrote no surveys to attach to, and a run that found everything has nothing to do.
   */
  canRetryMedia(run: MigrationRunListItemDto): boolean {
    return run.mode === this.importMode && (run.isTerminal ?? false) && (run.filesMissing ?? 0) > 0;
  }

  /**
   * Re-reads the workbook this run already stored and attaches whatever has since reached the
   * archive. Nothing is uploaded — see `RetryMigrationMediaCommand`.
   */
  retryMedia(run: MigrationRunListItemDto): void {
    if (this.retryingRunId() !== null || !run.id) {
      return;
    }

    this.retryingRunId.set(run.id);

    this.migrationClient
      .fsmsDataMigration_RetryMedia(run.id)
      .pipe(finalize(() => this.retryingRunId.set(null)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('common.success'),
            detail: this.transloco.translate('dataMigration.retryQueued'),
          });

          // Back to the top so the run just queued is the first thing shown, and polling restarts.
          this.page = 1;
          this.loadRuns();
        },
        error: (error: unknown) =>
          this.showError(apiErrorMessage(error, this.transloco.translate('dataMigration.retryFailed'))),
      });
  }

  readonly selectedSource = computed(() =>
    this.sources().find((source) => source.code === this.selectedSourceCode()) ?? null,
  );

  /** What the file picker should accept, straight from the adapter that will read it. */
  readonly acceptedExtensions = computed(() =>
    (this.selectedSource()?.acceptedExtensions ?? []).join(','),
  );

  /** Every reason the run would be refused, so the button explains itself before it is pressed. */
  readonly blockingReasonKey = computed<string | null>(() => {
    const source = this.selectedSource();

    if (!source) {
      return 'dataMigration.blocked.noSource';
    }

    // Every run reads its media from the archive. Refusing here beats a run that "succeeds" with
    // several hundred photo-less surveys.
    if (!this.archiveExists()) {
      return 'dataMigration.blocked.noArchive';
    }

    if (this.templateId() === null) {
      return 'dataMigration.blocked.noTemplate';
    }

    if (!this.file()) {
      return 'dataMigration.blocked.noFile';
    }

    if (!this.hasPlacement()) {
      return 'dataMigration.blocked.noPlacement';
    }

    return null;
  });

  readonly canStart = computed(() => this.blockingReasonKey() === null && !this.starting());

  private page = 1;
  private pollTimer: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    this.loadSetup();
    this.loadTemplates();
    this.loadDepartments();
    this.loadTeams();
    this.loadRuns();
  }

  ngOnDestroy(): void {
    this.stopPolling();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.file.set(input.files?.[0] ?? null);
  }

  onLocationChange(location: OrgLocation): void {
    this.location.set(location);
  }

  onRunsLazyLoad(event: TableLazyLoadEvent): void {
    const rows = event.rows ?? this.pageSize;
    this.page = event.first !== undefined && rows ? Math.floor(event.first / rows) + 1 : 1;
    this.loadRuns(rows);
  }

  openRun(run: MigrationRunListItemDto): void {
    this.detailRunId.set(run.id ?? null);
    this.detailVisible.set(true);
  }

  start(): void {
    const source = this.selectedSource();
    const file = this.file();

    if (!source || !file || this.starting()) {
      return;
    }

    this.starting.set(true);
    this.uploadPercent.set(0);

    const place = this.location();

    this.uploadService
      .start({
        file,
        sourceCode: source.code ?? '',
        templateId: this.templateId() ?? 0,
        mode: this.mode(),
        cbuCode: place.cbuCode,
        branchCode: place.branchCode,
        operationAreaCode: place.operationAreaCode,
        departmentId: this.departmentId(),
        fieldTeamId: this.fieldTeamId(),
      })
      .pipe(
        finalize(() => {
          this.starting.set(false);
          this.uploadPercent.set(0);
        }),
      )
      .subscribe({
        next: (event) => {
          if (event.kind === 'progress') {
            this.uploadPercent.set(event.percent);
            return;
          }

          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('common.success'),
            detail: this.transloco.translate('dataMigration.queued'),
          });

          // Back to the top of the list so the run just queued is the first thing shown.
          this.page = 1;
          this.loadRuns();
        },
        // The service throws with the server's own reason when it has one.
        error: (error: Error) => this.showError(error.message),
      });
  }

  private loadSetup(): void {
    this.loadingSources.set(true);

    this.migrationClient
      .fsmsDataMigration_GetSetup()
      .pipe(finalize(() => this.loadingSources.set(false)))
      .subscribe({
        next: (result) => {
          const sources = result.data?.sources ?? [];
          this.sources.set(sources);
          this.archivePath.set(result.data?.archivePath ?? '');
          this.archiveExists.set(result.data?.archiveExists ?? false);

          // One source is the common case; picking it saves a click that has no alternative.
          if (sources.length === 1) {
            this.selectedSourceCode.set(sources[0].code ?? null);
          }
        },
        error: () => {
          this.sources.set([]);
          this.archiveExists.set(false);
        },
      });
  }

  /**
   * The forms a run may import into. Published only — nothing else can accept a record — and
   * narrowed to the caller's territory by the endpoint itself.
   */
  private loadTemplates(): void {
    this.templatesClient
      .fsmsTemplates_GetTemplates(1, LOOKUP_PAGE_SIZE, undefined, PUBLISHED_STATUS, undefined, undefined, undefined)
      .subscribe({
        next: (result) =>
          this.templates.set(
            (result.data?.items ?? []).map((template) => ({
              label: `${this.localized(template.templateNameEn, template.templateNameAr)} (${template.templateCode ?? ''})`,
              value: template.templateId ?? 0,
            })),
          ),
        error: () => this.templates.set([]),
      });
  }

  private loadDepartments(): void {
    this.lookups.getDepartments().subscribe({
      next: (departments) =>
        this.departments.set(
          departments.map((department) => ({
            label: this.localized(department.nameEn, department.nameAr),
            value: department.id ?? 0,
          })),
        ),
      error: () => this.departments.set([]),
    });
  }

  private loadTeams(): void {
    this.teamsClient
      .fsmsTeams_GetTeamsPaged(1, LOOKUP_PAGE_SIZE, undefined, true, undefined, undefined, undefined, undefined)
      .subscribe({
        next: (result) =>
          this.teams.set(
            (result.data?.items ?? []).map((team) => ({
              label: `${team.name ?? ''} (${team.userCode ?? ''})`,
              value: team.teamId ?? 0,
            })),
          ),
        error: () => this.teams.set([]),
      });
  }

  private loadRuns(rows = this.pageSize): void {
    this.loadingRuns.set(true);

    this.migrationClient
      .fsmsDataMigration_GetRuns(this.page, rows, undefined, undefined)
      .pipe(finalize(() => this.loadingRuns.set(false)))
      .subscribe({
        next: (result) => {
          this.runs.set(result.data?.items ?? []);
          this.totalRuns.set(result.data?.totalCount ?? 0);
          this.syncPolling();
        },
        error: () => {
          this.runs.set([]);
          this.totalRuns.set(0);
          this.stopPolling();
        },
      });
  }

  /**
   * Polls only while something is actually moving, and stops the moment nothing is. A run takes
   * minutes, so the alternative — the operator refreshing to find out — is exactly the thing the
   * progress column exists to avoid.
   */
  private syncPolling(): void {
    const inFlight = this.runs().some(
      (run) => run.status === MIGRATION_RUN_STATUS.pending || run.status === MIGRATION_RUN_STATUS.running,
    );

    if (!inFlight) {
      this.stopPolling();
      return;
    }

    this.pollTimer ??= setInterval(() => this.refreshRunsQuietly(), POLL_INTERVAL_MS);
  }

  /** A poll must not flash the table's spinner — only a user-driven load does that. */
  private refreshRunsQuietly(): void {
    this.migrationClient.fsmsDataMigration_GetRuns(this.page, this.pageSize, undefined, undefined).subscribe({
      next: (result) => {
        this.runs.set(result.data?.items ?? []);
        this.totalRuns.set(result.data?.totalCount ?? 0);
        this.syncPolling();
      },
      error: () => this.stopPolling(),
    });
  }

  private stopPolling(): void {
    if (this.pollTimer !== null) {
      clearInterval(this.pollTimer);
      this.pollTimer = null;
    }
  }

  private hasPlacement(): boolean {
    const place = this.location();
    return Boolean(place.cbuCode || place.branchCode || place.operationAreaCode) || this.departmentId() !== null;
  }


  private localized(nameEn: string | undefined, nameAr: string | undefined): string {
    return (this.language.current() === 'ar' ? nameAr : nameEn) ?? nameEn ?? nameAr ?? '';
  }

  private showError(detail?: string): void {
    this.messageService.add({
      severity: 'error',
      summary: this.transloco.translate('common.error'),
      detail: detail ?? this.transloco.translate('dataMigration.startFailed'),
    });
  }
}
