import { Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { finalize, firstValueFrom } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { ChipModule } from 'primeng/chip';
import { SelectModule } from 'primeng/select';

import { FsmsLookupService } from '../../../core/lookups/fsms-lookup.service';
import {
  EMPTY_ORG_LOCATION,
  LEVEL_LABEL_KEYS,
  isLevelUnder,
  ORG_SCOPE_LEVELS,
  OrgLocation,
  OrgScopeAssignment,
  OrgScopeLevel,
} from './org-scope.model';

interface SelectOption {
  readonly label: string;
  readonly value: string;
}

interface DepartmentOption {
  readonly label: string;
  readonly value: number;
}

/**
 * The cascading Cluster → CBU → (Branch | Operation Area) picker, in one place because five screens
 * need it — the team form, the user form, the template form and both survey dialogs.
 *
 * Two modes:
 *
 * - **single** — one place, for a survey. Emits an {@link OrgLocation}.
 * - **multi** — a territory made of several entries, for a team, user or template. The operator
 *   narrows as far as they want and adds that selection; adding at `Cluster` covers everything
 *   beneath it, which is why the common case is one row rather than a list of leaves. Emits
 *   `OrgScopeAssignment[]`.
 *
 * Values arrive through `initialLocation` / `initialScopes` and leave through outputs rather than
 * two-way bindings: writing back into a model the parent also writes is how these pickers end up
 * looping, and a form only ever seeds this once per open.
 */
@Component({
  selector: 'app-org-scope-selector',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslocoDirective, ButtonModule, ChipModule, SelectModule],
  templateUrl: './org-scope-selector.component.html',
})
export class OrgScopeSelectorComponent {
  /** `single` for a survey's location, `multi` for a covered territory. */
  readonly mode = input<'single' | 'multi'>('single');

  /** `grid` for a 2-column layout, `row` for a 4-column row layout. */
  readonly layout = input<'grid' | 'row'>('grid');

  readonly disabled = input(false);

  /** Hides the operation-area control where that level is more precision than the screen wants. */
  readonly showOperationArea = input(true);

  readonly initialLocation = input<OrgLocation | null>(null);
  readonly initialScopes = input<readonly OrgScopeAssignment[]>([]);

  readonly locationChange = output<OrgLocation>();
  readonly scopesChange = output<OrgScopeAssignment[]>();

  private readonly lookups = inject(FsmsLookupService);
  private readonly transloco = inject(TranslocoService);

  protected readonly clusterCode = signal<string | null>(null);
  protected readonly cbuCode = signal<string | null>(null);
  protected readonly branchCode = signal<string | null>(null);
  protected readonly operationAreaCode = signal<string | null>(null);

  /**
   * The kind of work this row is limited to; blank means every department. Only offered in multi
   * mode — a survey carries its own department field, and asking for it twice on one dialog would
   * be two answers to the same question.
   */
  protected readonly departmentId = signal<number | null>(null);

  protected readonly clusters = signal<SelectOption[]>([]);
  protected readonly cbus = signal<SelectOption[]>([]);
  protected readonly branches = signal<SelectOption[]>([]);
  protected readonly operationAreas = signal<SelectOption[]>([]);
  protected readonly departments = signal<DepartmentOption[]>([]);

  protected readonly loadingClusters = signal(false);
  protected readonly loadingCbus = signal(false);
  protected readonly loadingBranches = signal(false);
  protected readonly loadingOperationAreas = signal(false);
  protected readonly loadingDepartments = signal(false);

  protected readonly scopes = signal<OrgScopeAssignment[]>([]);

  /** Names for the chips in multi mode, keyed `Level|CODE`. */
  private readonly scopeLabels = signal<Map<string, string>>(new Map());

  /** Department id → name, for chips naming a department. */
  private readonly departmentLabels = signal<Map<number, string>>(new Map());

  /**
   * The narrowest level the operator has picked, or null when nothing is selected. This is what
   * "Add" adds — picking a cluster and then a branch means they meant the branch.
   */
  protected readonly selectedLevel = computed<OrgScopeLevel | null>(() => {
    if (this.operationAreaCode()) return ORG_SCOPE_LEVELS.operationArea;
    if (this.branchCode()) return ORG_SCOPE_LEVELS.branch;
    if (this.cbuCode()) return ORG_SCOPE_LEVELS.cbu;
    if (this.clusterCode()) return ORG_SCOPE_LEVELS.cluster;
    return null;
  });

  protected readonly selectedCode = computed<string | null>(
    () => this.operationAreaCode() ?? this.branchCode() ?? this.cbuCode() ?? this.clusterCode(),
  );

