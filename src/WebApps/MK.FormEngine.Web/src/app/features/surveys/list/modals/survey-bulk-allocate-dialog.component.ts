import { Component, computed, inject, input, model, output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { finalize } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { MessageModule } from 'primeng/message';
import { MessageService } from 'primeng/api';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';

import {
  AllocationGroupDto,
  BulkAllocateSurveyItem,
  BulkAllocationItemResultDto,
  FsmsSurveysClient,
} from '../../../../core/api/api-client.generated';
import { LanguageService } from '../../../../core/i18n/language.service';
import { surveyErrorMessage } from '../../survey-error';
import { surveyStatusSeverity } from '../../survey-status';

interface TeamOption {
  readonly label: string;
  readonly value: number;

  /** How many surveys the crew already holds — shown as a badge beside its name. */
  readonly activeAssignmentCount: number;
}

/**
 * One group's editable choices. Mutable on purpose: the template binds `[(ngModel)]` straight at
 * these fields, so a group is one row of state rather than one entry in a parallel form array
 * that has to be kept aligned with the groups it describes.
 */
interface BulkGroupState {
  readonly group: AllocationGroupDto;
  readonly teamOptions: TeamOption[];
  fieldTeamId: number | null;
  dueDate: Date | null;
  completionDueDate: Date | null;
  note: string;
  expanded: boolean;
}

/**
 * Allocates a whole selection of surveys at once.
 *
 * The dispatcher never picks a crew per survey: the API groups the selection by the location tuple
 * that decides coverage and names the crews scoped to each group, so the choice is made once per
 * group. Groups nothing covers are shown with a warning rather than hidden — a survey silently
 * dropped from a batch is a survey nobody goes back for.
 */
@Component({
  selector: 'app-survey-bulk-allocate-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslocoDirective,
    ButtonModule,
    DatePickerModule,
    DialogModule,
    MessageModule,
    SelectModule,
    TagModule,
    TextareaModule,
    TooltipModule,
  ],
  templateUrl: './survey-bulk-allocate-dialog.component.html',
})
export class SurveyBulkAllocateDialogComponent {
  readonly visible = model.required<boolean>();
  readonly surveyIds = input<readonly number[]>([]);

  /** Raised once the run finishes with at least one success, so the list can reload. */
  readonly allocated = output<void>();

  private readonly surveysClient = inject(FsmsSurveysClient);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly groups = signal<BulkGroupState[]>([]);
  protected readonly ignoredCount = signal(0);
  protected readonly results = signal<BulkAllocationItemResultDto[] | null>(null);
  protected readonly succeededCount = signal(0);
  protected readonly failedCount = signal(0);

  protected readonly statusSeverity = surveyStatusSeverity;

  /** Groups the dispatcher left without a crew — skipped rather than blocking the run. */
  protected readonly skippedGroupCount = computed(
    () => this.groups().filter((state) => state.fieldTeamId == null).length,
  );

  protected readonly allocatableCount = computed(() =>
    this.groups()
      .filter((state) => state.fieldTeamId != null)
      .reduce((total, state) => total + (state.group.surveys?.length ?? 0), 0),
  );

  protected readonly failedResults = computed(
    () => this.results()?.filter((result) => !result.success) ?? [],
  );

  protected onShow(): void {
    this.groups.set([]);
    this.ignoredCount.set(0);
    this.results.set(null);
    this.succeededCount.set(0);
    this.failedCount.set(0);
    this.loadSuggestions();
  }

  protected cancel(): void {
    this.visible.set(false);
  }

  protected groupLocationLabel(group: AllocationGroupDto): string {
    const parts = [
      this.codeAndName(group.branchCode, group.branchNameEn, group.branchNameAr),
      this.codeAndName(
        group.operationAreaCode,
        group.operationAreaNameEn,
        group.operationAreaNameAr,
      ),
      this.codeAndName(group.cbuCode, group.cbuNameEn, group.cbuNameAr),
    ].filter((part) => part.length > 0);

    return parts.length > 0 ? parts.join(' · ') : this.transloco.translate('org.everywhere');
  }

  protected groupDepartmentLabel(group: AllocationGroupDto): string {
    return (
      this.localizedName(group.departmentNameEn, group.departmentNameAr) ||
      this.transloco.translate('org.allDepartments')
    );
  }

  protected toggleSurveys(state: BulkGroupState): void {
    state.expanded = !state.expanded;
  }

  /**
   * Writes the choice and republishes the list. The group objects are mutable, so the signal has
   * to be handed a new array for the counters derived from it to recompute.
   */
  protected setGroupTeam(state: BulkGroupState, fieldTeamId: number | null): void {
    state.fieldTeamId = fieldTeamId ?? null;
    this.groups.update((states) => [...states]);
  }

