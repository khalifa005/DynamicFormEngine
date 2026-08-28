import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import {
  BehaviorSubject,
  catchError,
  finalize,
  forkJoin,
  of,
  switchMap,
  timer,
} from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';

import {
  FsmsSurveysClient,
  FsmsTeamsClient,
  SurveyListItemDto,
  TeamDto,
} from '../../../core/api/api-client.generated';
import { apiErrorMessage } from '../../../core/api/api-error';
import { LanguageService } from '../../../core/i18n/language.service';
import { SurveyStatus, surveyStatusSeverity } from '../../surveys/survey-status';
import { MockTeamLocationService } from '../data/mock-team-location.service';
import { TeamLocation, TeamLocationService } from '../data/team-location.service';
import { MonitoringMapComponent } from './monitoring-map.component';
import type { MonitoringSurveyMarker, MonitoringTeamMarker } from './monitoring-map.types';

const OPEN_STATUSES: readonly string[] = [
  SurveyStatus.Assigned,
  SurveyStatus.InProgress,
  SurveyStatus.Returned,
];

const SURVEY_PAGE_SIZE = 1000;
const TEAM_PAGE_SIZE = 500;
const POLL_MS = 60_000;

@Component({
  selector: 'app-live-monitoring',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    TranslocoDirective,
    ButtonModule,
    IconFieldModule,
    InputIconModule,
    InputTextModule,
    SkeletonModule,
    TagModule,
    ToastModule,
    TooltipModule,
    MonitoringMapComponent,
  ],
  providers: [
    MessageService,
    { provide: TeamLocationService, useClass: MockTeamLocationService },
  ],
  templateUrl: './live-monitoring.component.html',
  styleUrl: './live-monitoring.component.scss',
})
export class LiveMonitoringComponent implements OnInit {
  private readonly surveysClient = inject(FsmsSurveysClient);
  private readonly teamsClient = inject(FsmsTeamsClient);
  private readonly teamLocations = inject(TeamLocationService);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);
  private readonly destroyRef = inject(DestroyRef);

  private readonly teamIds$ = new BehaviorSubject<readonly number[]>([]);

  protected readonly loading = signal(false);
  protected readonly surveys = signal<SurveyListItemDto[]>([]);
  protected readonly teams = signal<TeamDto[]>([]);
  protected readonly locations = signal<readonly TeamLocation[]>([]);
  protected readonly surveySearch = signal('');
  protected readonly teamSearch = signal('');
  protected readonly selectedSurveyId = signal<number | null>(null);
  protected readonly selectedTeamId = signal<number | null>(null);
  protected readonly statusSeverity = surveyStatusSeverity;

  protected readonly filteredSurveys = computed(() => {
    const selectedTeam = this.selectedTeamId();
    const q = this.surveySearch().trim().toLowerCase();
    let items = this.surveys();

    if (selectedTeam !== null) {
      items = items.filter((s) => s.allocatedFieldTeamId === selectedTeam);
    }

    if (q) {
      items = items.filter((s) => {
        const hay = [s.surveyCode, s.faId, s.taskCode, s.allocatedFieldTeamName]
          .filter(Boolean)
          .join(' ')
          .toLowerCase();
        return hay.includes(q);
      });
    }

    return items;
  });

  protected readonly mappedSurveys = computed(() =>
    this.filteredSurveys().filter(
      (s) => s.latitude != null && s.longitude != null && s.surveyId != null,
    ),
  );

  protected readonly unmappedSurveys = computed(() =>
    this.filteredSurveys().filter((s) => s.latitude == null || s.longitude == null),
  );

  protected readonly filteredTeams = computed(() => {
    const q = this.teamSearch().trim().toLowerCase();
    const teams = this.teams();
    if (!q) {
      return teams;
    }
    return teams.filter((t) => {
      const hay = [t.name, t.userCode, t.mobile].filter(Boolean).join(' ').toLowerCase();
      return hay.includes(q);
    });
  });

  protected readonly surveyMarkers = computed((): MonitoringSurveyMarker[] => {
    const selectedTeam = this.selectedTeamId();
    const allWithCoords = this.surveys().filter(
      (s) => s.latitude != null && s.longitude != null && s.surveyId != null,
    );

    return allWithCoords.map((s) => ({
      surveyId: s.surveyId!,
      surveyCode: s.surveyCode ?? String(s.surveyId),
      status: s.status ?? '',
      latitude: s.latitude!,
      longitude: s.longitude!,
      faId: s.faId,
      allocatedFieldTeamId: s.allocatedFieldTeamId,
      allocatedFieldTeamName: s.allocatedFieldTeamName,
      dimmed: selectedTeam !== null && s.allocatedFieldTeamId !== selectedTeam,
    }));
  });

  protected readonly teamMarkers = computed((): MonitoringTeamMarker[] => {
    const selectedTeam = this.selectedTeamId();
    const teamById = new Map(this.teams().map((t) => [t.teamId!, t]));

    return this.locations()
      .filter((loc) => teamById.has(loc.teamId))
      .map((loc) => {
        const team = teamById.get(loc.teamId)!;
        return {
          teamId: loc.teamId,
          name: team.name ?? String(loc.teamId),
          latitude: loc.latitude,
          longitude: loc.longitude,
          isOnline: loc.isOnline,
          emphasized: selectedTeam === loc.teamId,
        };
      });
  });

  protected readonly openCountByTeam = computed(() => {
    const counts = new Map<number, number>();
    for (const survey of this.surveys()) {
      const teamId = survey.allocatedFieldTeamId;
      if (teamId == null) {
        continue;
      }
      counts.set(teamId, (counts.get(teamId) ?? 0) + 1);
    }
    return counts;
  });

  ngOnInit(): void {
    timer(0, POLL_MS)
      .pipe(
        switchMap(() => this.loadData$()),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();

    this.teamIds$
      .pipe(
        switchMap((ids) =>
          ids.length === 0 ? of([] as readonly TeamLocation[]) : this.teamLocations.watchLocations(ids),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((locs) => this.locations.set(locs));
  }

  protected refresh(): void {
    this.loadData$().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  protected selectSurvey(surveyId: number): void {
    this.selectedSurveyId.set(surveyId);
  }

  protected selectTeam(teamId: number): void {
    if (this.selectedTeamId() === teamId) {
      this.selectedTeamId.set(null);
      return;
    }
    this.selectedTeamId.set(teamId);
  }

  protected clearTeamFilter(): void {
    this.selectedTeamId.set(null);
  }

  protected templateName(survey: SurveyListItemDto): string {
    return this.language.current() === 'ar'
      ? (survey.templateNameAr || survey.templateNameEn || '')
      : (survey.templateNameEn || survey.templateNameAr || '');
  }

  protected teamOpenCount(teamId: number | undefined): number {
    if (teamId == null) {
      return 0;
    }
    return this.openCountByTeam().get(teamId) ?? 0;
  }

  protected isTeamOnline(teamId: number | undefined): boolean {
    if (teamId == null) {
      return false;
    }
    return this.locations().some((l) => l.teamId === teamId && l.isOnline);
  }

  protected onSurveySearch(value: string): void {
    this.surveySearch.set(value);
  }

  protected onTeamSearch(value: string): void {
    this.teamSearch.set(value);
  }

  private loadData$() {
    this.loading.set(true);

    return forkJoin({
      surveys: this.surveysClient.fsmsSurveys_GetSurveys(
        1,
        SURVEY_PAGE_SIZE,
        undefined,
        [...OPEN_STATUSES],
      ),
      teams: this.teamsClient.fsmsTeams_GetTeamsPaged(1, TEAM_PAGE_SIZE, undefined, true),
    }).pipe(
      finalize(() => this.loading.set(false)),
      catchError((err) => {
        this.messageService.add({
          severity: 'error',
          summary: this.transloco.translate('monitoring.errorTitle'),
          detail: apiErrorMessage(err, this.transloco.translate('monitoring.loadError')),
        });
        return of(null);
      }),
      switchMap((result) => {
        if (!result) {
          return of(null);
        }

        const surveys = result.surveys?.data?.items ?? [];
        const teams = (result.teams?.data?.items ?? []).filter((t) => t.teamId != null);

        this.surveys.set(surveys);
        this.teams.set(teams);

        const selectedSurvey = this.selectedSurveyId();
        if (selectedSurvey !== null && !surveys.some((s) => s.surveyId === selectedSurvey)) {
          this.selectedSurveyId.set(null);
        }

        const selectedTeam = this.selectedTeamId();
        if (selectedTeam !== null && !teams.some((t) => t.teamId === selectedTeam)) {
          this.selectedTeamId.set(null);
        }

        this.teamLocations.setSeedHints(this.buildSeedHints(surveys));

        const nextIds = teams.map((t) => t.teamId!);
        const prev = this.teamIds$.value;
        const same =
          prev.length === nextIds.length && prev.every((id, i) => id === nextIds[i]);
        if (!same) {
          this.teamIds$.next(nextIds);
        }

        return of(null);
      }),
    );
  }

  private buildSeedHints(
    surveys: SurveyListItemDto[],
  ): Map<number, { lat: number; lng: number }> {
    const buckets = new Map<number, { sumLat: number; sumLng: number; count: number }>();

    for (const survey of surveys) {
      const teamId = survey.allocatedFieldTeamId;
      if (teamId == null || survey.latitude == null || survey.longitude == null) {
        continue;
      }
      const bucket = buckets.get(teamId) ?? { sumLat: 0, sumLng: 0, count: 0 };
      bucket.sumLat += survey.latitude;
      bucket.sumLng += survey.longitude;
      bucket.count += 1;
      buckets.set(teamId, bucket);
    }

    const hints = new Map<number, { lat: number; lng: number }>();
    for (const [teamId, bucket] of buckets) {
      hints.set(teamId, {
        lat: bucket.sumLat / bucket.count,
        lng: bucket.sumLng / bucket.count,
      });
    }
    return hints;
  }
}
