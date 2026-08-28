import { ChangeDetectionStrategy, Component, input, model } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { DialogModule } from 'primeng/dialog';
import { GeoMapComponent } from '../../../../shared/components/geo-map/geo-map.component';
import type { GeoPoint } from '../../../../shared/components/geo-map/google-maps.types';

/**
 * Shows one geolocation answer on a read-only map, with the address it was filed under underneath
 * (the map component renders it). The result summary lists the same as text — this is the "where is
 * that, actually" view behind it, so the reviewer never has to paste a pair of numbers into another
 * tab.
 *
 * The map is only built while the dialog is open (`@if` on the point), so opening a survey with
 * several geolocation answers does not load a map per row.
 */
@Component({
  selector: 'app-answer-map-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, DialogModule, GeoMapComponent],
  template: `
    <ng-container *transloco="let t">
      <p-dialog
        [(visible)]="visible"
        [modal]="true"
        [draggable]="false"
        [dismissableMask]="true"
        [style]="{ width: '46rem', maxWidth: '95vw' }"
        [header]="label() || t('surveys.detail.viewOnMap')"
      >
        @if (visible() && point(); as picked) {
          <app-geo-map [value]="picked" [readonly]="true" height="380px" />
        }
      </p-dialog>
    </ng-container>
  `,
})
export class AnswerMapDialogComponent {
  readonly visible = model.required<boolean>();
  readonly point = input<GeoPoint | null>(null);

  /** The question's own label, so the map is titled the way the answer row reads. */
  readonly label = input('');
}
