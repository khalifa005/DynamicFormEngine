import { Tag } from 'primeng/tag';

/** Run statuses as the API defines them (`KH.Domain.Constants.Fsms.MigrationRunStatuses`). */
export const MIGRATION_RUN_STATUS = {
  pending: 'PENDING',
  running: 'RUNNING',
  completed: 'COMPLETED',
  failed: 'FAILED',
} as const;

/** Per-record outcomes (`MigrationRecordStatuses`). */
export const MIGRATION_RECORD_STATUS = {
  imported: 'IMPORTED',
  skipped: 'SKIPPED',
  failed: 'FAILED',
  validated: 'VALIDATED',
} as const;

/** How far a run goes (`MigrationModes`). */
export const MIGRATION_MODE = {
  validate: 'VALIDATE',
  import: 'IMPORT',
  backfillMedia: 'BACKFILL_MEDIA',
} as const;


type TagSeverity = Tag['severity'];

/**
 * Colour per run status. `SKIPPED` and `VALIDATED` are deliberately neutral rather than warnings —
 * a re-run that skips everything it already imported is the system working, and telling the
 * operator off for it would train them to ignore the one colour that does mean something.
 */
export function runStatusSeverity(status: string | undefined): TagSeverity {
  switch (status) {
    case MIGRATION_RUN_STATUS.completed:
      return 'success';
    case MIGRATION_RUN_STATUS.running:
      return 'info';
    case MIGRATION_RUN_STATUS.failed:
      return 'danger';
    default:
      return 'secondary';
  }
}

export function recordStatusSeverity(status: string | undefined): TagSeverity {
  switch (status) {
    case MIGRATION_RECORD_STATUS.imported:
      return 'success';
    case MIGRATION_RECORD_STATUS.failed:
      return 'danger';
    case MIGRATION_RECORD_STATUS.validated:
      return 'info';
    default:
      return 'secondary';
  }
}

/** Translation key for a run status, so no label is ever hardcoded in a template. */
export function runStatusKey(status: string | undefined): string {
  return `dataMigration.runStatus.${(status ?? '').toLowerCase() || 'unknown'}`;
}

export function recordStatusKey(status: string | undefined): string {
  return `dataMigration.recordStatus.${(status ?? '').toLowerCase() || 'unknown'}`;
}

export function modeKey(mode: string | undefined): string {
  return `dataMigration.mode.${(mode ?? '').toLowerCase() || 'unknown'}`;
}

/**
 * A duration in seconds as `1h 04m 12s` / `4m 12s` / `12s`.
 *
 * Unit-free and so not localized: the operator's question is "did this take two minutes or forty",
 * and the shape answers it at a glance in either language. Seconds are dropped past the hour mark,
 * where they are noise.
 */
export function formatDuration(seconds: number | undefined | null): string {
  if (seconds === null || seconds === undefined || !Number.isFinite(seconds) || seconds < 0) {
    return '';
  }

  const total = Math.round(seconds);
  const hours = Math.floor(total / 3600);
  const minutes = Math.floor((total % 3600) / 60);
  const secs = total % 60;

  if (hours > 0) {
    return `${hours}h ${String(minutes).padStart(2, '0')}m`;
  }

  return minutes > 0 ? `${minutes}m ${String(secs).padStart(2, '0')}s` : `${secs}s`;
}

