/** A survey asset pin on the live monitoring map. */
export interface MonitoringSurveyMarker {
  readonly surveyId: number;
  readonly surveyCode: string;
  readonly status: string;
  readonly latitude: number;
  readonly longitude: number;
  readonly faId?: string | null;
  readonly allocatedFieldTeamId?: number | null;
  readonly allocatedFieldTeamName?: string | null;
  readonly dimmed?: boolean;
}

/** A (mock or live) field-team position pin. */
export interface MonitoringTeamMarker {
  readonly teamId: number;
  readonly name: string;
  readonly latitude: number;
  readonly longitude: number;
  readonly isOnline: boolean;
  readonly emphasized?: boolean;
}