  protected allocate(): void {
    if (this.saving()) {
      return;
    }

    const items = this.buildItems();

    if (items.length === 0) {
      this.messageService.add({
        severity: 'warn',
        summary: this.transloco.translate('common.error'),
        detail: this.transloco.translate('surveys.bulkAllocate.nothingToAllocate'),
      });
      return;
    }

    this.saving.set(true);
    this.surveysClient
      .fsmsSurveys_BulkAllocate({ items })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (res) => {
          const succeeded = res.data?.succeededCount ?? 0;
          const failed = res.data?.failedCount ?? 0;

          this.succeededCount.set(succeeded);
          this.failedCount.set(failed);
          this.results.set(res.data?.results ?? []);

          this.messageService.add({
            severity: failed === 0 ? 'success' : succeeded === 0 ? 'error' : 'warn',
            summary: this.transloco.translate('surveys.bulkAllocate.summaryTitle'),
            detail: this.summaryMessage(succeeded, failed),
          });

          // Even a partial run changed the worklist, so it has to reload behind the summary.
          if (succeeded > 0) {
            this.allocated.emit();
          }
        },
        error: (error: unknown) => {
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: surveyErrorMessage(
              error,
              this.transloco.translate('surveys.bulkAllocate.submitFailed'),
            ),
          });
        },
      });
  }

  /** Every survey in a group inherits that group's crew, deadline and note. */
  private buildItems(): BulkAllocateSurveyItem[] {
    return this.groups()
      .filter((state) => state.fieldTeamId != null)
      .flatMap((state) =>
        (state.group.surveys ?? [])
          .filter((survey) => survey.surveyId != null)
          .map<BulkAllocateSurveyItem>((survey) => ({
            surveyId: survey.surveyId!,
            fieldTeamId: state.fieldTeamId!,
            dueDate: state.dueDate ?? undefined,
            completionDueDate: state.completionDueDate ?? undefined,
            note: state.note?.trim() || undefined,
          })),
      );
  }

  private loadSuggestions(): void {
    const ids = [...this.surveyIds()];

    if (ids.length === 0) {
      return;
    }

    this.loading.set(true);
    this.surveysClient
      .fsmsSurveys_GetAllocationSuggestions({ surveyIds: ids })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (res) => {
          this.groups.set((res.data?.groups ?? []).map((group) => this.toGroupState(group)));
          this.ignoredCount.set(res.data?.ignoredSurveyIds?.length ?? 0);
        },
        error: (error: unknown) => {
          this.groups.set([]);
          this.ignoredCount.set(0);
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: surveyErrorMessage(
              error,
              this.transloco.translate('surveys.bulkAllocate.loadFailed'),
            ),
          });
        },
      });
  }

  private toGroupState(group: AllocationGroupDto): BulkGroupState {
    const defaults = bulkSlaDeadlineDefaults(group);
    return {
      group,
      teamOptions: (group.candidateTeams ?? []).map((team) => ({
        label: `${team.userCode ?? ''} — ${team.name ?? ''}`.trim(),
        value: team.teamId!,
        activeAssignmentCount: team.activeAssignmentCount ?? 0,
      })),
      fieldTeamId: group.defaultFieldTeamId ?? null,
      dueDate: defaults.fillDue,
      completionDueDate: defaults.completionDue,
      note: '',
      // A group with no crew needs its surveys visible: the dispatcher has to know what is at
      // stake before deciding to skip it.
      expanded: (group.candidateTeams?.length ?? 0) === 0,
    };
  }

  private summaryMessage(succeeded: number, failed: number): string {
    if (failed === 0) {
      return this.transloco.translate('surveys.bulkAllocate.allSucceeded', { count: succeeded });
    }

    if (succeeded === 0) {
      return this.transloco.translate('surveys.bulkAllocate.allFailed');
    }

    return this.transloco.translate('surveys.bulkAllocate.partial', { succeeded, failed });
  }

  private codeAndName(code?: string, nameEn?: string, nameAr?: string): string {
    const name = this.localizedName(nameEn, nameAr);

    if (code && name) {
      return `${code} — ${name}`;
    }

    return code || name || '';
  }

  private localizedName(nameEn?: string, nameAr?: string): string {
    const preferred = this.language.current() === 'ar' ? nameAr : nameEn;
    return preferred?.trim() || nameEn?.trim() || nameAr?.trim() || '';
  }
}

/** Group-level defaults from the first survey that carries snapshotted SLA hours. */
function bulkSlaDeadlineDefaults(group: AllocationGroupDto): {
  fillDue: Date | null;
  completionDue: Date | null;
} {
  const survey = (group.surveys ?? []).find(
    (row) => row.teamFillSlaHours != null && row.teamFillSlaHours > 0,
  );
  const fillHours = survey?.teamFillSlaHours;
  const completionHours = survey?.completionSlaHours;
  if (fillHours == null || fillHours <= 0) {
    return { fillDue: null, completionDue: null };
  }

  const now = Date.now();
  const fillDue = survey?.dueDate
    ? new Date(survey.dueDate)
    : new Date(now + fillHours * 60 * 60 * 1000);
  const completionDue = survey?.completionDueDate
    ? new Date(survey.completionDueDate)
    : completionHours != null && completionHours > 0
      ? new Date(now + (fillHours + completionHours) * 60 * 60 * 1000)
      : null;

  return { fillDue, completionDue };
}
