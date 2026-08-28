import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
  importProvidersFrom,
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { providePrimeNG } from 'primeng/config';
import { MessageService } from 'primeng/api';
import { provideTransloco, TranslocoService } from '@jsverse/transloco';
import { NgxPermissionsModule } from 'ngx-permissions';
import { FORMLY_CONFIG, FormlyModule } from '@ngx-formly/core';
import { FormlyPrimeNGModule } from '@ngx-formly/primeng';

import { FormlyFieldAutocompleteComponent } from './shared/components/dynamic-form/formly-autocomplete.component';
import { FormlyDateComponent } from './shared/components/dynamic-form/formly-date.component';
import { FormlyTimeComponent } from './shared/components/dynamic-form/formly-time.component';
import { FormlyChoiceComponent } from './shared/components/dynamic-form/formly-choice.component';
import { FormlySignatureComponent } from './shared/components/dynamic-form/formly-signature.component';
import { FormlyAttachmentComponent } from './shared/components/dynamic-form/formly-attachment.component';
import { FormlyGeolocationComponent } from './shared/components/dynamic-form/formly-geolocation.component';
import { FormlyBarcodeComponent } from './shared/components/dynamic-form/formly-barcode.component';
import { FormlyCalendarWithHoursComponent } from './shared/components/dynamic-form/formly-calendar-with-hours.component';
import { FormlyDateTimeComponent } from './shared/components/dynamic-form/formly-date-time.component';
import { FormlyFieldHelpWrapperComponent } from './shared/components/dynamic-form/formly-field-help.wrapper';
import { FormlySectionWrapperComponent } from './shared/components/dynamic-form/formly-section.wrapper';
import { formlyValidationConfig } from './shared/components/dynamic-form/formly-validators';
import {
  FORMLY_TYPES,
  FORMLY_WRAPPERS,
} from './shared/components/dynamic-form/formly-preview.types';

import { routes } from './app.routes';
import { WaterPreset } from './core/theme/water-preset';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { AppConfigService } from './core/config/app-config.service';
import { SsoService } from './core/auth/sso.service';
import { LanguageService } from './core/i18n/language.service';
import { TranslocoHttpLoader } from './core/i18n/transloco-loader';
import { API_BASE_URL } from './core/api';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
    provideAnimationsAsync(),
    MessageService,
    {
      provide: API_BASE_URL,
      useFactory: (configService: AppConfigService) => configService.snapshot.apiBaseUrl,
      deps: [AppConfigService],
    },
    providePrimeNG({
      theme: {
        preset: WaterPreset,
        options: {
          darkModeSelector: '[data-theme="dark"]',
          cssLayer: {
            name: 'primeng',
            order: 'tailwind-base, primeng, tailwind-utilities',
          },
        },
      },
    }),
    provideTransloco({
      config: {
        availableLangs: ['ar', 'en'],
        defaultLang: 'ar',
        fallbackLang: 'en',
        reRenderOnLangChange: true,
        prodMode: false,
      },
      loader: TranslocoHttpLoader,
    }),
    provideAppInitializer(async () => {
      const configService = inject(AppConfigService);
      const languageService = inject(LanguageService);
      const ssoService = inject(SsoService);
      await configService.load();
      languageService.init(configService.snapshot.defaultLanguage);
      // Needs the API base URL, so it can only run once config has landed. The sign-in page reads
      // the answer to decide whether to offer SSO or the credentials form.
      await ssoService.loadStatus();
    }),
    importProvidersFrom(NgxPermissionsModule.forRoot()),
    importProvidersFrom(
      FormlyModule.forRoot({
        types: [
          { name: 'autocomplete', component: FormlyFieldAutocompleteComponent },
          { name: 'autocompleteem', component: FormlyFieldAutocompleteComponent },
          { name: 'autocompleteMultiSelect', component: FormlyFieldAutocompleteComponent },
          { name: FORMLY_TYPES.Date, component: FormlyDateComponent },
          { name: FORMLY_TYPES.Time, component: FormlyTimeComponent },
          {
            name: FORMLY_TYPES.Choice,
            component: FormlyChoiceComponent,
            defaultOptions: { wrappers: ['form-field'] },
          },
          {
            name: FORMLY_TYPES.MultiChoice,
            extends: FORMLY_TYPES.Choice,
            defaultOptions: { props: { multiple: true }, wrappers: ['form-field'] },
          },
          { name: FORMLY_TYPES.Signature, component: FormlySignatureComponent },
          { name: FORMLY_TYPES.Attachment, component: FormlyAttachmentComponent },
          { name: FORMLY_TYPES.Geolocation, component: FormlyGeolocationComponent },
          { name: FORMLY_TYPES.Barcode, component: FormlyBarcodeComponent },
          { name: FORMLY_TYPES.CalendarWithHours, component: FormlyCalendarWithHoursComponent },
          { name: FORMLY_TYPES.DateTime, component: FormlyDateTimeComponent },
        ],
        wrappers: [
          { name: FORMLY_WRAPPERS.FieldHelp, component: FormlyFieldHelpWrapperComponent },
          { name: FORMLY_WRAPPERS.SectionPanel, component: FormlySectionWrapperComponent },
        ],
      }),
      FormlyPrimeNGModule
    ),
    // Registered separately so validation messages resolve through Transloco.
    {
      provide: FORMLY_CONFIG,
      multi: true,
      useFactory: (transloco: TranslocoService) => formlyValidationConfig(transloco),
      deps: [TranslocoService],
    },
  ],
};
