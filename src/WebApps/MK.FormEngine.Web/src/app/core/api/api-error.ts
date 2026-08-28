import { SwaggerException } from './api-client.generated';

/**
 * Pulls the server's own message out of a failed call. The API answers every error with a
 * `Result<T>` envelope carrying `errors[].message`, and those messages are usually the domain
 * guards ("Only an UNDER_REVIEW survey can be completed") — far more useful to show than a generic
 * failure string.
 */
export function apiErrorMessage(error: unknown, fallback: string): string {
  if (!(error instanceof SwaggerException) || !error.response) {
    return fallback;
  }

  try {
    const body = JSON.parse(error.response) as { errors?: { message?: string }[] };
    const message = body?.errors?.find((e) => !!e?.message)?.message;
    return message?.trim() || fallback;
  } catch {
    return fallback;
  }
}
