import { Component, inject, input, signal } from '@angular/core';
import { TranslocoModule, TranslocoService } from '@jsverse/transloco';
import { Observable } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';

export type ExportFormat = 'excel' | 'pdf';

/**
 * Export UI for a report. The download itself is not this component's concern — each page hands in
 * `exportFn`, a function that performs the actual excel/pdf download (see `ReportExportService`) and
 * returns an `Observable` that completes on success or errors on failure. This component only owns
 * the busy/disabled state on the two buttons and the error toast, so the same shell works for all
 * three report pages without duplicating that UI three times.
 */
@Component({
  selector: 'app-report-export-actions',
  providers: [MessageService],
  imports: [TranslocoModule, ButtonModule, ToastModule],
  templateUrl: './report-export-actions.component.html',
  styleUrl: './report-export-actions.component.scss',
})
export class ReportExportActionsComponent {
  readonly reportNameKey = input.required<string>();
  readonly rowCount = input(0);
  readonly activeFilterCount = input(0);
  readonly disabled = input(false);
  /** Performs the export for the given format; resolves the endpoint/filters the page already has. */
  readonly exportFn = input.required<(format: ExportFormat) => Observable<unknown>>();

  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);

  protected readonly exporting = signal<ExportFormat | null>(null);

  protected export(format: ExportFormat): void {
    if (this.exporting()) {
      return;
    }

    this.exporting.set(format);
    this.exportFn()(format).subscribe({
      next: () => this.exporting.set(null),
      error: () => {
        this.exporting.set(null);
        this.messageService.add({
          severity: 'error',
          summary: this.transloco.translate('common.error'),
          detail: this.transloco.translate('reports.export.failed', {
            report: this.transloco.translate(this.reportNameKey()),
          }),
        });
      },
    });
  }
}
