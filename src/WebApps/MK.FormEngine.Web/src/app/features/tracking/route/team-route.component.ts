import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { finalize } from 'rxjs';

import { BadgeModule } from 'primeng/badge';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { MessageService } from 'primeng/api';
import { SelectModule } from 'primeng/select';
import { SkeletonModule } from 'primeng/skeleton';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';

import {
  FsmsTeamsClient,
  TeamSurveyRouteDto,
  TeamSurveyRouteStopDto,
} from '../../../core/api/api-client.generated';
import { apiErrorMessage } from '../../../core/api/api-error';
import { LanguageService } from '../../../core/i18n/language.service';
import { RouteMapComponent } from '../../../shared/components/route-map/route-map.component';
import type { RouteMapStop } from '../../../shared/components/route-map/route-map.types';
import { surveyStatusSeverity } from '../../surveys/survey-status';

interface TeamOption {
  readonly label: string;
  readonly value: number;
}

/** Enough to hold every active crew in the picker, which filters client-side from there. */
const TEAM_PAGE_SIZE = 500;

/** Below this a leg reads better in metres than as "0.1 km". */
const METRES_PER_KILOMETRE = 1000;

const MINUTES_PER_HOUR = 60;

const EMPTY_VALUE = '—';

/**
 * Query `DateOnly` on the API expects `yyyy-MM-dd`. The NSwag client always serialises `Date`
 * params with `toISOString()`, which emits a full UTC timestamp the binder rejects. Build a Date
 * whose `toISOString()` returns the civil day the picker showed (local Y/M/D, no timezone shift).
 */
function toDateOnly(local: Date): Date {
  const year = local.getFullYear();
  const month = local.getMonth();
  const day = local.getDate();
  const yyyyMmDd = `${year}-${pad2(month + 1)}-${pad2(day)}`;
  const date = new Date(Date.UTC(year, month, day));
  date.toISOString = () => yyyyMmDd;
  return date;
}

function pad2(value: number): string {
  return String(value).padStart(2, '0');
}

/**
 * One crew's day on a map. The stops are the surveys they filled that date in the order they filled
 * them — see the endpoint's own remarks: nothing records where a surveyor stood, so the pins are the
 * assets' locations and the line between them is a connector, not a path that was driven. Both
 * caveats are said on the page rather than left for the viewer to infer.
 */
@Component({
  selector: 'app-team-route',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslocoDirective,
    BadgeModule,
    ButtonModule,
    DatePickerModule,
    SelectModule,
    SkeletonModule,
    TableModule,
    TagModule,
    ToastModule,
    TooltipModule,
    RouteMapComponent,
  ],
  providers: [MessageService],
  templateUrl: './team-route.component.html',
  styles: [
    `
      /* The row matching the pin the map has open. ::ng-deep because the row is projected into
         p-table's own template, so the component's emulated encapsulation cannot reach it. */
      :host ::ng-deep tr.stop-row-active > td {
        background: color-mix(in srgb, var(--p-primary-color) 14%, transparent) !important;
      }
      :host ::ng-deep tr.stop-row-active {
        border-inline-start: 4px solid var(--p-primary-color);
      }
    `,
  ],
})
export class TeamRouteComponent implements OnInit {
  private readonly teamsClient = inject(FsmsTeamsClient);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  protected readonly route = signal<TeamSurveyRouteDto | null>(null);
  protected readonly loading = signal(false);
  protected readonly teamsLoading = signal(false);
  protected readonly teams = signal<TeamOption[]>([]);

  /** Set once a search has run, so the "pick a team" prompt is not shown over an empty result. */
  protected readonly searched = signal(false);

  /** Shared with the map both ways: a clicked pin selects the row, a clicked row focuses the pin. */
  protected readonly activeStopId = signal<number | null>(null);

  protected readonly statusSeverity = surveyStatusSeverity;
  protected readonly emptyValue = EMPTY_VALUE;

  protected teamId: number | null = null;
  protected date: Date = new Date();

  protected readonly stops = computed<TeamSurveyRouteStopDto[]>(() => this.route()?.stops ?? []);

  /** The subset the map can actually draw — a stop with no coordinate still lists, but cannot pin. */
  protected readonly mappedStops = computed<RouteMapStop[]>(() =>
    this.stops()
      .filter((stop) => stop.latitude != null && stop.longitude != null)
      .map((stop) => ({
        id: stop.surveyId ?? 0,
        sequence: stop.sequence ?? 0,
        lat: stop.latitude as number,
        lng: stop.longitude as number,
        title: stop.surveyCode ?? '',
        subtitle: this.localizedName(stop.templateNameEn, stop.templateNameAr) || stop.faId || null,
        time: this.formatTime(stop.submittedDate),
      })),
  );

  /** Percentage of stops that have valid mapped coordinates. */
  protected readonly mappedPercentage = computed<number>(() => {
    const route = this.route();
    if (!route || !route.totalStopCount || route.totalStopCount === 0) {
      return 0;
    }
    return Math.round(((route.mappedStopCount ?? 0) / route.totalStopCount) * 100);
  });

