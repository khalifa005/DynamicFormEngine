import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';

import { API_BASE_URL } from '../../../core/api/api-client.generated';

export type ReportExportFormat = 'excel' | 'pdf';

/**
 * Downloads a report export file straight from the API. NSwag generated the `fsmsReports_Export*`
 * client methods as `Observable<void>` — the OpenAPI metadata on these file-returning actions doesn't
 * tell NSwag they return a blob, so the generated method silently discards the response body. This
 * service bypasses the generated client entirely, mirroring what `SurveyListComponent.exportToPdf`
 * already does for the single-survey PDF export: a raw `HttpClient` GET for a blob, the file name
 * read out of `Content-Disposition`, then a synthetic anchor click to save it.
 */
@Injectable({ providedIn: 'root' })
export class ReportExportService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  /**
   * @param path API path starting with `/api/v1/...` (no query string).
   * @param params Query parameters in the API's PascalCase — omitted when `undefined`/empty.
   * @param fallbackFileName Used only if the response carries no `Content-Disposition` file name.
   */
  download(path: string, params: Record<string, string | undefined>, fallbackFileName: string): Observable<void> {
    const query = this.buildQuery(params);

    return this.http
      .get(`${this.baseUrl}${path}${query}`, { responseType: 'blob', observe: 'response' })
      .pipe(
        map((response) => {
          const contentDisposition = response.headers.get('content-disposition') || '';
          // Stop at the `;` — the header also carries a `filename*=UTF-8''…` copy, and swallowing it
          // makes the saved file name carry the whole header fragment.
          const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(contentDisposition);
          const fileName = match ? decodeURIComponent(match[1].trim()) : fallbackFileName;

          const blob = new Blob([response.body as Blob]);
          const url = window.URL.createObjectURL(blob);
          const a = document.createElement('a');
          a.href = url;
          a.download = fileName;
          document.body.appendChild(a);
          a.click();
          document.body.removeChild(a);
          window.URL.revokeObjectURL(url);
        }),
      );
  }

  private buildQuery(params: Record<string, string | undefined>): string {
    const usp = new URLSearchParams();
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined && value !== null && value !== '') {
        usp.set(key, value);
      }
    }
    const qs = usp.toString();
    return qs ? `?${qs}` : '';
  }
}
