/**
 * Survey lifecycle vocabulary, mirrored from the API's `SurveyStatuses` / `SurveySources` /
 * `FilledByRoles` constants. The API stores these exact strings; the UI labels them through
 * `surveys.status.*` / `surveys.source.*` / `surveys.role.*` translation keys.
 */
export const SurveyStatus = {
  Created: 'CREATED',
  Assigned: 'ASSIGNED',
  InProgress: 'IN_PROGRESS',
  Submitted: 'SUBMITTED',
  /**
   * Legacy. Filled surveys used to be "received" into this state before the back office could act
   * on them; that step was removed and `Submitted` is now the awaiting-review state. Kept so rows
   * written before the change still render and filter.
   */
  UnderReview: 'UNDER_REVIEW',
  Approved: 'APPROVED',
  Returned: 'RETURNED',
  Expired: 'EXPIRED',
} as const;

export type SurveyStatusValue = (typeof SurveyStatus)[keyof typeof SurveyStatus];

export const SURVEY_STATUSES: readonly SurveyStatusValue[] = [
  SurveyStatus.Created,
  SurveyStatus.Assigned,
  SurveyStatus.InProgress,
  SurveyStatus.Submitted,
  SurveyStatus.UnderReview,
  SurveyStatus.Approved,
  SurveyStatus.Returned,
  SurveyStatus.Expired,
];

export const SurveySource = {
  Api: 'API',
  Manual: 'MANUAL',
  Import: 'IMPORT',
  Team: 'TEAM',
} as const;

export const SURVEY_SOURCES: readonly string[] = [
  SurveySource.Api,
  SurveySource.Manual,
  SurveySource.Import,
  SurveySource.Team,
];

export const FilledByRole = {
  FieldTeam: 'FIELD_TEAM',
  BackOffice: 'BACK_OFFICE',
} as const;

/**
 * The return reasons the API seeds into `LKP_RETURN_REASON`. The table is editable, so the picker
 * reads it from the API rather than from this list — these are here only so the worklist can give
 * the shipped causes their own colour and a stable filter list. An operator-added code still
 * renders, just in the default colour.
 */
export const SurveyReturnReason = {
  MissingData: 'MISSING_DATA',
  WrongLocation: 'WRONG_LOCATION',
  PoorPhoto: 'POOR_PHOTO',
  NeedsRevisit: 'NEEDS_REVISIT',
  WrongTeam: 'WRONG_TEAM',
  Other: 'OTHER',
} as const;

export const SURVEY_RETURN_REASONS: readonly string[] = [
  SurveyReturnReason.MissingData,
  SurveyReturnReason.WrongLocation,
  SurveyReturnReason.PoorPhoto,
  SurveyReturnReason.NeedsRevisit,
  SurveyReturnReason.WrongTeam,
  SurveyReturnReason.Other,
];

export type SurveyTagSeverity = 'success' | 'secondary' | 'info' | 'warn' | 'danger' | 'contrast';

/** Colour of the status tag. Terminal-good is green, terminal-bad grey, in-flight blue/amber. */
export function surveyStatusSeverity(status?: string): SurveyTagSeverity {
  switch (status) {
    case SurveyStatus.Created:
      return 'secondary';
    case SurveyStatus.Assigned:
      return 'info';
    case SurveyStatus.InProgress:
      return 'info';
    case SurveyStatus.Submitted:
      return 'warn';
    case SurveyStatus.UnderReview:
      return 'warn';
    case SurveyStatus.Approved:
      return 'success';
    case SurveyStatus.Returned:
      return 'danger';
    case SurveyStatus.Expired:
      return 'contrast';
    default:
      return 'secondary';
  }
}

/**
 * Colour of the return-reason tag. A returned survey is already flagged red by its status, so the
 * reason sits beside it in a shade that says what kind of problem it is without competing: amber
 * for "fix what you sent", red for "go back to site".
 */
export function surveyReturnReasonSeverity(reasonCode?: string): SurveyTagSeverity {
  switch (reasonCode) {
    case SurveyReturnReason.MissingData:
    case SurveyReturnReason.PoorPhoto:
      return 'warn';
    case SurveyReturnReason.WrongLocation:
    case SurveyReturnReason.NeedsRevisit:
      return 'danger';
    case SurveyReturnReason.WrongTeam:
      return 'info';
    default:
      return 'secondary';
  }
}

/** The lifecycle actions the API will accept for a survey in this status. */
export type SurveyAction = 'allocate' | 'fill' | 'complete' | 'return' | 'expire';

const ACTION_PRECONDITIONS: Record<SurveyAction, readonly string[]> = {
  // Re-allocation moves the survey to a different team, and only an unfilled survey may move —
  // once answers exist they belong to the crew that collected them. See `canReallocate`, which
  // also checks the fill count; these three statuses are simply the ones that can carry no fill.
  allocate: [SurveyStatus.Created, SurveyStatus.Assigned, SurveyStatus.InProgress],
  // The back office fills its own part of the survey. Everything up to completion accepts a fill;
  // an approved or expired survey is closed.
  fill: [
    SurveyStatus.Created,
    SurveyStatus.Assigned,
    SurveyStatus.InProgress,
    SurveyStatus.Submitted,
    SurveyStatus.UnderReview,
    SurveyStatus.Returned,
  ],
  // A reviewer acts straight on a filled survey — there is no receive step in between.
  // `UnderReview` is listed only for surveys left in that retired state before it was removed,
  // which the API still accepts.
  complete: [SurveyStatus.Submitted, SurveyStatus.UnderReview],
  return: [SurveyStatus.Submitted, SurveyStatus.UnderReview],
  // Expiring is a correction the back office can apply at any point in the lifecycle, including
  // while the survey sits in review. Only a completed survey is off limits — it is a finished
  // record — and an already expired one has nowhere left to go.
  expire: [
    SurveyStatus.Created,
    SurveyStatus.Assigned,
    SurveyStatus.InProgress,
    SurveyStatus.Submitted,
    SurveyStatus.UnderReview,
    SurveyStatus.Returned,
  ],
};

/**
 * Whether the domain would accept this transition. The server guards it regardless — this only
 * keeps the menu from offering a click that is guaranteed to come back a 400.
 */
export function canRunSurveyAction(action: SurveyAction, status?: string): boolean {
  return status !== undefined && ACTION_PRECONDITIONS[action].includes(status);
}

/**
 * Whether the survey may still be moved onto a newer template version. A closed survey's answers
 * were given against the version it was pinned to, so re-pointing it would misdescribe them.
 */
export function canMigrateVersion(status?: string): boolean {
  return status !== undefined && status !== SurveyStatus.Approved && status !== SurveyStatus.Expired;
}

/**
 * Whether the survey can still be handed to a different team. A filled survey is locked to the
 * crew that collected the answers, so the fill count decides it as much as the status does.
 */
export function canReallocate(status?: string, submissionCount?: number): boolean {
  return canRunSurveyAction('allocate', status) && (submissionCount ?? 0) === 0;
}

/**
 * True when the latest fill landed after a return — i.e. the crew delivered rework.
 * `RecordFill` refreshes `submittedDate`; `Return` stamps `returnedDate`. While still returned,
 * the old fill date is earlier than the return, so this stays false.
 */
export function wasSubmittedAgain(survey: {
  submittedDate?: Date | string | null;
  returnedDate?: Date | string | null;
}): boolean {
  if (!survey.submittedDate || !survey.returnedDate) {
    return false;
  }

  return new Date(survey.submittedDate).getTime() > new Date(survey.returnedDate).getTime();
}
