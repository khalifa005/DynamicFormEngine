import { Injectable, computed, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { AppLanguage } from '../config/app-config.model';

const LANG_STORAGE_KEY = 'fsms.lang';
const RTL_LANGS: readonly AppLanguage[] = ['ar'];

/** Owns the active language and keeps the document direction/lang in sync. */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly transloco = inject(TranslocoService);
  private readonly _lang = signal<AppLanguage>('ar');

  readonly current = this._lang.asReadonly();
  readonly isRtl = computed(() => RTL_LANGS.includes(this._lang()));
  readonly direction = computed<'rtl' | 'ltr'>(() => (this.isRtl() ? 'rtl' : 'ltr'));

  /** Called once during bootstrap after config is loaded. */
  init(defaultLang: AppLanguage): void {
    const stored = localStorage.getItem(LANG_STORAGE_KEY) as AppLanguage | null;
    this.setLanguage(stored ?? defaultLang);
  }

  setLanguage(lang: AppLanguage): void {
    this._lang.set(lang);
    this.transloco.setActiveLang(lang);
    localStorage.setItem(LANG_STORAGE_KEY, lang);
    this.applyToDocument();
  }

  toggle(): void {
    this.setLanguage(this._lang() === 'ar' ? 'en' : 'ar');
  }

  private applyToDocument(): void {
    const el = document.documentElement;
    el.setAttribute('lang', this._lang());
    el.setAttribute('dir', this.direction());
  }
}
