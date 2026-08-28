import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import {
  AppConfig,
  DEFAULT_DASHBOARD_KPIS,
  DEFAULT_DASHBOARD_SECTIONS,
  DEFAULT_SSO,
} from './app-config.model';

const CONFIG_URL = 'config/app-config.json';

const FALLBACK_CONFIG: AppConfig = {
  apiBaseUrl: 'http://localhost:5157',
  defaultLanguage: 'ar',
  availableLanguages: ['ar', 'en'],
  tokenRefreshEnabled: true,
  appName: 'FSMS',
  fakeLogin: false,
  dashboardSections: DEFAULT_DASHBOARD_SECTIONS,
  dashboardKpis: DEFAULT_DASHBOARD_KPIS,
  sso: DEFAULT_SSO,
};

/**
 * Loads runtime configuration from a static JSON file so the backend URL and
 * environment settings can change without rebuilding the app.
 */
@Injectable({ providedIn: 'root' })
export class AppConfigService {
  private readonly http = inject(HttpClient);
  private readonly config = signal<AppConfig>(FALLBACK_CONFIG);

  get snapshot(): AppConfig {
    return this.config();
  }

  async load(): Promise<void> {
    try {
      const loaded = await firstValueFrom(this.http.get<Partial<AppConfig>>(CONFIG_URL));
      this.config.set(merge(loaded));
    } catch {
      // Keep fallback config; the app can still start in a degraded state.
      this.config.set(FALLBACK_CONFIG);
    }
  }
}

/**
 * Merges the loaded file over the fallback. The nested flag blocks are merged key by key rather
 * than replaced wholesale: a top-level spread would let a file listing one flag leave every other
 * flag `undefined`, which reads as `false` and would silently hide the rest of the dashboard.
 */
function merge(loaded: Partial<AppConfig>): AppConfig {
  return {
    ...FALLBACK_CONFIG,
    ...loaded,
    dashboardSections: {
      ...FALLBACK_CONFIG.dashboardSections,
      ...(loaded.dashboardSections ?? {}),
    },
    dashboardKpis: {
      ...FALLBACK_CONFIG.dashboardKpis,
      ...(loaded.dashboardKpis ?? {}),
    },
    sso: {
      ...FALLBACK_CONFIG.sso,
      ...(loaded.sso ?? {}),
    },
  };
}
