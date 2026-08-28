import {
  DATE_RULES,
  DEFAULT_ALLOWED_EXTENSIONS,
  DEFAULT_BARCODE_FORMATS,
  DEFAULT_VALUE_MODES,
  ELEMENT_TYPES,
  NUMERIC_FORMATS,
  PALETTE_GROUPS,
  RULE_MATCH,
  SECTION_DISPLAYS,
  type ElementType,
  type FormElement,
  type PaletteGroup,
  type RuleGroup,
  isAttachmentType,
  isChoiceType,
  isDateRuleType,
} from '../models/form-builder.types';

export interface PaletteItem {
  readonly type: ElementType;
  readonly icon: string;
  readonly group: PaletteGroup;
  /** Transloco key for the display label. */
  readonly labelKey: string;
}

export const PALETTE_ITEMS: readonly PaletteItem[] = [
  { type: ELEMENT_TYPES.Text, icon: 'pi pi-pencil', group: PALETTE_GROUPS.Basic, labelKey: 'formBuilder.types.text' },
  { type: ELEMENT_TYPES.Memo, icon: 'pi pi-align-left', group: PALETTE_GROUPS.Basic, labelKey: 'formBuilder.types.memo' },
  { type: ELEMENT_TYPES.Numeric, icon: 'pi pi-hashtag', group: PALETTE_GROUPS.Basic, labelKey: 'formBuilder.types.numeric' },
  { type: ELEMENT_TYPES.YesNo, icon: 'pi pi-check-square', group: PALETTE_GROUPS.Basic, labelKey: 'formBuilder.types.yes_no' },
  { type: ELEMENT_TYPES.Date, icon: 'pi pi-calendar', group: PALETTE_GROUPS.Basic, labelKey: 'formBuilder.types.date' },
  { type: ELEMENT_TYPES.Time, icon: 'pi pi-clock', group: PALETTE_GROUPS.Basic, labelKey: 'formBuilder.types.time' },
  { type: ELEMENT_TYPES.CalendarWithHours, icon: 'pi pi-calendar-clock', group: PALETTE_GROUPS.Basic, labelKey: 'formBuilder.types.calendar_with_hours' },
  { type: ELEMENT_TYPES.DateTime, icon: 'pi pi-calendar-plus', group: PALETTE_GROUPS.Basic, labelKey: 'formBuilder.types.date_time' },
  { type: ELEMENT_TYPES.Barcode, icon: 'pi pi-qrcode', group: PALETTE_GROUPS.Basic, labelKey: 'formBuilder.types.barcode' },
  { type: ELEMENT_TYPES.SingleChoice, icon: 'pi pi-list', group: PALETTE_GROUPS.Choice, labelKey: 'formBuilder.types.single_choice' },
  { type: ELEMENT_TYPES.MultipleChoice, icon: 'pi pi-check-circle', group: PALETTE_GROUPS.Choice, labelKey: 'formBuilder.types.multiple_choice' },
  { type: ELEMENT_TYPES.Signature, icon: 'pi pi-pencil', group: PALETTE_GROUPS.Media, labelKey: 'formBuilder.types.signature' },
  { type: ELEMENT_TYPES.Photo, icon: 'pi pi-camera', group: PALETTE_GROUPS.Media, labelKey: 'formBuilder.types.photo' },
  { type: ELEMENT_TYPES.Video, icon: 'pi pi-video', group: PALETTE_GROUPS.Media, labelKey: 'formBuilder.types.video' },
  { type: ELEMENT_TYPES.Audio, icon: 'pi pi-volume-up', group: PALETTE_GROUPS.Media, labelKey: 'formBuilder.types.audio' },
  { type: ELEMENT_TYPES.File, icon: 'pi pi-file', group: PALETTE_GROUPS.Media, labelKey: 'formBuilder.types.file' },
  { type: ELEMENT_TYPES.Geolocation, icon: 'pi pi-map-marker', group: PALETTE_GROUPS.Location, labelKey: 'formBuilder.types.geolocation' },
  { type: ELEMENT_TYPES.Section, icon: 'pi pi-folder', group: PALETTE_GROUPS.Design, labelKey: 'formBuilder.types.section' },
];

export const PALETTE_GROUP_ORDER: readonly PaletteGroup[] = [
  PALETTE_GROUPS.Basic,
  PALETTE_GROUPS.Choice,
  PALETTE_GROUPS.Media,
  PALETTE_GROUPS.Location,
  PALETTE_GROUPS.Design,
];

/** Defaults for a new file-backed media element. */
const DEFAULT_MAX_FILES = 3;
const DEFAULT_MAX_FILE_SIZE_MB = 10;

/** Country-level view of Saudi Arabia. */
const DEFAULT_MAP_ZOOM = 5;

function emptyRuleGroup(): RuleGroup {
  return { match: RULE_MATCH.All, conditions: [], preserve_data: false };
}

function generateKey(): string {
  return `el_${Math.random().toString(36).slice(2, 8)}`;
}

/**
 * Build a fresh element for the given palette type with sensible defaults.
 * `data_name` is assigned by the store so it can guarantee uniqueness.
 */
export function createElement(type: ElementType): FormElement {
  const base: FormElement = {
    key: generateKey(),
    type,
    label_en: '',
    label_ar: '',
    data_name: '',
    description_en: '',
    description_ar: '',
    default_value: null,
    default_value_mode: DEFAULT_VALUE_MODES.Fixed,
    required: false,
    hidden: false,
    disabled: false,
    min_length: null,
    max_length: null,
    pattern: null,
    format: type === ELEMENT_TYPES.Numeric ? NUMERIC_FORMATS.Decimal : null,
    min: null,
    max: null,
    date_rule: isDateRuleType(type) ? DATE_RULES.None : null,
    min_date: null,
    max_date: null,
    allow_other: false,
    multiple: type === ELEMENT_TYPES.MultipleChoice,
    parent_field: null,
    choices: [],
    max_files: isAttachmentType(type) ? DEFAULT_MAX_FILES : null,
    max_file_size_mb: isAttachmentType(type) ? DEFAULT_MAX_FILE_SIZE_MB : null,
    allowed_extensions: [...(DEFAULT_ALLOWED_EXTENSIONS[type] ?? [])],
    barcode_formats: type === ELEMENT_TYPES.Barcode ? [...DEFAULT_BARCODE_FORMATS] : [],
    map_zoom: type === ELEMENT_TYPES.Geolocation ? DEFAULT_MAP_ZOOM : null,
    display: type === ELEMENT_TYPES.Section ? SECTION_DISPLAYS.Inline : null,
    elements: [],
    visible_conditions: emptyRuleGroup(),
    required_conditions: emptyRuleGroup(),
  };

  if (isChoiceType(type)) {
    base.choices = [
      { value: 'option_1', label_en: 'Option 1', label_ar: 'الخيار 1', dependency_value: null },
      { value: 'option_2', label_en: 'Option 2', label_ar: 'الخيار 2', dependency_value: null },
    ];
  }

  return base;
}
