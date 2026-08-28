import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  model,
  output,
  signal,
} from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';

import { OrgScopeSelectorComponent } from '../../../../shared/components/org-scope/org-scope-selector.component';
import {
  EMPTY_ORG_LOCATION,
  OrgLocation,
  isEmptyLocation,
} from '../../../../shared/components/org-scope/org-scope.model';

/**
 * The worklist's organization filter, as a popup.
 *
 * It used to sit open inside the table caption, where the four-control cascade
 * took a full row above the grid whether or not anyone was filtering by place.
 *
 * The selection is **staged**: the cascade picker reports every level as it is
 * chosen, and the inline version reloaded the list on each one — narrowing to an
 * operation area cost four round trips. Here those go into a draft and only
 * leave on Apply, so narrowing costs one.
 */
@Component({
  selector: 'app-survey-org-filter-dialog',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoDirective, ButtonModule, DialogModule, OrgScopeSelectorComponent],
  templateUrl: './survey-org-filter-dialog.component.html',
})
export class SurveyOrgFilterDialogComponent {
  readonly visible = model(false);

  /** The filter currently in force, so reopening shows what is applied. */
  readonly location = input<OrgLocation>(EMPTY_ORG_LOCATION);

  readonly applied = output<OrgLocation>();

  /**
   * What the picker reports as the operator moves through the cascade. Seeded
   * from `location` on open — the dialog builds its content only while visible,
   * so the selector is a fresh instance each time and re-reads `initialLocation`.
   */
  protected readonly draft = signal<OrgLocation>(EMPTY_ORG_LOCATION);

  /**
   * A new object identity on every open is what tells the picker to rebuild its
   * cascade; reusing the same reference would leave the previous one standing.
   */
  protected readonly seed = signal<OrgLocation>(EMPTY_ORG_LOCATION);

  protected readonly canApply = computed(() => !isEmptyLocation(this.draft()));

  protected onShow(): void {
    const current = this.location();
    this.draft.set({ ...current });
    this.seed.set({ ...current });
  }

  protected onDraftChange(location: OrgLocation): void {
    this.draft.set(location);
  }

  /** Clears the cascade without closing — the operator usually re-picks straight away. */
  protected reset(): void {
    this.draft.set({ ...EMPTY_ORG_LOCATION });
    this.seed.set({ ...EMPTY_ORG_LOCATION });
  }

  protected apply(): void {
    this.applied.emit(this.draft());
    this.visible.set(false);
  }

  protected cancel(): void {
    this.visible.set(false);
  }
}
