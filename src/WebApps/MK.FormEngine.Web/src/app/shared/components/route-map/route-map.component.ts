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
  signal,
  viewChild,
} from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { MessageModule } from 'primeng/message';
import { TooltipModule } from 'primeng/tooltip';
import { TranslocoDirective } from '@jsverse/transloco';
import { GoogleMapsLoaderService } from '../geo-map/google-maps-loader.service';
import { SAUDI_ARABIA_CENTER, SAUDI_ARABIA_ZOOM } from '../geo-map/geo-map.component';
import {
  MAP_TYPES,
  type GeoPoint,
  type GoogleInfoWindowInstance,
  type GoogleMapInstance,
  type GoogleMapsApi,
  type GoogleMarkerInstance,
  type GooglePolylineInstance,
  type MapType,
} from '../geo-map/google-maps.types';
import type { RouteMapStop } from './route-map.types';

/** Zoom used when a single stop is all there is to frame — `fitBounds` alone would zoom to the max. */
const SINGLE_STOP_ZOOM = 15;

/** Zoom used when the user focuses one stop out of several. */
const FOCUSED_ZOOM = 15;

/** Pixels of breathing room left around the framed stops. */
const BOUNDS_PADDING = 48;

const PIN_SIZE = 34;
const PIN_RADIUS = 14;
const PIN_STROKE = 3;

/** Start / intermediate / end. Distinct enough to read at a glance without a legend. */
const PIN_COLORS = {
  start: '#16a34a',
  stop: '#0284c7',
  end: '#dc2626',
} as const;

/** Drawn beneath the pins, so a marker is never hidden by the line leaving it. */
const ROUTE_LINE = {
  color: '#0284c7',
  opacity: 0.75,
  weight: 4,
  zIndex: 1,
} as const;

/** Raised above the intermediate pins so the two ends of the day stay findable in a cluster. */
const ENDPOINT_Z_INDEX = 10;

/** A line needs two points; one stop is a place, not a route. */
const MIN_STOPS_FOR_LINE = 2;

/**
 * A circular pin carrying the stop number. Inlined as a data URI rather than shipped as an asset:
 * the colour varies per pin, and three colours would otherwise be three files to keep in step.
 */
function pinIcon(fill: string): string {
  const centre = PIN_SIZE / 2;
  const svg =
    `<svg xmlns="http://www.w3.org/2000/svg" width="${PIN_SIZE}" height="${PIN_SIZE}" viewBox="0 0 ${PIN_SIZE} ${PIN_SIZE}">` +
    `<circle cx="${centre}" cy="${centre}" r="${PIN_RADIUS}" fill="${fill}" stroke="#ffffff" stroke-width="${PIN_STROKE}"/>` +
    `</svg>`;
  return `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`;
}

/** Stop titles are user data and go into the info window as markup, so they are escaped first. */
function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

/**
 * Read-only map of an ordered set of stops: numbered pins joined by a line, framed to fit.
 *
 * A sibling of `GeoMapComponent` rather than a mode of it. That one is a single-value picker whose
 * whole surface — `model()` binding, reverse geocoding, the address box, Places search — exists to
 * choose one coordinate. None of it applies to showing a day that has already happened, and folding
 * the two together would mean guarding every one of those behaviours against a mode that never wants
 * them. What is genuinely shared — the API loader, the default view, the map/satellite toggle — is
 * imported from it.
 */
