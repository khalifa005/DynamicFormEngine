/** One consistent filter shape shared by every report page and by `ReportFilterBarComponent`. */
export interface ReportFilterValue {
  fromDate: Date | null;
  toDate: Date | null;
  status: string | null;
  /** Only shown where `showTeamFilter` is on — General Statistics has no team dimension. */
  teamId: number | null;
  branchCode: string | null;
  operationAreaCode: string | null;
  /** Only the Survey Tasks report shows this (`showSourceFilter`) — others leave it null. */
  source: string | null;
}

export const EMPTY_REPORT_FILTER: ReportFilterValue = {
  fromDate: null,
  toDate: null,
  status: null,
  teamId: null,
  branchCode: null,
  operationAreaCode: null,
  source: null,
};

export interface ReportSelectOption<T = string> {
  readonly label: string;
  readonly value: T;
}
