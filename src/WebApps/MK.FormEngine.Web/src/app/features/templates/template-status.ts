/**
 * Template lifecycle vocabulary, mirrored from the API's `TemplateStatuses` constants. The API
 * stores and returns these exact uppercase strings; the UI labels them through
 * `templates.statusLabel.*` translation keys (`templates.status` is the column header).
 *
 * These live in one place because the row menu used to compare against PascalCase literals
 * (`'Draft'`, `'Published'`, `'Deprecated'`) that the API never sends, which left Publish, Deprecate
 * and Archive permanently disabled and every status tag the same default colour. The helpers below
 * compare case-insensitively so neither side can quietly break the other again.
 */
export const TemplateStatus = {
  Draft: 'DRAFT',
  Published: 'PUBLISHED',
  Deprecated: 'DEPRECATED',
  Archived: 'ARCHIVED',
} as const;

export type TemplateStatusValue = (typeof TemplateStatus)[keyof typeof TemplateStatus];

export const TEMPLATE_STATUSES: readonly TemplateStatusValue[] = [
  TemplateStatus.Draft,
  TemplateStatus.Published,
  TemplateStatus.Deprecated,
  TemplateStatus.Archived,
];

export type TemplateTagSeverity = 'success' | 'secondary' | 'info' | 'warn' | 'danger' | 'contrast';

/** Normalizes whatever the row carries so casing can never decide a guard. */
function normalize(status?: string): string {
  return (status ?? '').trim().toUpperCase();
}

/** Colour of the status tag: live is green, in-design blue, retiring amber, retired grey. */
export function templateStatusSeverity(status?: string): TemplateTagSeverity {
  switch (normalize(status)) {
    case TemplateStatus.Published:
      return 'success';
    case TemplateStatus.Draft:
      return 'info';
    case TemplateStatus.Deprecated:
      return 'warn';
    case TemplateStatus.Archived:
      return 'secondary';
    default:
      return 'secondary';
  }
}

/** Translation key for the status label, so the tag is not raw uppercase in either language. */
export function templateStatusLabelKey(status?: string): string {
  const value = normalize(status);
  return TEMPLATE_STATUSES.includes(value as TemplateStatusValue)
    ? `templates.statusLabel.${value.toLowerCase()}`
    : 'templates.statusLabel.unknown';
}

/**
 * Only a draft can be published. `SurveyTemplate.Publish` refuses a deprecated or archived
 * template, and a published one has nothing new to freeze until it is edited back into a draft.
 */
export function canPublish(status?: string): boolean {
  return normalize(status) === TemplateStatus.Draft;
}

/** `SurveyTemplate.Deprecate` accepts a published template and nothing else. */
export function canDeprecate(status?: string): boolean {
  return normalize(status) === TemplateStatus.Published;
}

/**
 * Archiving is offered only after deprecation, keeping the lifecycle to a single path
 * (Published → Deprecated → Archived). The domain would allow archiving a draft outright; the UI
 * deliberately does not, so a template is never retired without first being taken out of service.
 */
export function canArchive(status?: string): boolean {
  return normalize(status) === TemplateStatus.Deprecated;
}
