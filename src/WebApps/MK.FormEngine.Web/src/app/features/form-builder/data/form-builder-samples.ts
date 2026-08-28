/** Static sample JSON served from `public/form-builder/`. */
export const FORM_BUILDER_SAMPLE_FILES = {
  AllFieldTypes: 'sample-all-fields',
  WaterLeakageQuality: 'sample-water-leakage',
} as const;

export type FormBuilderSampleFile =
  (typeof FORM_BUILDER_SAMPLE_FILES)[keyof typeof FORM_BUILDER_SAMPLE_FILES];

export function formBuilderSampleUrl(fileName: FormBuilderSampleFile): string {
  return `/form-builder/${fileName}.json`;
}
