/**
 * Status slice colours keyed by the API's real `SurveyStatuses` vocabulary. Mirrors
 * `dashboard.service.ts`'s `STATUS_COLORS` so a status reads the same colour everywhere in Reports.
 */
export const SURVEY_STATUS_COLORS: Readonly<Record<string, string>> = {
  CREATED: '#64748b',
  ASSIGNED: '#06b6d4',
  IN_PROGRESS: '#3b82f6',
  SUBMITTED: '#8b5cf6',
  UNDER_REVIEW: '#f59e0b',
  APPROVED: '#10b981',
  RETURNED: '#ef4444',
  EXPIRED: '#94a3b8',
};

export const FALLBACK_SURVEY_STATUS_COLOR = '#94a3b8';
