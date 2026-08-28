import { Component, computed, effect, inject, input, model, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { TranslocoDirective } from '@jsverse/transloco';
import { finalize } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { TabsModule } from 'primeng/tabs';
import { TagModule } from 'primeng/tag';
import { TimelineModule } from 'primeng/timeline';
import { TooltipModule } from 'primeng/tooltip';

import {
  FsmsSurveysClient,
  SurveyDetailDto,
  SurveyFileDto,
  SurveyTimelineEntryDto,
  API_BASE_URL
} from '../../../../core/api/api-client.generated';
import { MediaObjectUrlService } from '../../../../core/api/media-object-url.service';
import { LanguageService } from '../../../../core/i18n/language.service';
import { MediaThumbnailComponent } from '../../../../shared/components/media-viewer/media-thumbnail.component';
import { MediaViewerDialogComponent } from '../../../../shared/components/media-viewer/media-viewer-dialog.component';
import { MediaItem } from '../../../../shared/components/media-viewer/media-kind';
import { GeoMapComponent } from '../../../../shared/components/geo-map/geo-map.component';
import type { GeoPoint } from '../../../../shared/components/geo-map/google-maps.types';
import {
  surveyReturnReasonSeverity,
  surveyStatusSeverity,
  wasSubmittedAgain,
} from '../../survey-status';
import { AnswerMapDialogComponent } from './answer-map-dialog.component';
import { SurveyPreviewDialogComponent } from './survey-preview-dialog.component';

/**
 * One answer in the digest. The API snapshots the question's labels and, for a choice field, the
 * selected option's labels at fill time. Fills recorded before that change stored the bare value
 * instead, so an entry may still be a primitive — see `answerEntries`.
 */
interface SummaryAnswer {
  readonly type?: string;
  readonly labelEn?: string;
  readonly labelAr?: string;
  readonly value?: unknown;
  readonly displayEn?: string;
  readonly displayAr?: string;
}

/** The form-builder type whose answer is a map point. Mirrors `BuilderElementTypes.Geolocation`. */
const GEOLOCATION_TYPE = 'geolocation';

/** One rendered answer row: what it reads as, and — for a geolocation answer — where it points. */
interface AnswerEntry {
  readonly label: string;
  readonly value: string;
  readonly point: GeoPoint | null;
}

/** One fill, as stored in the survey's `resultSummaryJson` digest. */
interface FillSummary {
  readonly submissionId?: number;
  readonly filledBy?: string;
  readonly filledAt?: string;
  readonly answerCount?: number;
  readonly answers?: Record<string, unknown>;
}

/** One tile in the Files gallery: what the viewer needs, plus the columns the old table showed. */
interface FileCard {
  readonly item: MediaItem;
  readonly created?: Date;
  readonly dataName?: string;
}

/** The digest grouped for display: one block per fill role, newest fill last. */
interface RoleSummary {
  readonly role: string;
  readonly fills: readonly FillSummary[];
}

/** The newest fill across every role — shown on the detail body, not only in Records. */
interface LatestFillView {
  readonly role: string;
  readonly fill: FillSummary;
}

/**
 * Read-only survey inspector: the header facts, the latest fill, who it is allocated to,
 * earlier fills on the Records tab, and the append-only status trail. Detail and timeline
 * are separate endpoints, so both are fetched when the dialog opens.
 */
@Component({
  selector: 'app-survey-detail-dialog',
  standalone: true,
  imports: [
    CommonModule,
    TranslocoDirective,
    DialogModule,
    ProgressSpinnerModule,
    TabsModule,
    TagModule,
    TimelineModule,
    ButtonModule,
    TooltipModule,
    MediaThumbnailComponent,
    MediaViewerDialogComponent,
    GeoMapComponent,
    AnswerMapDialogComponent,
    SurveyPreviewDialogComponent,
  ],
  templateUrl: './survey-detail-dialog.component.html',
})
export class SurveyDetailDialogComponent {
  readonly visible = model.required<boolean>();
  readonly surveyId = input<number | null>(null);

  private readonly surveysClient = inject(FsmsSurveysClient);
  private readonly media = inject(MediaObjectUrlService);
  private readonly language = inject(LanguageService);
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /** Drives the direction-sensitive bits the CSS cannot reach, such as the status-trail arrow. */
  protected readonly isRtl = this.language.isRtl;

  protected readonly exporting = signal(false);

  protected readonly loading = signal(false);
  protected readonly timelineLoading = signal(false);
  protected readonly survey = signal<SurveyDetailDto | null>(null);
  protected readonly timeline = signal<SurveyTimelineEntryDto[]>([]);
  protected readonly files = signal<SurveyFileDto[]>([]);
  protected readonly filesLoading = signal(false);
  protected readonly summaries = signal<RoleSummary[]>([]);
  protected readonly loadFailed = signal(false);

  protected readonly statusSeverity = surveyStatusSeverity;
  protected readonly returnReasonSeverity = surveyReturnReasonSeverity;
  protected readonly wasSubmittedAgain = wasSubmittedAgain;

  protected readonly viewerVisible = signal(false);
  protected readonly viewerIndex = signal(0);

  /** The read-only render of the filled form; see {@link SurveyPreviewDialogComponent}. */
  protected readonly previewVisible = signal(false);

  /** The geolocation answer currently shown on the map dialog. */
  protected readonly answerMapVisible = signal(false);
  protected readonly answerMapPoint = signal<GeoPoint | null>(null);
  protected readonly answerMapLabel = signal('');

  /** File ids whose download is in flight, so only that tile's button spins. */
  private readonly downloading = signal<ReadonlySet<string>>(new Set());

  protected readonly fileCards = computed<FileCard[]>(() =>
    this.files().map((file) => ({
      item: {
        fileId: file.fileId,
        name: file.fileName ?? '',
        contentType: file.contentType,
        sizeBytes: file.sizeBytes,
      },
      created: file.created,
      dataName: file.dataName,
    })),
  );

  /** Newest fill by `filledAt`, used on the detail body so the user does not open Records first. */
  protected readonly latestFill = computed<LatestFillView | null>(() => {
    let latest: LatestFillView | null = null;
    let latestTime = Number.NEGATIVE_INFINITY;

    for (const group of this.summaries()) {
      for (const fill of group.fills) {
        const time = fill.filledAt ? Date.parse(fill.filledAt) : Number.NEGATIVE_INFINITY;
        if (!latest || time >= latestTime) {
          latest = { role: group.role, fill };
          latestTime = time;
        }
      }
    }

    return latest;
  });

  /** Older fills kept on the Records tab as fill history. */
  protected readonly historicalSummaries = computed<RoleSummary[]>(() => {
    const latest = this.latestFill();
    if (!latest) {
      return [];
    }

    return this.summaries()
      .map((group) => ({
        role: group.role,
        fills: group.fills.filter((fill) => !isSameFill(fill, latest.fill)),
      }))
      .filter((group) => group.fills.length > 0);
  });

  /** The survey's pin, or null when the record carries no coordinates. */
  protected readonly locationPoint = computed<GeoPoint | null>(() => {
    const detail = this.survey();
    return detail?.latitude != null && detail?.longitude != null
      ? { lat: detail.latitude, lng: detail.longitude }
      : null;
  });

  /** The gallery order the viewer steps through — the same list the tiles render. */
  protected readonly viewerItems = computed<MediaItem[]>(() =>
    this.fileCards().map((card) => card.item),
  );

  constructor() {
    effect(() => {
      const id = this.surveyId();
      if (this.visible() && id) {
        this.load(id);
      }
    });
  }



  protected openPreview(): void {
    this.previewVisible.set(true);
  }

  protected exportToPdf() {
    const id = this.surveyId();
    if (!id) return;
    
    this.exporting.set(true);
    const lang = this.language.current();
    
    this.http.get(`${this.baseUrl}/api/v1/fsms/surveys/${id}/export-pdf?language=${lang}`, {
      responseType: 'blob',
      observe: 'response'
    }).pipe(
      finalize(() => this.exporting.set(false))
    ).subscribe({
      next: (response) => {
        const contentDisposition = response.headers.get('content-disposition') || '';
        // Stop at the `;` — the header also carries a `filename*=UTF-8''…` copy, and swallowing it
        // makes the saved file name (and any later re-upload) carry the whole header fragment.
        const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(contentDisposition);
        const fileName = match ? decodeURIComponent(match[1].trim()) : `Survey_${id}_${new Date().toISOString().split('T')[0]}.pdf`;
        
        const blob = new Blob([response.body as Blob], { type: 'application/pdf' });
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        window.URL.revokeObjectURL(url);
      },
      error: () => {
        // We could show a toast here if we injected MessageService, but keeping it simple for now.
        console.error('Failed to export PDF');
      }
    });
  }

  private load(id: number): void {
    this.survey.set(null);
    this.timeline.set([]);
    this.files.set([]);
    this.summaries.set([]);
    this.loadFailed.set(false);
    this.loading.set(true);
    this.timelineLoading.set(true);
    this.filesLoading.set(true);

    this.surveysClient.fsmsSurveys_GetSurveyById(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          const detail = res.data ?? null;
          this.survey.set(detail);
          this.summaries.set(parseResultSummary(detail?.resultSummaryJson));
        },
        error: () => this.loadFailed.set(true),
      });

    this.surveysClient.fsmsSurveys_GetSurveyTimeline(id)
      .pipe(finalize(() => this.timelineLoading.set(false)))
      .subscribe({
        next: (res) => this.timeline.set([...(res.data ?? [])]),
        error: () => this.timeline.set([]),
      });

    this.surveysClient.fsmsSurveys_GetSurveyFiles(id)
      .pipe(finalize(() => this.filesLoading.set(false)))
      .subscribe({
        next: (res) => this.files.set([...(res.data ?? [])]),
        error: () => this.files.set([]),
      });
  }

  /**
   * Renders a digest answer map as `label: value` lines in the active language. Choice answers carry
   * the option's own labels, so the user reads "Football" rather than the stored `option_1`. Older
   * fills stored the bare value with no labels at all; those keep falling back to `data_name: value`.
   * A geolocation answer also carries its point, which is what puts a "view on map" button on the row.
   */
  protected answerEntries(answers?: Record<string, unknown>): AnswerEntry[] {
    if (!answers) {
      return [];
    }

    return Object.entries(answers).map(([key, raw]) => {
      const bare = asGeoPoint(raw);
      if (bare || !isSummaryAnswer(raw)) {
        return { label: key, value: this.formatValue(raw), point: bare };
      }

      return {
        label: this.localizedName(raw.labelEn, raw.labelAr) || key,
        value: this.localizedName(raw.displayEn, raw.displayAr) || this.formatValue(raw.value),
        // Only a declared geolocation field may be read out of text — a "1,2" answer in a text
        // field is not a coordinate.
        point: raw.type === GEOLOCATION_TYPE ? geoPointOf(raw.value) : null,
      };
    });
  }

  /**
   * The display name behind an account id. The digest stamps each fill with the id alone, so the
   * detail document carries the names beside it; an id the API could not resolve — a deleted
   * account — still reads as the id rather than as a blank.
   */
  protected userName(userId?: string | null): string {
    if (!userId) {
      return '';
    }

    return this.survey()?.userNames?.[userId] ?? userId;
  }

  protected openAnswerMap(entry: AnswerEntry): void {
    if (!entry.point) {
      return;
    }

    this.answerMapPoint.set(entry.point);
    this.answerMapLabel.set(entry.label);
    this.answerMapVisible.set(true);
  }

  /** Stored values shown as-is; a multi-select array reads as a comma-joined list. */
  private formatValue(value: unknown): string {
    if (value === null || value === undefined || value === '') {
      return '-';
    }

    if (Array.isArray(value)) {
      return value.length > 0 ? value.map((item) => String(item)).join(', ') : '-';
    }

    // A geolocation answer is a `{ lat, lng, address? }` object. The API resolves it to a display
    // line, but a digest written before that did not, and `String(point)` would read
    // "[object Object]".
    const point = asGeoPoint(value);
    if (point) {
      const coordinates = `${point.lat}, ${point.lng}`;
      return point.address ? `${coordinates} — ${point.address}` : coordinates;
    }

    return String(value);
  }

  /** Branch as `code — name`, falling back to whichever side is available. */
  protected branchLabel(survey: SurveyDetailDto): string {
    const name = this.localizedName(survey.branchNameEn, survey.branchNameAr);
    if (survey.branchCode && name) {
      return `${survey.branchCode} — ${name}`;
    }

    return survey.branchCode || name || '-';
  }

  protected departmentLabel(survey: SurveyDetailDto): string {
    return this.localizedName(survey.departmentNameEn, survey.departmentNameAr) || '-';
  }

  protected taskTypeLabel(survey: SurveyDetailDto): string {
    return this.localizedName(survey.taskTypeNameEn, survey.taskTypeNameAr) || '-';
  }

  protected customerTypeLabel(survey: SurveyDetailDto): string {
    return this.localizedName(survey.customerTypeNameEn, survey.customerTypeNameAr) || '-';
  }

  private localizedName(nameEn?: string, nameAr?: string): string {
    const preferred = this.language.current() === 'ar' ? nameAr : nameEn;
    return preferred?.trim() || nameEn?.trim() || nameAr?.trim() || '';
  }

  protected formatBytes(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + ' ' + sizes[i];
  }

  protected openViewer(index: number): void {
    this.viewerIndex.set(index);
    this.viewerVisible.set(true);
  }

  protected isDownloading(item: MediaItem): boolean {
    return !!item.fileId && this.downloading().has(item.fileId);
  }

  protected downloadFile(item: MediaItem): void {
    const fileId = item.fileId;
    if (!fileId || this.isDownloading(item)) {
      return;
    }

    this.setDownloading(fileId, true);
    this.media
      .download(fileId, item.name)
      .pipe(finalize(() => this.setDownloading(fileId, false)))
      .subscribe({
        error: () => {
          // The tile stays put; the user can retry from the same button.
        },
      });
  }

  private setDownloading(fileId: string, busy: boolean): void {
    const next = new Set(this.downloading());
    if (busy) {
      next.add(fileId);
    } else {
      next.delete(fileId);
    }
    this.downloading.set(next);
  }
}

