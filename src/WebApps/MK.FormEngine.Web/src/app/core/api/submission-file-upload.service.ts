import { HttpClient, HttpEvent, HttpEventType, HttpRequest } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, filter, map } from 'rxjs';
import {
  API_BASE_URL,
  FsmsUploadsClient,
  type BooleanResult,
  type UploadedFileDtoResult,
} from './api-client.generated';

const UPLOADS_PATH = '/api/v1/fsms/uploads';

/**
 * A file already stored on the server; this is what a media field value holds.
 *
 * Narrower than the generated `UploadedFileDto`, whose properties are all optional because
 * NSwag cannot see that the handler always populates them. Keeping the required shape here
 * spares every consumer a null check for values that cannot be null.
 */
export interface UploadedFileRef {
  readonly fileId: string;
  readonly path: string;
  readonly name: string;
  readonly type: string;
  readonly size: number;
}

/** Upload lifecycle: percent while bytes move, then the stored reference. */
export type UploadEvent =
  | { readonly kind: 'progress'; readonly percent: number }
  | { readonly kind: 'done'; readonly file: UploadedFileRef };

/**
 * Real-time media uploads for survey forms — a file goes to the server as soon as it is picked,
 * so the submit payload only carries references.
 *
 * Delete goes through the generated `FsmsUploadsClient`. Upload and download do not, and for
 * concrete reasons rather than preference:
 *
 * - `uploadsPOST` is generated without `reportProgress`, so it emits no progress events — and
 *   progress is the entire point of uploading on pick.
 * - `uploadsGET` is generated as `Observable<void>`: it reads the response blob and discards it,
 *   so it cannot return the file.
 *
 * Both still use the generated response *types*, and auth/base URL flow through the app's
 * normal interceptors and `API_BASE_URL`.
 */
@Injectable({ providedIn: 'root' })
export class SubmissionFileUploadService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly client = inject(FsmsUploadsClient);

  upload(templateId: number, dataName: string, file: File, surveyId?: number): Observable<UploadEvent> {
    const body = new FormData();
    body.append('file', file, file.name);
    body.append('templateId', String(templateId));
    body.append('dataName', dataName);
    if (surveyId != null) {
      body.append('surveyId', String(surveyId));
    }

    const request = new HttpRequest('POST', `${this.baseUrl}${UPLOADS_PATH}`, body, {
      reportProgress: true,
    });

    return this.http.request<UploadedFileDtoResult>(request).pipe(
      map((event) => this.toUploadEvent(event)),
      filter((event): event is UploadEvent => event !== null),
    );
  }

  /** Removes a file the user picked and then dropped before submitting. */
  delete(fileId: string): Observable<BooleanResult> {
    return this.client.fsmsUploads_Delete(fileId);
  }

  /**
   * Fetches a stored file as a blob object URL, ready for `<img>` / `<video>` / `<audio>`.
   *
   * Pointing an element straight at the endpoint URL would not work: a browser-issued asset
   * request never passes through `authInterceptor`, so it arrives without the bearer token and
   * is rejected. Going through `HttpClient` keeps the token attached.
   *
   * The caller owns the returned URL and must `URL.revokeObjectURL` it.
   */
  downloadObjectUrl(fileId: string): Observable<string> {
    return this.http
      .get(`${this.baseUrl}${UPLOADS_PATH}/${fileId}`, { responseType: 'blob' })
      .pipe(map((blob) => URL.createObjectURL(blob)));
  }

  private toUploadEvent(event: HttpEvent<UploadedFileDtoResult>): UploadEvent | null {
    if (event.type === HttpEventType.UploadProgress) {
      const percent = event.total ? Math.round((event.loaded / event.total) * 100) : 0;
      return { kind: 'progress', percent };
    }

    if (event.type === HttpEventType.Response) {
      return { kind: 'done', file: this.toFileRef(event.body) };
    }

    return null;
  }

  /** Collapses the all-optional generated DTO into the required shape, or throws. */
  private toFileRef(result: UploadedFileDtoResult | null): UploadedFileRef {
    const data = result?.data;
    if (!result?.isSuccess || !data?.fileId) {
      throw new Error(result?.errors?.[0]?.message ?? 'The file could not be uploaded.');
    }

    return {
      fileId: data.fileId,
      path: data.path ?? '',
      name: data.name ?? '',
      type: data.type ?? '',
      size: data.size ?? 0,
    };
  }
}
