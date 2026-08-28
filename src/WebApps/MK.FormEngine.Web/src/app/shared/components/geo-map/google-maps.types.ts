/**
 * Minimal typings for the slice of the Google Maps JS API this app uses.
 *
 * Declared locally instead of pulling in `@types/google.maps` — the surface is
 * small and stable, and this keeps the dependency list unchanged.
 */

/**
 * A WGS-84 coordinate plus the street address it resolves to. This is also the shape stored in the
 * form model and submitted as the answer.
 *
 * `address` is optional everywhere: a template default carries only `"lat,lng"`, a mobile client may
 * not send one, and a pin dropped somewhere the geocoder cannot name keeps its coordinates without
 * it. Nothing may treat a point as invalid for lacking an address.
 */
export interface GeoPoint {
  lat: number;
  lng: number;
  address?: string | null;
}

export interface GoogleLatLng {
  lat(): number;
  lng(): number;
}

export interface GoogleMapMouseEvent {
  latLng: GoogleLatLng | null;
}

export interface GoogleMapInstance {
  setCenter(position: GeoPoint): void;
  setZoom(zoom: number): void;
  getZoom(): number | undefined;
  panTo(position: GeoPoint): void;
  setMapTypeId(mapTypeId: string): void;
  /** Frames every point in the bounds. Used by the route view, which has no single subject. */
  fitBounds(bounds: GoogleLatLngBoundsInstance, padding?: number): void;
  addListener(event: string, handler: (event: GoogleMapMouseEvent) => void): void;
}

/** The two views a field surveyor actually switches between. */
export const MAP_TYPES = {
  Roadmap: 'roadmap',
  Satellite: 'hybrid',
} as const;

export type MapType = (typeof MAP_TYPES)[keyof typeof MAP_TYPES];

export interface GoogleMarkerInstance {
  setPosition(position: GeoPoint | null): void;
  setMap(map: GoogleMapInstance | null): void;
  setDraggable(draggable: boolean): void;
  getPosition(): GoogleLatLng | undefined;
  addListener(event: string, handler: (event: GoogleMapMouseEvent) => void): void;
}

/** Text drawn inside the pin — the stop number on a route. */
export interface GoogleMarkerLabel {
  text: string;
  color?: string;
  fontSize?: string;
  fontWeight?: string;
}

/**
 * A marker graphic supplied as an image. Given as a data URI so the route pins stay self-contained
 * — no extra network request, and nothing to break if an asset path moves.
 */
export interface GoogleIcon {
  url: string;
  scaledSize?: GoogleSize;
  labelOrigin?: GooglePoint;
  anchor?: GooglePoint;
}

export interface GoogleSize {
  width: number;
  height: number;
}

export interface GooglePoint {
  x: number;
  y: number;
}

export interface GoogleMapOptions {
  center: GeoPoint;
  zoom: number;
  mapTypeId?: string;
  mapTypeControl?: boolean;
  streetViewControl?: boolean;
  fullscreenControl?: boolean;
  clickableIcons?: boolean;
  gestureHandling?: string;
}

export interface GoogleMarkerOptions {
  position: GeoPoint;
  map: GoogleMapInstance;
  draggable?: boolean;
  label?: string | GoogleMarkerLabel;
  icon?: GoogleIcon;
  title?: string;
  /** Raised on the start and end pins so a dense cluster does not bury them. */
  zIndex?: number;
  /** 0–1. Used to dim markers that are outside the current team filter. */
  opacity?: number;
}

/* ------------------------------------------------------------------ *
 * Route overlays — the line through a day's stops and the box that
 * frames them. Only used by the route map; the picker needs neither.
 * ------------------------------------------------------------------ */

export interface GooglePolylineOptions {
  path: GeoPoint[];
  map: GoogleMapInstance;
  strokeColor?: string;
  strokeOpacity?: number;
  strokeWeight?: number;
  /** Follows the great circle rather than the flat projection. */
  geodesic?: boolean;
  zIndex?: number;
}

export interface GooglePolylineInstance {
  setPath(path: GeoPoint[]): void;
  setMap(map: GoogleMapInstance | null): void;
}

export interface GoogleLatLngBoundsInstance {
  extend(position: GeoPoint): void;
  isEmpty(): boolean;
}

export interface GoogleInfoWindowOptions {
  content?: string;
}

export interface GoogleInfoWindowInstance {
  setContent(content: string): void;
  open(options: { map: GoogleMapInstance; anchor: GoogleMarkerInstance }): void;
  close(): void;
}

/* ------------------------------------------------------------------ *
 * Geocoding — turns the picked coordinate into a readable address.
 * ------------------------------------------------------------------ */

export interface GeocoderRequest {
  location: GeoPoint;
  /** `en` / `ar` — the address is geocoded in the language the surveyor is filling in. */
  language?: string;
  region?: string;
}

/** Google's own naming; snake_case is the API's, not ours. */
export interface GeocoderResult {
  formatted_address?: string;
}

export interface GeocoderResponse {
  results: GeocoderResult[];
}

export interface GoogleGeocoder {
  geocode(request: GeocoderRequest): Promise<GeocoderResponse>;
}

/* ------------------------------------------------------------------ *
 * Places — the "search for an address" box above the map.
 * ------------------------------------------------------------------ */

export interface GooglePlace {
  location?: GoogleLatLng | null;
  formattedAddress?: string | null;
  fetchFields(request: { fields: string[] }): Promise<{ place: GooglePlace }>;
}

export interface GooglePlacePrediction {
  toPlace(): GooglePlace;
}

/** Payload of the element's `gmp-select` event. */
export interface PlaceSelectEvent {
  placePrediction?: GooglePlacePrediction | null;
}

export interface PlaceAutocompleteOptions {
  /** ISO country codes the suggestions are limited to. */
  includedRegionCodes?: string[];
}

/**
 * `google.maps.places.PlaceAutocompleteElement` — a custom element that renders its own input and
 * suggestion list. Used instead of the legacy `places.Autocomplete`, which Google closed to keys
 * created after March 2025.
 */
export type GooglePlaceAutocompleteElement = HTMLElement;

export interface GooglePlacesLibrary {
  PlaceAutocompleteElement: new (
    options?: PlaceAutocompleteOptions,
  ) => GooglePlaceAutocompleteElement;
}

/** The `google.maps` namespace, narrowed to what we call. */
export interface GoogleMapsApi {
  Map: new (container: HTMLElement, options: GoogleMapOptions) => GoogleMapInstance;
  Marker: new (options: GoogleMarkerOptions) => GoogleMarkerInstance;
  Geocoder: new () => GoogleGeocoder;
  Polyline: new (options: GooglePolylineOptions) => GooglePolylineInstance;
  LatLngBounds: new () => GoogleLatLngBoundsInstance;
  InfoWindow: new (options?: GoogleInfoWindowOptions) => GoogleInfoWindowInstance;
  Size: new (width: number, height: number) => GoogleSize;
  Point: new (x: number, y: number) => GooglePoint;
  /** Absent when the script was loaded without `libraries=places`. */
  places?: GooglePlacesLibrary;
}
