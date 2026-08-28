/** Mirrors the backend Shared.Core.Common.Result<T> envelope. */
export interface ApiError {
  readonly code: string | null;
  readonly message: string | null;
  readonly httpStatusCode: number | null;
  readonly ivrMessageEn: string | null;
  readonly ivrMessageAr: string | null;
}

export interface ApiResult<T> {
  readonly isSuccess: boolean;
  readonly data: T | null;
  readonly errors: ApiError[];
  readonly correlationId: string | null;
}

export function firstErrorMessage(result: ApiResult<unknown> | null | undefined): string | null {
  return result?.errors?.[0]?.message ?? null;
}
