import { Observable } from 'rxjs';

/** Last-known (or simulated) position of a field team on the monitoring map. */
export interface TeamLocation {
  readonly teamId: number;
  readonly latitude: number;
  readonly longitude: number;
  readonly updatedAt: Date;
  readonly isOnline: boolean;
}

/**
 * Source of live team positions for the monitoring map.
 * Swap the provider from MockTeamLocationService to an HTTP/SignalR implementation later
 * without changing the page or map components.
 */
export abstract class TeamLocationService {
  /**
   * Emits the latest locations for the given teams. Implementations may poll, push, or simulate.
   * Completing / unsubscribing stops the stream.
   */
  abstract watchLocations(teamIds: readonly number[]): Observable<readonly TeamLocation[]>;

  /**
   * Optional seed hints (e.g. survey centroids) so a mock can place crews near their work.
   * Real HTTP implementations can ignore this.
   */
  setSeedHints(_hints: ReadonlyMap<number, { lat: number; lng: number }>): void {
    // no-op by default
  }
}
