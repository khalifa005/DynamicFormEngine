import { HttpClient, HttpEvent, HttpEventType, HttpRequest } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, filter, map } from 'rxjs';
import { API_BASE_URL, type StartedMigrationRunDtoResult } from './api-client.generated';

const RUNS_PATH = '/api/v1/fsms/data-migration/runs';

/** What the operator chose on the import page, before it becomes a multipart body. */
export interface StartMigrationRunRequest {
  readonly file: File;
  readonly sourceCode: string;
  readonly templateId: number;
  readonly mode: string;
  readonly cbuCode?: string | null;
  readonly branchCode?: string | null;
  readonly operationAreaCode?: string | null;
  readonly departmentId?: number | null;
  readonly fieldTeamId?: number | null;
}

/** Upload lifecycle: percent while the workbook moves, then the queued run. */
export type StartMigrationRunEvent =
  | { readonly kind: 'progress'; readonly percent: number }
  | { readonly kind: 'done'; readonly runId: number };

/**
 * Starts an import run.
 *
 * Deliberately not the generated `fsmsDataMigration_StartRun`, for the same reason
 * {@link SubmissionFileUploadService} bypasses the generated upload: NSwag emits
 * `if (x === null || x === undefined) throw` for **every** multipart field, optional ones included.
 * A run placed by branch alone — with no CBU and no operation area — therefore cannot be expressed
 * through the generated method at all; it throws before a request is made.
 *
 * Appending only the fields that have a value is what lets the server see them as absent, which is
 * what its own `string?` / `long?` parameters were written to accept. Reporting progress is a second
 * gain: the workbook may be tens of megabytes, and the generated method emits no progress events.
 *
 * The generated response *type* is still used, and auth and base URL flow through the app's normal
 * interceptors and `API_BASE_URL`. Every other call on this feature stays on the generated client.
 */
@Injectable({ providedIn: 'root' })
export class DataMigrationUploadService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  start(request: StartMigrationRunRequest): Observable<StartMigrationRunEvent> {
    const body = new FormData();

    body.append('file', request.file, request.file.name);
    body.append('sourceCode', request.sourceCode);
    body.append('templateId', String(request.templateId));
    body.append('mode', request.mode);

    // Only what was actually chosen. An omitted field is what the server reads as null; sending an
    // empty string would instead place a survey in a CBU called "".
    appendIfPresent(body, 'cbuCode', request.cbuCode);
    appendIfPresent(body, 'branchCode', request.branchCode);
    appendIfPresent(body, 'operationAreaCode', request.operationAreaCode);
    appendIfPresent(body, 'departmentId', request.departmentId);
    appendIfPresent(body, 'fieldTeamId', request.fieldTeamId);

    const httpRequest = new HttpRequest('POST', `${this.baseUrl}${RUNS_PATH}`, body, {
      reportProgress: true,
    });

    return this.http.request<StartedMigrationRunDtoResult>(httpRequest).pipe(
      map((event) => this.toEvent(event)),
      filter((event): event is StartMigrationRunEvent => event !== null),
    );
  }

  private toEvent(event: HttpEvent<StartedMigrationRunDtoResult>): StartMigrationRunEvent | null {
    if (event.type === HttpEventType.UploadProgress) {
      return { kind: 'progress', percent: event.total ? Math.round((event.loaded / event.total) * 100) : 0 };
    }

    if (event.type === HttpEventType.Response) {
      return { kind: 'done', runId: this.toRunId(event.body) };
    }

    return null;
  }

  /** Collapses the all-optional generated result into the run id, or throws with the server's reason. */
  private toRunId(result: StartedMigrationRunDtoResult | null): number {
    const runId = result?.data?.runId;

    if (!result?.isSuccess || runId === undefined) {
      throw new Error(result?.errors?.[0]?.message ?? 'The import could not be started.');
    }

    return runId;
  }
}

function appendIfPresent(body: FormData, name: string, value: string | number | null | undefined): void {
  if (value !== null && value !== undefined && value !== '') {
    body.append(name, String(value));
  }
}
