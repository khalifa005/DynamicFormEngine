/**
 * One drawable stop on a route. Deliberately free of survey concepts — the map draws numbered
 * points joined by a line, and the page decides what a point means.
 */
export interface RouteMapStop {
  /** Identifies the stop back to the caller; the survey id in the tracking page. */
  id: number;

  /** 1-based position in the day, drawn inside the pin. */
  sequence: number;

  lat: number;
  lng: number;

  /** Bold first line of the info window. */
  title: string;

  /** Optional second line — a template name, an FA id. */
  subtitle?: string | null;

  /** Optional third line, already formatted by the caller in its own locale. */
  time?: string | null;
}
