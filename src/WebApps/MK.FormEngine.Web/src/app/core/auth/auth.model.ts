/** Response payload from POST /api/v1/auth/login (AuthTokenDto). */
export interface AuthToken {
  readonly accessToken: string;
  readonly refreshToken: string;
  readonly expiresInSeconds: number;
  /**
   * Who signed in, as the server sees it. An SSO sign-in never fills in a form, so the user name
   * cannot be taken from one — it comes back with the token instead.
   */
  readonly userName: string;
  readonly roles: readonly string[];
  readonly permissions: readonly string[];
}

/** Response payload from GET /api/v1/auth/sso/status (SsoStatusDto). */
export interface SsoStatus {
  readonly enabled: boolean;
  readonly administratorLocalLoginAllowed: boolean;
}

export interface LoginRequest {
  readonly userName: string;
  readonly password: string;
}

export interface RefreshRequest {
  readonly refreshToken: string;
}

/** Persisted session slice. */
export interface PersistedSession {
  readonly accessToken?: string;
  readonly refreshToken: string;
  readonly userName: string;
  readonly roles: readonly string[];
  readonly permissions: readonly string[];
}