  /** True when at least one stop of the day could not be drawn, so the route shown is partial. */
  protected readonly hasUnmappedStops = computed(() => {
    const route = this.route();
    return !!route && (route.mappedStopCount ?? 0) < (route.totalStopCount ?? 0);
  });

  ngOnInit(): void {
    this.loadTeams();
  }

  protected setToday(): void {
    this.date = new Date();
    if (this.teamId != null) {
      this.loadRoute();
    }
  }

  protected setYesterday(): void {
    const d = new Date();
    d.setDate(d.getDate() - 1);
    this.date = d;
    if (this.teamId != null) {
      this.loadRoute();
    }
  }

  protected isToday(): boolean {
    if (!this.date) return false;
    const today = new Date();
    return (
      this.date.getFullYear() === today.getFullYear() &&
      this.date.getMonth() === today.getMonth() &&
      this.date.getDate() === today.getDate()
    );
  }

  protected isYesterday(): boolean {
    if (!this.date) return false;
    const yesterday = new Date();
    yesterday.setDate(yesterday.getDate() - 1);
    return (
      this.date.getFullYear() === yesterday.getFullYear() &&
      this.date.getMonth() === yesterday.getMonth() &&
      this.date.getDate() === yesterday.getDate()
    );
  }

  protected loadTeams(): void {
    this.teamsLoading.set(true);
    this.teamsClient
      .fsmsTeams_GetTeamsPaged(1, TEAM_PAGE_SIZE, undefined, true)
      .pipe(finalize(() => this.teamsLoading.set(false)))
      .subscribe({
        next: (res) => {
          this.teams.set(
            (res.data?.items ?? [])
              .filter((team) => team.teamId != null)
              .map((team) => ({
                label: team.userCode ? `${team.name ?? ''} (${team.userCode})`.trim() : (team.name ?? ''),
                value: team.teamId as number,
              })),
          );
        },
        error: (error: unknown) => {
          this.teams.set([]);
          this.toastError(error, 'tracking.loadError');
        },
      });
  }

  protected loadRoute(): void {
    if (this.teamId == null || !this.date || this.loading()) {
      return;
    }

    this.loading.set(true);
    this.searched.set(true);
    this.activeStopId.set(null);

    // The viewer's own offset, so "11 August" means their civil day rather than a UTC one.
    const utcOffsetMinutes = -new Date().getTimezoneOffset();

    this.teamsClient
      .fsmsTeams_GetSurveyRoute(this.teamId, toDateOnly(this.date), utcOffsetMinutes)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => this.route.set(res.data ?? null),
        error: (error: unknown) => {
          this.route.set(null);
          this.toastError(error, 'tracking.loadError');
        },
      });
  }

  protected selectStop(stop: TeamSurveyRouteStopDto): void {
    if (stop.latitude == null || stop.longitude == null) {
      return;
    }
    this.activeStopId.set(stop.surveyId ?? null);
  }

  protected isActive(stop: TeamSurveyRouteStopDto): boolean {
    return this.activeStopId() !== null && this.activeStopId() === stop.surveyId;
  }

  protected templateName(stop: TeamSurveyRouteStopDto): string {
    return this.localizedName(stop.templateNameEn, stop.templateNameAr) || EMPTY_VALUE;
  }

  protected formatTime(value?: Date | null): string {
    if (!value) {
      return EMPTY_VALUE;
    }
    return new Intl.DateTimeFormat(this.language.current(), {
      hour: '2-digit',
      minute: '2-digit',
    }).format(new Date(value));
  }

  protected formatGap(minutes?: number | null): string {
    if (minutes == null) {
      return EMPTY_VALUE;
    }
    if (minutes < MINUTES_PER_HOUR) {
      return this.transloco.translate('tracking.minutesShort', { value: minutes });
    }

    const hours = Math.floor(minutes / MINUTES_PER_HOUR);
    const remainder = minutes % MINUTES_PER_HOUR;
    return remainder === 0 ? `${hours}h` : `${hours}h ${remainder}m`;
  }

  protected formatDistance(meters?: number | null): string {
    if (meters == null) {
      return EMPTY_VALUE;
    }
    return meters >= METRES_PER_KILOMETRE
      ? this.transloco.translate('tracking.kilometres', {
          value: (meters / METRES_PER_KILOMETRE).toFixed(1),
        })
      : this.transloco.translate('tracking.metres', { value: Math.round(meters) });
  }

  private localizedName(nameEn?: string, nameAr?: string): string {
    return (this.language.current() === 'ar' ? nameAr || nameEn : nameEn || nameAr) ?? '';
  }

  private toastError(error: unknown, fallbackKey: string): void {
    this.messageService.add({
      severity: 'error',
      summary: this.transloco.translate('common.error'),
      detail: apiErrorMessage(error, this.transloco.translate(fallbackKey)),
    });
  }
}
