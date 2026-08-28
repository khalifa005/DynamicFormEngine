import { Injectable, inject } from '@angular/core';
import { AppConfigService } from '../../../core/config/app-config.service';
import type { GoogleMapsApi } from './google-maps.types';

const SCRIPT_ID = 'google-maps-js-api';
const SCRIPT_BASE = 'https://maps.googleapis.com/maps/api/js';

/** Bias geocoding/behaviour towards Saudi Arabia. */
const REGION = 'SA';

/** Requested for the address search box; see `hasPlaces`. */
const PLACES_LIBRARY = 'places';

interface GoogleGlobal {
  maps?: GoogleMapsApi;
}

/**
 * Resolves the Google Maps JS API, however the page provides it.
 *
 * `index.html` loads the API with a plain `<script>` tag, so in practice the
 * namespace already exists by the time Angular bootstraps and this service just
 * hands it over. The injection path stays as a fallback: it keeps the map
 * working if the tag is ever removed, deferred, or the key moves to runtime
 * config (`app-config.json` → `googleMapsApiKey`) for per-environment builds.
 */
@Injectable({ providedIn: 'root' })
export class GoogleMapsLoaderService {
  private readonly config = inject(AppConfigService);
  private pending: Promise<GoogleMapsApi> | null = null;

  /** False only when the API is nowhere to be found — callers show a hint then. */
  get isConfigured(): boolean {
    return !!this.resolveExisting() || !!this.findScriptTag() || !!this.config.snapshot.googleMapsApiKey;
  }

  /**
   * Whether the loaded API carries the Places library. A script tag that predates the address
   * search box does not request it, so the box is hidden rather than allowed to throw — the map
   * itself still works and the pin can still be dropped by hand.
   */
  hasPlaces(api: GoogleMapsApi): boolean {
    return !!api.places?.PlaceAutocompleteElement;
  }

  load(): Promise<GoogleMapsApi> {
    const existing = this.resolveExisting();
    if (existing) {
      return Promise.resolve(existing);
    }

    // A tag is present but has not finished loading (deferred/async): wait for
    // it rather than requesting a second copy of the API.
    const tag = this.findScriptTag();
    if (tag) {
      this.pending ??= this.awaitScript(tag);
      return this.pending;
    }

    if (!this.config.snapshot.googleMapsApiKey) {
      return Promise.reject(new Error('Google Maps API key is not configured.'));
    }
    this.pending ??= this.injectScript();
    return this.pending;
  }

  private resolveExisting(): GoogleMapsApi | null {
    return (window as unknown as { google?: GoogleGlobal }).google?.maps ?? null;
  }

  /** Any Maps API script already on the page — ours or one in `index.html`. */
  private findScriptTag(): HTMLScriptElement | null {
    return document.querySelector<HTMLScriptElement>(`script[src^="${SCRIPT_BASE}"]`);
  }

  private awaitScript(tag: HTMLScriptElement): Promise<GoogleMapsApi> {
    return new Promise<GoogleMapsApi>((resolve, reject) => {
      tag.addEventListener('load', () => {
        const api = this.resolveExisting();
        if (api) {
          resolve(api);
          return;
        }
        this.pending = null;
        reject(new Error('Google Maps loaded but the namespace is unavailable.'));
      });
      tag.addEventListener('error', () => {
        this.pending = null;
        reject(new Error('Failed to load the Google Maps script.'));
      });
    });
  }

  private injectScript(): Promise<GoogleMapsApi> {
    return new Promise<GoogleMapsApi>((resolve, reject) => {
      const params = new URLSearchParams({
        key: this.config.snapshot.googleMapsApiKey ?? '',
        region: REGION,
        language: document.documentElement.lang || 'en',
        // Backs the geolocation picker's address search box. Kept in step with
        // the tag in `index.html`, which is the path actually taken in practice.
        libraries: PLACES_LIBRARY,
        loading: 'async',
        v: 'weekly',
      });

      const script = document.createElement('script');
      script.id = SCRIPT_ID;
      script.src = `${SCRIPT_BASE}?${params.toString()}`;
      script.async = true;
      script.onload = () => {
        const api = this.resolveExisting();
        if (api) {
          resolve(api);
          return;
        }
        this.pending = null;
        reject(new Error('Google Maps loaded but the namespace is unavailable.'));
      };
      script.onerror = () => {
        // Allow a later retry (transient network, key just fixed, …).
        this.pending = null;
        script.remove();
        reject(new Error('Failed to load the Google Maps script.'));
      };

      document.head.appendChild(script);
    });
  }
}