/**
 * Tells the labelled answer shape from the bare value stored by pre-existing fills. An array is a
 * multi-select answer from the old shape, not a labelled entry, so it is deliberately excluded.
 */
function isSummaryAnswer(value: unknown): value is SummaryAnswer {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/**
 * A bare `{ lat, lng, address? }` answer — the shape a geolocation field stores — or `null`. The
 * address is carried through so the map dialog can name the pin; a fill from before addresses
 * existed simply has none.
 */
function asGeoPoint(value: unknown): GeoPoint | null {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    return null;
  }

  const point = value as Partial<GeoPoint>;
  if (typeof point.lat !== 'number' || typeof point.lng !== 'number') {
    return null;
  }

  return {
    lat: point.lat,
    lng: point.lng,
    address: typeof point.address === 'string' ? point.address : null,
  };
}

/**
 * A geolocation answer in either shape it can reach the client: the `{ lat, lng }` object, or the
 * `"lat, lng"` text a template default (and a value read back out of its column) uses.
 * Call only for a field already known to be a geolocation.
 */
function geoPointOf(value: unknown): GeoPoint | null {
  const point = asGeoPoint(value);
  if (point || typeof value !== 'string') {
    return point;
  }

  const parts = value.split(',');
  if (parts.length !== 2) {
    return null;
  }

  const lat = Number(parts[0].trim());
  const lng = Number(parts[1].trim());
  return Number.isFinite(lat) && Number.isFinite(lng) && Math.abs(lat) <= 90 && Math.abs(lng) <= 180
    ? { lat, lng }
    : null;
}

function isSameFill(left: FillSummary, right: FillSummary): boolean {
  if (left.submissionId != null && right.submissionId != null) {
    return left.submissionId === right.submissionId;
  }

  return left === right;
}

/**
 * The API stores the roll-up as `{ "FIELD_TEAM": [fill, …], "BACK_OFFICE": [fill, …] }`. Anything
 * unparseable is treated as "no summary yet" rather than breaking the dialog.
 */
function parseResultSummary(json?: string): RoleSummary[] {
  if (!json) {
    return [];
  }

  try {
    const parsed = JSON.parse(json) as Record<string, FillSummary[]>;
    return Object.entries(parsed)
      .filter(([, fills]) => Array.isArray(fills) && fills.length > 0)
      .map(([role, fills]) => ({ role, fills }));
  } catch {
    return [];
  }
}
