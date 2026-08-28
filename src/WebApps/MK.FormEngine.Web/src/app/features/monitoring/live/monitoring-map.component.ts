import {
  AfterViewInit,
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  OnDestroy,
  effect,
  inject,
  input,
  model,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { TooltipModule } from 'primeng/tooltip';
import { TranslocoDirective } from '@jsverse/transloco';
import { GoogleMapsLoaderService } from '../../../shared/components/geo-map/google-maps-loader.service';
import {
  SAUDI_ARABIA_CENTER,
  SAUDI_ARABIA_ZOOM,
} from '../../../shared/components/geo-map/geo-map.component';
import {
  MAP_TYPES,
  type GeoPoint,
  type GoogleInfoWindowInstance,
  type GoogleMapInstance,
  type GoogleMapsApi,
  type GoogleMarkerInstance,
  type MapType,
} from '../../../shared/components/geo-map/google-maps.types';
import { SurveyStatus } from '../../surveys/survey-status';
import type { MonitoringSurveyMarker, MonitoringTeamMarker } from './monitoring-map.types';

const SINGLE_STOP_ZOOM = 15;
const FOCUSED_ZOOM = 15;
const BOUNDS_PADDING = 48;
const PIN_SIZE = 36;
const TEAM_PIN_SIZE = 40;

const STATUS_PIN_COLORS: Record<string, string> = {
  [SurveyStatus.Assigned]: '#0284c7',
  [SurveyStatus.InProgress]: '#7c3aed',
  [SurveyStatus.Returned]: '#dc2626',
};

const DIMMED_OPACITY = 0.35;
const TEAM_PIN_COLOR = '#0f766e';
const TEAM_PIN_EMPHASIZED = '#ea580c';

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

/** Circular survey pin; optional team badge when the survey is allocated. */
function surveyPinIcon(fill: string, allocated: boolean, dimmed: boolean): string {
  const opacity = dimmed ? DIMMED_OPACITY : 1;
  const centre = PIN_SIZE / 2;
  const badge =
    allocated
      ? `<circle cx="${PIN_SIZE - 8}" cy="8" r="7" fill="#0f766e" stroke="#ffffff" stroke-width="2"/>` +
        `<path d="M${PIN_SIZE - 11} 6.5 h6 M${PIN_SIZE - 8} 4.5 v5" stroke="#ffffff" stroke-width="1.4" stroke-linecap="round"/>` +
        `<circle cx="${PIN_SIZE - 8}" cy="9.5" r="1.6" fill="#ffffff"/>`
      : '';
  const svg =
    `<svg xmlns="http://www.w3.org/2000/svg" width="${PIN_SIZE}" height="${PIN_SIZE}" viewBox="0 0 ${PIN_SIZE} ${PIN_SIZE}" opacity="${opacity}">` +
    `<circle cx="${centre}" cy="${centre}" r="13" fill="${fill}" stroke="#ffffff" stroke-width="3"/>` +
    badge +
    `</svg>`;
  return `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`;
}

/** Distinct vehicle-style pin for mock / live team GPS. */
function teamPinIcon(fill: string, emphasized: boolean): string {
  const size = TEAM_PIN_SIZE;
  const ring = emphasized ? 3.5 : 2.5;
  const svg =
    `<svg xmlns="http://www.w3.org/2000/svg" width="${size}" height="${size}" viewBox="0 0 ${size} ${size}">` +
    `<circle cx="${size / 2}" cy="${size / 2}" r="15" fill="${fill}" stroke="#ffffff" stroke-width="${ring}"/>` +
    `<path d="M12 22 L12 18 L14 14 L26 14 L28 18 L28 22 Z" fill="#ffffff" opacity="0.95"/>` +
    `<circle cx="16" cy="22" r="2.2" fill="${fill}"/>` +
    `<circle cx="24" cy="22" r="2.2" fill="${fill}"/>` +
    `</svg>`;
  return `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`;
}

@Component({
  selector: 'app-monitoring-map',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonModule, MessageModule, TooltipModule, TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
      <div class="monitoring-map-shell">
        @if (error()) {
          <p-message severity="warn" [text]="t(error()!)" styleClass="w-full rounded-none border-0" />
        }

        <div class="relative flex-1 min-h-0">
          <div #canvas class="absolute inset-0 bg-[var(--app-surface-alt)]"></div>

          <div
            class="absolute top-2 start-2 flex rounded-lg overflow-hidden shadow-md bg-white/95 backdrop-blur-sm z-10"
          >
            <button
              type="button"
              class="px-2.5 py-1.5 text-xs font-semibold transition"
              [class]="mapType() === roadmap ? activeViewClass : idleViewClass"
              (click)="setMapType(roadmap)"
            >
              <i class="pi pi-map text-[11px] me-1"></i>{{ t('geoMap.viewMap') }}
            </button>
            <button
              type="button"
              class="px-2.5 py-1.5 text-xs font-semibold transition"
              [class]="mapType() === satellite ? activeViewClass : idleViewClass"
              (click)="setMapType(satellite)"
            >
              <i class="pi pi-globe text-[11px] me-1"></i>{{ t('geoMap.viewSatellite') }}
            </button>
          </div>

          <div class="absolute bottom-3 end-3 z-10">
            <p-button
              icon="pi pi-arrows-alt"
              size="small"
              [rounded]="true"
              severity="secondary"
              [pTooltip]="t('monitoring.fitMap')"
              [ariaLabel]="t('monitoring.fitMap')"
              (onClick)="fitToMarkers()"
            />
          </div>
        </div>
      </div>
    </ng-container>
  `,
  styles: [
    `
      :host {
        display: block;
        height: 100%;
        min-height: 0;
      }
      .monitoring-map-shell {
        display: flex;
        flex-direction: column;
        height: 100%;
        min-height: 0;
        overflow: hidden;
        border-radius: 0.75rem;
        border: 1px solid var(--app-border);
        background: var(--app-surface);
      }
    `,
  ],
})
export class MonitoringMapComponent implements AfterViewInit, OnDestroy {
  private readonly loader = inject(GoogleMapsLoaderService);
  private readonly changeDetector = inject(ChangeDetectorRef);

  readonly surveys = input<readonly MonitoringSurveyMarker[]>([]);
  readonly teams = input<readonly MonitoringTeamMarker[]>([]);
  readonly selectedSurveyId = model<number | null>(null);
  readonly selectedTeamId = model<number | null>(null);

  readonly surveySelected = output<number>();
  readonly teamSelected = output<number>();

  protected readonly error = signal<string | null>(null);
  protected readonly mapType = signal<MapType>(MAP_TYPES.Roadmap);
  protected readonly roadmap = MAP_TYPES.Roadmap;
  protected readonly satellite = MAP_TYPES.Satellite;
  protected readonly activeViewClass = 'bg-[var(--p-primary-color)] text-white';
  protected readonly idleViewClass = 'text-[var(--p-text-color)] hover:bg-app-hover';

  private readonly canvas = viewChild.required<ElementRef<HTMLDivElement>>('canvas');

  private api: GoogleMapsApi | null = null;
  private map: GoogleMapInstance | null = null;
  private infoWindow: GoogleInfoWindowInstance | null = null;
  private surveyMarkers = new Map<number, GoogleMarkerInstance>();
  private teamMarkers = new Map<number, GoogleMarkerInstance>();
  private destroyed = false;
  private hasFitted = false;

  constructor() {
    effect(() => {
      const surveys = this.surveys();
      const teams = this.teams();
      if (this.map) {
        this.draw(surveys, teams);
      }
    });

    effect(() => {
      const surveyId = this.selectedSurveyId();
      if (this.map && surveyId !== null) {
        this.focusSurvey(surveyId);
      }
    });
  }

  async ngAfterViewInit(): Promise<void> {
    if (!this.loader.isConfigured) {
      this.error.set('geoMap.notConfigured');
      return;
    }

    try {
      const api = await this.loader.load();
      if (this.destroyed) {
        return;
      }

      this.api = api;
      this.map = new api.Map(this.canvas().nativeElement, {
        center: SAUDI_ARABIA_CENTER,
        zoom: SAUDI_ARABIA_ZOOM,
        mapTypeId: this.mapType(),
        mapTypeControl: false,
        streetViewControl: false,
        fullscreenControl: true,
        clickableIcons: false,
      });

      this.infoWindow = new api.InfoWindow();
      this.draw(this.surveys(), this.teams());
    } catch {
      this.error.set('geoMap.loadFailed');
    } finally {
      this.changeDetector.markForCheck();
    }
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.clearOverlays();
    this.infoWindow?.close();
    this.infoWindow = null;
    this.map = null;
    this.api = null;
  }

  protected setMapType(type: MapType): void {
    this.mapType.set(type);
    this.map?.setMapTypeId(type);
  }

  protected fitToMarkers(): void {
    this.hasFitted = false;
    this.fitBounds(this.surveys(), this.teams());
  }

  private draw(
    surveys: readonly MonitoringSurveyMarker[],
    teams: readonly MonitoringTeamMarker[],
  ): void {
    const api = this.api;
    const map = this.map;
    if (!api || !map) {
      return;
    }

    this.clearOverlays();

    for (const survey of surveys) {
      const fill = STATUS_PIN_COLORS[survey.status] ?? '#64748b';
      const allocated = survey.allocatedFieldTeamId != null;
      const marker = new api.Marker({
        position: { lat: survey.latitude, lng: survey.longitude },
        map,
        title: survey.surveyCode,
        opacity: survey.dimmed ? DIMMED_OPACITY : 1,
        zIndex: survey.dimmed ? 1 : allocated ? 5 : 3,
        icon: {
          url: surveyPinIcon(fill, allocated, !!survey.dimmed),
          scaledSize: new api.Size(PIN_SIZE, PIN_SIZE),
          anchor: new api.Point(PIN_SIZE / 2, PIN_SIZE / 2),
        },
      });

      marker.addListener('click', () => {
        this.selectedSurveyId.set(survey.surveyId);
        this.surveySelected.emit(survey.surveyId);
      });

      this.surveyMarkers.set(survey.surveyId, marker);
    }

    for (const team of teams) {
      const fill = team.emphasized ? TEAM_PIN_EMPHASIZED : TEAM_PIN_COLOR;
      const marker = new api.Marker({
        position: { lat: team.latitude, lng: team.longitude },
        map,
        title: team.name,
        zIndex: team.emphasized ? 20 : 10,
        icon: {
          url: teamPinIcon(fill, !!team.emphasized),
          scaledSize: new api.Size(TEAM_PIN_SIZE, TEAM_PIN_SIZE),
          anchor: new api.Point(TEAM_PIN_SIZE / 2, TEAM_PIN_SIZE / 2),
        },
      });

      marker.addListener('click', () => {
        this.selectedTeamId.set(team.teamId);
        this.teamSelected.emit(team.teamId);
        this.openTeamInfo(team, marker);
      });

      this.teamMarkers.set(team.teamId, marker);
    }

    if (!this.hasFitted && (surveys.length > 0 || teams.length > 0)) {
      this.fitBounds(surveys, teams);
      this.hasFitted = true;
    }

    const selectedId = this.selectedSurveyId();
    if (selectedId !== null) {
      this.focusSurvey(selectedId);
    }

    this.changeDetector.markForCheck();
  }

  private fitBounds(
    surveys: readonly MonitoringSurveyMarker[],
    teams: readonly MonitoringTeamMarker[],
  ): void {
    if (!this.map || !this.api) {
      return;
    }

    const points: GeoPoint[] = [
      ...surveys.filter((s) => !s.dimmed).map((s) => ({ lat: s.latitude, lng: s.longitude })),
      ...teams.map((t) => ({ lat: t.latitude, lng: t.longitude })),
    ];

    if (points.length === 0) {
      const all = [
        ...surveys.map((s) => ({ lat: s.latitude, lng: s.longitude })),
        ...teams.map((t) => ({ lat: t.latitude, lng: t.longitude })),
      ];
      if (all.length === 0) {
        this.map.setCenter(SAUDI_ARABIA_CENTER);
        this.map.setZoom(SAUDI_ARABIA_ZOOM);
        return;
      }
      points.push(...all);
    }

    if (points.length === 1) {
      this.map.setCenter(points[0]!);
      this.map.setZoom(SINGLE_STOP_ZOOM);
      return;
    }

    const bounds = new this.api.LatLngBounds();
    for (const point of points) {
      bounds.extend(point);
    }
    this.map.fitBounds(bounds, BOUNDS_PADDING);
  }

  private focusSurvey(surveyId: number): void {
    if (!this.infoWindow || !this.map) {
      return;
    }

    const marker = this.surveyMarkers.get(surveyId);
    const survey = this.surveys().find((x) => x.surveyId === surveyId);
    if (!marker || !survey) {
      return;
    }

    this.infoWindow.setContent(surveyInfoHtml(survey));
    this.infoWindow.open({ map: this.map, anchor: marker });
    this.map.panTo({ lat: survey.latitude, lng: survey.longitude });
    if ((this.map.getZoom() ?? 0) < FOCUSED_ZOOM) {
      this.map.setZoom(FOCUSED_ZOOM);
    }
  }

  private openTeamInfo(team: MonitoringTeamMarker, marker: GoogleMarkerInstance): void {
    if (!this.infoWindow || !this.map) {
      return;
    }

    this.infoWindow.setContent(
      `<div style="min-width:140px;color:#1f2937;">` +
        `<div style="font-weight:700;font-size:0.8rem;">${escapeHtml(team.name)}</div>` +
        `<div style="font-size:0.7rem;opacity:0.7;">${team.isOnline ? '● Online' : '○ Offline'}</div>` +
        `</div>`,
    );
    this.infoWindow.open({ map: this.map, anchor: marker });
  }

  private clearOverlays(): void {
    for (const marker of this.surveyMarkers.values()) {
      marker.setMap(null);
    }
    this.surveyMarkers.clear();

    for (const marker of this.teamMarkers.values()) {
      marker.setMap(null);
    }
    this.teamMarkers.clear();

    this.infoWindow?.close();
  }
}

function surveyInfoHtml(survey: MonitoringSurveyMarker): string {
  const lines = [
    `<div style="font-weight:700;font-size:0.8rem;">${escapeHtml(survey.surveyCode)}</div>`,
    `<div style="font-size:0.75rem;opacity:0.8;">${escapeHtml(survey.status)}</div>`,
  ];

  if (survey.faId) {
    lines.push(`<div style="font-size:0.7rem;opacity:0.65;">FA: ${escapeHtml(survey.faId)}</div>`);
  }

  if (survey.allocatedFieldTeamName) {
    lines.push(
      `<div style="font-size:0.7rem;opacity:0.8;">Team: ${escapeHtml(survey.allocatedFieldTeamName)}</div>`,
    );
  }

  return `<div style="min-width:150px;color:#1f2937;">${lines.join('')}</div>`;
}
