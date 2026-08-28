import { Component, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoModule } from '@jsverse/transloco';

import { ButtonModule } from 'primeng/button';
import { ChipModule } from 'primeng/chip';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';

import { ReportFilterValue, ReportSelectOption } from '../../models/report-filter.model';

interface ActiveChip {
  readonly key: keyof ReportFilterValue;
  readonly label: string;
}

/**
 * The filter row every report page opens with — date range, status, team, branch, operation area.
 * Fields are staged locally and only reach the page (via `apply`) when the operator clicks Apply,
 * matching how the existing dashboard filter bar behaves. Chips below the fields reflect what is
 * staged, with a per-chip clear, so "what will Apply do" is never a guess.
 */
@Component({
  selector: 'app-report-filter-bar',
  imports: [FormsModule, TranslocoModule, ButtonModule, ChipModule, DatePickerModule, SelectModule],
  templateUrl: './report-filter-bar.component.html',
  styleUrl: './report-filter-bar.component.scss',
})
export class ReportFilterBarComponent {
  readonly teamOptions = input<ReportSelectOption<number>[]>([]);
  readonly branchOptions = input<ReportSelectOption<string>[]>([]);
  readonly operationAreaOptions = input<ReportSelectOption<string>[]>([]);
  readonly statusOptions = input<ReportSelectOption<string>[]>([]);
  readonly sourceOptions = input<ReportSelectOption<string>[]>([]);
  readonly loading = input(false);
  /** Off for General Statistics — the underlying data has no team dimension there. */
  readonly showTeamFilter = input(true);
  readonly showSourceFilter = input(false);

  readonly apply = output<ReportFilterValue>();
  readonly reset = output<void>();

  protected fromDate: Date | null = null;
  protected toDate: Date | null = null;
  protected status: string | null = null;
  protected teamId: number | null = null;
  protected branchCode: string | null = null;
  protected operationAreaCode: string | null = null;
  protected source: string | null = null;

  protected activeChips(): ActiveChip[] {
    const chips: ActiveChip[] = [];

    if (this.fromDate) {
      chips.push({ key: 'fromDate', label: this.formatDate(this.fromDate) });
    }
    if (this.toDate) {
      chips.push({ key: 'toDate', label: this.formatDate(this.toDate) });
    }
    if (this.status) {
      chips.push({ key: 'status', label: this.optionLabel(this.statusOptions(), this.status) });
    }
    if (this.teamId) {
      chips.push({ key: 'teamId', label: this.optionLabel(this.teamOptions(), this.teamId) });
    }
    if (this.branchCode) {
      chips.push({ key: 'branchCode', label: this.optionLabel(this.branchOptions(), this.branchCode) });
    }
    if (this.operationAreaCode) {
      chips.push({
        key: 'operationAreaCode',
        label: this.optionLabel(this.operationAreaOptions(), this.operationAreaCode),
      });
    }
    if (this.source) {
      chips.push({ key: 'source', label: this.optionLabel(this.sourceOptions(), this.source) });
    }

    return chips;
  }

  protected clearChip(key: keyof ReportFilterValue): void {
    switch (key) {
      case 'fromDate':
        this.fromDate = null;
        break;
      case 'toDate':
        this.toDate = null;
        break;
      case 'status':
        this.status = null;
        break;
      case 'teamId':
        this.teamId = null;
        break;
      case 'branchCode':
        this.branchCode = null;
        break;
      case 'operationAreaCode':
        this.operationAreaCode = null;
        break;
      case 'source':
        this.source = null;
        break;
    }
  }

  protected onApply(): void {
    this.apply.emit({
      fromDate: this.fromDate,
      toDate: this.toDate,
      status: this.status,
      teamId: this.teamId,
      branchCode: this.branchCode,
      operationAreaCode: this.operationAreaCode,
      source: this.source,
    });
  }

  protected onReset(): void {
    this.fromDate = null;
    this.toDate = null;
    this.status = null;
    this.teamId = null;
    this.branchCode = null;
    this.operationAreaCode = null;
    this.source = null;
    this.reset.emit();
  }

  private optionLabel(options: readonly ReportSelectOption<string | number>[], value: string | number): string {
    return options.find((o) => o.value === value)?.label ?? String(value);
  }

  private formatDate(date: Date): string {
    return date.toISOString().slice(0, 10);
  }
}
