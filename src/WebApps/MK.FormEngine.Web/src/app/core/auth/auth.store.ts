import { Injectable, computed, signal, inject, effect } from '@angular/core';
import { AuthToken, PersistedSession } from './auth.model';
import { NgxPermissionsService, NgxRolesService } from 'ngx-permissions';

const STORAGE_KEY = 'fsms.session';

/**
 * Central auth state.
 *
 * Security note: the backend returns tokens in the response body (not an
 * HttpOnly cookie), so a persisted session is required to survive reloads.
 * We keep the short-lived access token in memory only and persist just the
 * refresh token + profile metadata to localStorage.
 */
@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly accessToken = signal<string | null>(null);
  private readonly refreshToken = signal<string | null>(null);
  private readonly _userName = signal<string | null>(null);
  private readonly _roles = signal<readonly string[]>([]);
  private readonly _permissions = signal<readonly string[]>([]);

  private readonly ngxPermissions = inject(NgxPermissionsService);
  private readonly ngxRoles = inject(NgxRolesService);

  readonly isAuthenticated = computed(
    () => this.accessToken() !== null || this.refreshToken() !== null,
  );
  readonly userName = this._userName.asReadonly();
  readonly roles = this._roles.asReadonly();
  readonly permissions = this._permissions.asReadonly();

  constructor() {
    this.restore();

    effect(() => {
      const perms = this._permissions();
      const currentRoles = this._roles();
      this.syncPermissionsAndRoles(currentRoles, perms);
    });
  }

  get currentAccessToken(): string | null {
    return this.accessToken();
  }

  get currentRefreshToken(): string | null {
    return this.refreshToken();
  }

  setSession(token: AuthToken, userName: string, persist: boolean): void {
    this.accessToken.set(token.accessToken);
    this.refreshToken.set(token.refreshToken);
    this._userName.set(userName);
    this._roles.set(token.roles);
    this._permissions.set(token.permissions);

    this.syncPermissionsAndRoles(token.roles, token.permissions);

    if (persist) {
      const payload: PersistedSession = {
        accessToken: token.accessToken,
        refreshToken: token.refreshToken,
        userName,
        roles: token.roles,
        permissions: token.permissions,
      };
      localStorage.setItem(STORAGE_KEY, JSON.stringify(payload));
    }
  }

  /** Update tokens after a silent refresh without touching profile metadata. */
  updateTokens(token: AuthToken): void {
    this.accessToken.set(token.accessToken);
    this.refreshToken.set(token.refreshToken);
    const raw = localStorage.getItem(STORAGE_KEY);
    if (raw) {
      const parsed = JSON.parse(raw) as PersistedSession;
      localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          ...parsed,
          accessToken: token.accessToken,
          refreshToken: token.refreshToken,
        }),
      );
    }
  }

  clear(): void {
    this.accessToken.set(null);
    this.refreshToken.set(null);
    this._userName.set(null);
    this._roles.set([]);
    this._permissions.set([]);
    this.ngxPermissions.flushPermissions();
    this.ngxRoles.flushRoles();
    localStorage.removeItem(STORAGE_KEY);
  }

  private restore(): void {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return;
    }
    try {
      const parsed = JSON.parse(raw) as PersistedSession;
      if (parsed.accessToken) {
        this.accessToken.set(parsed.accessToken);
      }
      this.refreshToken.set(parsed.refreshToken);
      this._userName.set(parsed.userName);
      this._roles.set(parsed.roles ?? []);
      this._permissions.set(parsed.permissions ?? []);

      this.syncPermissionsAndRoles(parsed.roles ?? [], parsed.permissions ?? []);
    } catch {
      localStorage.removeItem(STORAGE_KEY);
    }
  }

  private syncPermissionsAndRoles(roles: readonly string[], permissions: readonly string[]): void {
    const allPermissions = Array.from(new Set([...(permissions ?? []), ...(roles ?? [])]));
    this.ngxPermissions.flushPermissions();
    if (allPermissions.length > 0) {
      this.ngxPermissions.loadPermissions(allPermissions);
    }

    this.ngxRoles.flushRoles();
    if (roles && roles.length > 0) {
      roles.forEach((role) => {
        this.ngxRoles.addRole(role, () => true);
      });
    }
  }
}
