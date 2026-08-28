import { Injectable } from '@angular/core';
import { Observable, interval, map, startWith } from 'rxjs';
import { SAUDI_ARABIA_CENTER } from '../../../shared/components/geo-map/geo-map.component';
import { TeamLocation, TeamLocationService } from './team-location.service';

const TICK_MS = 5_000;
/** Rough degrees of random walk per tick (~tens of metres near Riyadh). */
const JITTER = 0.00035;

interface MutableLocation {
  teamId: number;
  latitude: number;
  longitude: number;
  isOnline: boolean;
}

/**
 * Simulates crew GPS for the monitoring map until a real mobile ingest endpoint exists.
 * Seeds each team near the centroid of its allocated open surveys when hints are provided.
 */
@Injectable()
export class MockTeamLocationService extends TeamLocationService {
  private readonly state = new Map<number, MutableLocation>();
  private seedHints = new Map<number, { lat: number; lng: number }>();

  override setSeedHints(hints: ReadonlyMap<number, { lat: number; lng: number }>): void {
    this.seedHints = new Map(hints);
    for (const [teamId, hint] of this.seedHints) {
      const existing = this.state.get(teamId);
      if (!existing) {
        this.state.set(teamId, {
          teamId,
          latitude: hint.lat + (Math.random() - 0.5) * JITTER * 4,
          longitude: hint.lng + (Math.random() - 0.5) * JITTER * 4,
          isOnline: true,
        });
      }
    }
  }

  override watchLocations(teamIds: readonly number[]): Observable<readonly TeamLocation[]> {
    for (const teamId of teamIds) {
      if (!this.state.has(teamId)) {
        const hint = this.seedHints.get(teamId);
        this.state.set(teamId, {
          teamId,
          latitude: hint?.lat ?? SAUDI_ARABIA_CENTER.lat + (Math.random() - 0.5) * 0.4,
          longitude: hint?.lng ?? SAUDI_ARABIA_CENTER.lng + (Math.random() - 0.5) * 0.4,
          isOnline: true,
        });
      }
    }

    const idSet = new Set(teamIds);

    return interval(TICK_MS).pipe(
      startWith(0),
      map(() => {
        const now = new Date();
        const result: TeamLocation[] = [];

        for (const teamId of idSet) {
          const loc = this.state.get(teamId);
          if (!loc) {
            continue;
          }

          loc.latitude += (Math.random() - 0.5) * JITTER;
          loc.longitude += (Math.random() - 0.5) * JITTER;
          loc.isOnline = true;

          result.push({
            teamId: loc.teamId,
            latitude: loc.latitude,
            longitude: loc.longitude,
            updatedAt: now,
            isOnline: loc.isOnline,
          });
        }

        return result;
      }),
    );
  }
}
