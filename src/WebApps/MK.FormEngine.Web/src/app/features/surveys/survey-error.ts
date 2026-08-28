import { apiErrorMessage } from '../../core/api/api-error';

/**
 * Survey-flow alias of the shared API error reader. The behaviour is not survey-specific, so the
 * implementation lives in `core/api/api-error`; this keeps the existing call sites reading in the
 * language of the feature.
 */
export function surveyErrorMessage(error: unknown, fallback: string): string {
  return apiErrorMessage(error, fallback);
}