@Component({
  selector: 'app-route-map',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonModule, MessageModule, TooltipModule, TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
      <div
        class="rounded-xl border border-[var(--app-border)] bg-[var(--app-surface)] overflow-hidden shadow-sm"
      >
        @if (error()) {
          <p-message severity="warn" [text]="t(error()!)" styleClass="w-full rounded-none border-0" />
        }

        <div class="relative">
          <div #canvas class="w-full bg-[var(--app-surface-alt)]" [style.height]="height()"></div>

          <!-- View switch, top-start so it clears Google's own controls -->
          <div
            class="absolute top-2 start-2 flex rounded-lg overflow-hidden shadow-md bg-white/95 backdrop-blur-sm"
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

          @if (!error() && stops().length === 0) {
            <div class="absolute inset-0 flex items-center justify-center pointer-events-none">
              <span
                class="rounded-lg bg-[var(--app-surface)]/90 px-4 py-2 text-sm font-medium text-[var(--p-text-muted-color)] shadow-sm"
              >
                <i class="pi pi-map-marker me-1"></i>{{ t('tracking.noMappedStops') }}
              </span>
            </div>
          }
        </div>

        <!-- Legend + recentre -->
        <div class="px-3 py-2.5 border-t border-[var(--app-border)] flex flex-wrap items-center gap-3">
          <span class="legend-item">
            <span class="legend-dot" [style.background]="colors.start"></span>
            {{ t('tracking.legendStart') }}
          </span>
          <span class="legend-item">
            <span class="legend-dot" [style.background]="colors.stop"></span>
            {{ t('tracking.legendStop') }}
          </span>
          <span class="legend-item">
            <span class="legend-dot" [style.background]="colors.end"></span>
            {{ t('tracking.legendEnd') }}
          </span>

          <span class="flex-1"></span>

          <p-button
            icon="pi pi-arrows-alt"
            size="small"
            [text]="true"
            severity="secondary"
            [disabled]="stops().length === 0"
            [pTooltip]="t('tracking.fitRoute')"
            [ariaLabel]="t('tracking.fitRoute')"
            (onClick)="fitToStops()"
          />
        </div>
      </div>
    </ng-container>
  `,
  styles: [
    `
      .legend-item {
        display: inline-flex;
        align-items: center;
        gap: 0.35rem;
        font-size: 0.7rem;
        font-weight: 600;
        color: var(--p-text-muted-color);
        white-space: nowrap;
      }
      .legend-dot {
        width: 0.65rem;
        height: 0.65rem;
        border-radius: 9999px;
        border: 2px solid #ffffff;
        box-shadow: 0 0 0 1px rgba(0, 0, 0, 0.12);
      }
    `,
  ],
})
export class RouteMapComponent implements AfterViewInit, OnDestroy {
  private readonly loader = inject(GoogleMapsLoaderService);
  private readonly changeDetector = inject(ChangeDetectorRef);

  /** Already ordered and already filtered to stops that have a coordinate. */
  readonly stops = input<RouteMapStop[]>([]);
  readonly height = input('520px');

  /** Two-way with the stop list beside the map: clicking either highlights the other. */
  readonly activeStopId = model<number | null>(null);

  protected readonly error = signal<string | null>(null);
  protected readonly mapType = signal<MapType>(MAP_TYPES.Roadmap);

  protected readonly roadmap = MAP_TYPES.Roadmap;
  protected readonly satellite = MAP_TYPES.Satellite;
  protected readonly colors = PIN_COLORS;
  protected readonly activeViewClass = 'bg-[var(--p-primary-color)] text-white';
  protected readonly idleViewClass = 'text-[var(--p-text-color)] hover:bg-app-hover';

  private readonly canvas = viewChild.required<ElementRef<HTMLDivElement>>('canvas');

  private api: GoogleMapsApi | null = null;
  private map: GoogleMapInstance | null = null;
  private polyline: GooglePolylineInstance | null = null;
  private infoWindow: GoogleInfoWindowInstance | null = null;
  private markers = new Map<number, GoogleMarkerInstance>();
  private destroyed = false;

  constructor() {
    // Redraw whenever the day changes. Guarded because the first run happens before the API has
    // loaded; `ngAfterViewInit` draws that first set itself.
    effect(() => {
      const stops = this.stops();
      if (this.map) {
        this.draw(stops);
      }
    });

    effect(() => {
      const activeStopId = this.activeStopId();
      if (this.map) {
        this.focus(activeStopId);
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
        // Replaced by our own compact view switch.
        mapTypeControl: false,
        streetViewControl: false,
        fullscreenControl: true,
        clickableIcons: false,
      });

      this.infoWindow = new api.InfoWindow();
      this.draw(this.stops());
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

  /** Frames the whole day again after the user has panned or zoomed into one stop. */
  protected fitToStops(): void {
    const stops = this.stops();
    if (!this.map || !this.api || stops.length === 0) {
      return;
    }

    if (stops.length === 1) {
      this.map.setCenter(toPoint(stops[0]));
      this.map.setZoom(SINGLE_STOP_ZOOM);
      return;
    }

    const bounds = new this.api.LatLngBounds();
    for (const stop of stops) {
      bounds.extend(toPoint(stop));
    }
    this.map.fitBounds(bounds, BOUNDS_PADDING);
  }

  /**
   * Rebuilds every overlay from scratch. Cheaper than diffing and impossible to get subtly wrong:
   * a day is at most a few dozen pins, and the alternative is reconciling sequence numbers, colours
   * and the line's path against whatever was drawn before.
   */
  private draw(stops: readonly RouteMapStop[]): void {
    const api = this.api;
    const map = this.map;
    if (!api || !map) {
      return;
    }

    this.clearOverlays();

    if (stops.length === 0) {
      map.setCenter(SAUDI_ARABIA_CENTER);
      map.setZoom(SAUDI_ARABIA_ZOOM);
      this.changeDetector.markForCheck();
      return;
    }

    const lastIndex = stops.length - 1;

    stops.forEach((stop, index) => {
      const isStart = index === 0;
      const isEnd = index === lastIndex && stops.length > 1;
      const fill = isStart ? PIN_COLORS.start : isEnd ? PIN_COLORS.end : PIN_COLORS.stop;

      const marker = new api.Marker({
        position: toPoint(stop),
        map,
        title: stop.title,
        zIndex: isStart || isEnd ? ENDPOINT_Z_INDEX : undefined,
        label: {
          text: String(stop.sequence),
          color: '#ffffff',
          fontSize: '12px',
          fontWeight: '700',
        },
        icon: {
          url: pinIcon(fill),
          scaledSize: new api.Size(PIN_SIZE, PIN_SIZE),
          labelOrigin: new api.Point(PIN_SIZE / 2, PIN_SIZE / 2),
          anchor: new api.Point(PIN_SIZE / 2, PIN_SIZE / 2),
        },
      });

      marker.addListener('click', () => {
        this.activeStopId.set(stop.id);
      });

      this.markers.set(stop.id, marker);
    });

    if (stops.length >= MIN_STOPS_FOR_LINE) {
      this.polyline = new api.Polyline({
        path: stops.map(toPoint),
        map,
        strokeColor: ROUTE_LINE.color,
        strokeOpacity: ROUTE_LINE.opacity,
        strokeWeight: ROUTE_LINE.weight,
        geodesic: true,
        zIndex: ROUTE_LINE.zIndex,
      });
    }

    this.fitToStops();
    this.focus(this.activeStopId());
    this.changeDetector.markForCheck();
  }

  /** Pans to the selected stop and names it. A cleared selection just closes the bubble. */
  private focus(stopId: number | null): void {
    if (!this.infoWindow || !this.map) {
      return;
    }

    if (stopId === null) {
      this.infoWindow.close();
      return;
    }

    const marker = this.markers.get(stopId);
    const stop = this.stops().find((x) => x.id === stopId);
    if (!marker || !stop) {
      this.infoWindow.close();
      return;
    }

    this.infoWindow.setContent(infoWindowContent(stop));
    this.infoWindow.open({ map: this.map, anchor: marker });
    this.map.panTo(toPoint(stop));

    // Only zoom in if the map is still wider than a working view; re-zooming on every selection
    // would fight a user who has deliberately pulled back to see the whole day.
    if ((this.map.getZoom() ?? 0) < FOCUSED_ZOOM) {
      this.map.setZoom(FOCUSED_ZOOM);
    }
  }

  private clearOverlays(): void {
    for (const marker of this.markers.values()) {
      marker.setMap(null);
    }
    this.markers.clear();

    this.polyline?.setMap(null);
    this.polyline = null;

    this.infoWindow?.close();
  }
}

function toPoint(stop: RouteMapStop): GeoPoint {
  return { lat: stop.lat, lng: stop.lng };
}

function infoWindowContent(stop: RouteMapStop): string {
  const lines = [
    `<div style="font-weight:700;font-size:0.8rem;">${stop.sequence}. ${escapeHtml(stop.title)}</div>`,
  ];

  if (stop.subtitle) {
    lines.push(`<div style="font-size:0.75rem;opacity:0.8;">${escapeHtml(stop.subtitle)}</div>`);
  }

  if (stop.time) {
    lines.push(`<div style="font-size:0.7rem;opacity:0.65;">${escapeHtml(stop.time)}</div>`);
  }

  return `<div style="min-width:150px;color:#1f2937;">${lines.join('')}</div>`;
}
