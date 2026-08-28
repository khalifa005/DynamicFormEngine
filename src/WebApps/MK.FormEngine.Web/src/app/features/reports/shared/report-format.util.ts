/** Picks the name matching the active language — same fallback rule `dashboard` uses. */
export function localizedName(nameEn: string | null | undefined, nameAr: string | null | undefined, lang: string): string {
  const preferred = lang === 'ar' ? nameAr : nameEn;
  return preferred?.trim() || nameEn?.trim() || nameAr?.trim() || '';
}