  /**
   * A row needs a territory, a department, or both — a department with no place is a legitimate
   * scope ("water network, everywhere"), which is why this is an OR rather than requiring a level.
   */
  protected readonly canAdd = computed(() => {
    const level = this.selectedLevel();
    const code = this.selectedCode();
    const department = this.departmentId();

    if (this.disabled()) {
      return false;
    }

    if ((!level || !code) && department === null) {
      return false;
    }

    return !this.scopes().some(scope =>
      (scope.level ?? null) === (level ?? null)
      && (scope.code ?? null) === (code ?? null)
      && (scope.departmentId ?? null) === department);
  });

  constructor() {
    this.loadClusters();
    this.loadDepartments();

    // Both effects seed internal state from an input; everything they touch is written, not derived.
    // The work goes inside `untracked` because the lookup observables replay their cached value
    // synchronously on subscribe — so without it, a load that reads one of these signals would
    // register it as a dependency of the effect that is about to write it, and the effect would
    // retrigger itself forever.
    effect(() => {
      const location = this.initialLocation();

      if (location) {
        untracked(() => void this.seedFromLocation(location));
      }
    });

    effect(() => {
      const initial = this.initialScopes();

      untracked(() => {
        this.scopes.set(
          initial.map(scope => ({
            level: scope.level,
            code: scope.code,
            departmentId: scope.departmentId,
          })),
        );

        if (initial.length > 0) {
          this.loadScopeLabels();
        }
      });
    });
  }

  protected onClusterChange(code: string | null): void {
    this.clusterCode.set(code);
    this.cbuCode.set(null);
    this.branchCode.set(null);
    this.operationAreaCode.set(null);
    this.branches.set([]);
    this.operationAreas.set([]);
    this.loadCbus(code);
    this.emitLocation();
  }

  protected onCbuChange(code: string | null): void {
    this.cbuCode.set(code);
    this.branchCode.set(null);
    this.operationAreaCode.set(null);
    // Both leaves hang off the CBU, so both lists are reloaded from it in parallel.
    this.loadBranches(code);
    this.loadOperationAreas(code);
    this.emitLocation();
  }

  /** Picking a branch narrows nothing further — the operation area list stays as the CBU set it. */
  protected onBranchChange(code: string | null): void {
    this.branchCode.set(code);
    this.emitLocation();
  }

  protected onOperationAreaChange(code: string | null): void {
    this.operationAreaCode.set(code);
    this.emitLocation();
  }

  protected addScope(): void {
    if (!this.canAdd()) {
      return;
    }

    const level = this.selectedLevel();
    const code = this.selectedCode();
    const department = this.departmentId();
    const hasTerritory = !!level && !!code;

    // A narrower entry is already covered by a broader one on the same branch, so keeping both
    // would tell the operator their coverage is two things when it is one. Only within the same
    // department, though: "RCBU, all departments" must not silently swallow a deliberate
    // "branch 1110, water only", which is a genuinely different claim.
    const covered = hasTerritory
      ? this.scopes().filter(scope => !this.isCoveredBy(scope, level!, code!, department))
      : this.scopes();

    this.scopes.set([
      ...covered,
      {
        level: hasTerritory ? level! : undefined,
        code: hasTerritory ? code! : undefined,
        departmentId: department ?? undefined,
      },
    ]);

    if (hasTerritory) {
      this.rememberLabel(level!, code!);
    }

    this.resetSelection();
    this.scopesChange.emit(this.scopes());
  }

  protected removeScope(scope: OrgScopeAssignment): void {
    this.scopes.set(this.scopes().filter(item => !this.isSameScope(item, scope)));
    this.scopesChange.emit(this.scopes());
  }

  /**
   * The chip text, read as the sentence the row is: a place, a kind of work, or both.
   */
  protected scopeLabel(scope: OrgScopeAssignment): string {
    const parts: string[] = [];

    if (scope.level && scope.code) {
      const level = this.transloco.translate(
        LEVEL_LABEL_KEYS[scope.level as OrgScopeLevel] ?? 'org.cluster',
      );
      const name = this.scopeLabels().get(this.labelKey(scope.level, scope.code));
      parts.push(`${level}: ${name ?? scope.code}`);
    } else {
      parts.push(this.transloco.translate('org.everywhere'));
    }

    parts.push(
      scope.departmentId == null
        ? this.transloco.translate('org.allDepartments')
        : (this.departmentLabels().get(scope.departmentId) ?? `#${scope.departmentId}`),
    );

    return parts.join(' · ');
  }

  /** Stable identity for a row, used by both removal and the template's track expression. */
  protected scopeKey(scope: OrgScopeAssignment): string {
    return `${scope.level ?? ''}|${scope.code ?? ''}|${scope.departmentId ?? ''}`;
  }

