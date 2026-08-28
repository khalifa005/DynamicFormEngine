# FSMS Web (NWC.Web)

Angular 20 front-end for the **Field Survey Management System (FSMS)**.

## Stack

- Angular 20 (standalone, signals)
- PrimeNG 20 (Aura) + custom water-domain preset
- Tailwind CSS 3 (`tailwindcss-primeui`)
- Transloco (Arabic / English, RTL + LTR)
- JWT auth against the NWC.API backend (`/api/v1/auth/login`)

## Prerequisites

- Node.js 20+ and npm
- Running NWC.API backend (default dev URL `http://localhost:5157`)

## Getting started

```bash
npm install
npm start
```

The app runs at `http://localhost:4200`.

## Runtime configuration

Backend URL and app settings are read at runtime from
[`public/config/app-config.json`](public/config/app-config.json) so they can be
changed per environment without rebuilding:

```json
{
  "apiBaseUrl": "http://localhost:5157/api/v1",
  "defaultLanguage": "ar",
  "availableLanguages": ["ar", "en"],
  "tokenRefreshEnabled": true,
  "appName": "FSMS"
}
```

## Project structure

```
src/app/
  core/        config, auth (service + signal store), interceptors, guards, i18n, theme
  layout/      dashboard shell (sidebar + topbar)
  features/    auth/login, dashboard
```

## Internationalization / RTL

Language and text direction are owned by `core/i18n/language.service.ts`, which
sets `document.dir` (`rtl` for Arabic, `ltr` for English). Translation files live
in `public/i18n/{ar,en}.json`. Default language is Arabic (RTL).

## Auth notes

Tokens are returned in the response body. The short-lived access token is kept in
memory (`AuthStore`); the refresh token + profile are persisted to `localStorage`
only when "Remember me" is checked, so sessions survive reloads.

## Build

```bash
npm run build
```