  /** Clears the cascade — used by a dialog reopening onto a blank form. */
  reset(): void {
    this.resetSelection();
    this.scopes.set([]);
  }

  private isSameScope(left: OrgScopeAssignment, right: OrgScopeAssignment): boolean {
    return (left.level ?? null) === (right.level ?? null)
      && (left.code ?? null) === (right.code ?? null)
      && (left.departmentId ?? null) === (right.departmentId ?? null);
  }

  private resetSelection(): void {
    this.clusterCode.set(null);
    this.cbuCode.set(null);
    this.branchCode.set(null);
    this.operationAreaCode.set(null);
    this.departmentId.set(null);
    this.cbus.set([]);
    this.branches.set([]);
    this.operationAreas.set([]);
  }

  private emitLocation(): void {
    this.locationChange.emit({
      clusterCode: this.clusterCode(),
      cbuCode: this.cbuCode(),
      branchCode: this.branchCode(),
      operationAreaCode: this.operationAreaCode(),
    });
  }

  /**
   * Rebuilds the cascade from a stored location. A survey stores no cluster, so when one is missing
   * it is looked up from the CBU — otherwise the cluster control would sit empty next to a CBU that
   * plainly belongs to one.
   */
  private async seedFromLocation(location: OrgLocation): Promise<void> {
    let clusterCode = location.clusterCode ?? null;

    if (!clusterCode && location.cbuCode) {
      // Failing to resolve the cluster must not stop the rest of the cascade from being restored,
      // so a lookup error leaves the cluster blank rather than rejecting.
      const allCbus = await firstValueFrom(this.lookups.getCbus()).catch(() => []);
      clusterCode = allCbus.find(cbu => cbu.code === location.cbuCode)?.clusterCode ?? null;
    }

    this.clusterCode.set(clusterCode);
    this.cbuCode.set(location.cbuCode ?? null);
    this.branchCode.set(location.branchCode ?? null);
    this.operationAreaCode.set(location.operationAreaCode ?? null);

    if (clusterCode) {
      this.loadCbus(clusterCode);
    }

    if (location.cbuCode) {
      this.loadBranches(location.cbuCode);
      this.loadOperationAreas(location.cbuCode);
    }
  }

  private loadClusters(): void {
    this.loadingClusters.set(true);
    this.lookups
      .getClusters()
      .pipe(finalize(() => this.loadingClusters.set(false)))
      .subscribe({
        next: items =>
          this.clusters.set(
            items.map(item => ({
              label: this.optionLabel(item.code, item.nameEn, item.nameAr),
              value: item.code!,
            })),
          ),
        error: () => this.clusters.set([]),
      });
  }

  private loadCbus(clusterCode: string | null): void {
    if (!clusterCode) {
      this.cbus.set([]);
      return;
    }

    this.loadingCbus.set(true);
    this.lookups
      .getCbus(clusterCode)
      .pipe(finalize(() => this.loadingCbus.set(false)))
      .subscribe({
        next: items =>
          this.cbus.set(
            items.map(item => ({
              label: this.optionLabel(item.code, item.nameEn, item.nameAr),
              value: item.code!,
            })),
          ),
        error: () => this.cbus.set([]),
      });
  }

  private loadBranches(cbuCode: string | null): void {
    if (!cbuCode) {
      this.branches.set([]);
      return;
    }

    this.loadingBranches.set(true);
    this.lookups
      .getBranches(cbuCode)
      .pipe(finalize(() => this.loadingBranches.set(false)))
      .subscribe({
        next: items =>
          this.branches.set(
            items.map(item => ({
              label: this.optionLabel(item.code, item.nameEn, item.nameAr),
              value: item.code!,
            })),
          ),
        error: () => this.branches.set([]),
      });
  }

  private loadOperationAreas(cbuCode: string | null): void {
    if (!cbuCode) {
      this.operationAreas.set([]);
      return;
    }

    this.loadingOperationAreas.set(true);
    this.lookups
      .getOperationAreas(cbuCode)
      .pipe(finalize(() => this.loadingOperationAreas.set(false)))
      .subscribe({
        next: items =>
          this.operationAreas.set(
            items.map(item => ({
              label: this.optionLabel(item.code, item.nameEn, item.nameAr),
              value: item.code!,
            })),
          ),
        error: () => this.operationAreas.set([]),
      });
  }

  /**
   * Names for chips that arrived as bare level/code pairs from the API. Reads the four levels
   * unfiltered — the lookup service caches each for the session, so this costs at most four calls
   * per browser session no matter how many forms open.
   */
  private loadDepartments(): void {
    this.loadingDepartments.set(true);

    this.lookups
      .getDepartments()
      .pipe(finalize(() => this.loadingDepartments.set(false)))
      .subscribe({
        next: items => {
          this.departments.set(
            items
              .filter(item => item.id != null)
              .map(item => ({
                label: this.localizedName(item.nameEn, item.nameAr) || `#${item.id}`,
                value: item.id!,
              })),
          );

          const labels = new Map(untracked(this.departmentLabels));

          for (const item of items) {
            if (item.id != null) {
              labels.set(item.id, this.localizedName(item.nameEn, item.nameAr) || `#${item.id}`);
            }
          }

          this.departmentLabels.set(labels);
        },
        error: () => this.departments.set([]),
      });
  }

  private loadScopeLabels(): void {
    type NamedUnit = { code?: string; nameEn?: string; nameAr?: string };

    // One signal write per list rather than per row: each write re-renders the chips, and copying
    // the map for every item made that quadratic in the size of the reference data.
    const merge = (level: OrgScopeLevel, items: readonly NamedUnit[]) => {
      const labels = new Map(untracked(this.scopeLabels));

      for (const item of items) {
        if (item.code) {
          labels.set(this.labelKey(level, item.code), this.localizedName(item.nameEn, item.nameAr) || item.code);
        }
      }

      this.scopeLabels.set(labels);
    };

    this.lookups.getClusters().subscribe({
      next: items => merge(ORG_SCOPE_LEVELS.cluster, items),
      error: () => undefined,
    });

    this.lookups.getCbus().subscribe({
      next: items => merge(ORG_SCOPE_LEVELS.cbu, items),
      error: () => undefined,
    });

    this.lookups.getBranches().subscribe({
      next: items => merge(ORG_SCOPE_LEVELS.branch, items),
      error: () => undefined,
    });

    this.lookups.getOperationAreas().subscribe({
      next: items => merge(ORG_SCOPE_LEVELS.operationArea, items),
      error: () => undefined,
    });
  }

  private rememberLabel(level: OrgScopeLevel, code: string): void {
    const options = this.optionsFor(level);
    const label = options.find(option => option.value === code)?.label;

    if (!label) {
      return;
    }

    const labels = new Map(untracked(this.scopeLabels));
    labels.set(this.labelKey(level, code), label);
    this.scopeLabels.set(labels);
  }

  private optionsFor(level: OrgScopeLevel): SelectOption[] {
    switch (level) {
      case ORG_SCOPE_LEVELS.cluster:
        return this.clusters();
      case ORG_SCOPE_LEVELS.cbu:
        return this.cbus();
      case ORG_SCOPE_LEVELS.branch:
        return this.branches();
      default:
        return this.operationAreas();
    }
  }

  /**
    * Whether an already-added scope sits beneath the one being added. Only decided for entries on
    * the path currently selected in the cascade — anything else is a different territory and stays.
    * A branch and an operation area never cover one another: they are siblings under the CBU.
    */
  private isCoveredBy(
    scope: OrgScopeAssignment,
    level: OrgScopeLevel,
    code: string,
    departmentId: number | null,
  ): boolean {
    // Different department, different claim — a broader place does not absorb a row that limits
    // itself to a kind of work the new row does not mention.
    if ((scope.departmentId ?? null) !== departmentId) {
      return false;
    }

    if (!scope.level || !scope.code) {
      return false;
    }

    if (!isLevelUnder(scope.level as OrgScopeLevel, level)) {
      return false;
    }

    const selectedPath: Record<string, string | null> = {
      [ORG_SCOPE_LEVELS.cluster]: this.clusterCode(),
      [ORG_SCOPE_LEVELS.cbu]: this.cbuCode(),
      [ORG_SCOPE_LEVELS.branch]: this.branchCode(),
      [ORG_SCOPE_LEVELS.operationArea]: this.operationAreaCode(),
    };

    return selectedPath[scope.level ?? ''] === scope.code && code === selectedPath[level];
  }

  private labelKey(level: string | undefined, code: string | undefined): string {
    return `${level ?? ''}|${(code ?? '').toUpperCase()}`;
  }

  private optionLabel(code?: string, nameEn?: string, nameAr?: string): string {
    const name = this.localizedName(nameEn, nameAr);

    return name ? `${code} — ${name}` : (code ?? '');
  }

  private localizedName(nameEn?: string, nameAr?: string): string {
    return this.transloco.getActiveLang() === 'ar'
      ? (nameAr || nameEn || '')
      : (nameEn || nameAr || '');
  }

  protected readonly emptyLocation = EMPTY_ORG_LOCATION;
}
