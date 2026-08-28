import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';

import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { InputTextModule } from 'primeng/inputtext';
import { ToastModule } from 'primeng/toast';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { MessageService } from 'primeng/api';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';
import { finalize } from 'rxjs/operators';

import {
  FsmsContractorDto,
  FsmsTeamsClient,
  TeamDto,
} from '../../../core/api/api-client.generated';
import { FsmsLookupService } from '../../../core/lookups/fsms-lookup.service';
import { GeoMapComponent } from '../../../shared/components/geo-map/geo-map.component';
import type { GeoPoint } from '../../../shared/components/geo-map/google-maps.types';
import { TeamFormComponent } from '../form/team-form.component';

@Component({
  selector: 'app-team-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TranslocoDirective,
    TableModule,
    ButtonModule,
    TagModule,
    InputTextModule,
    ToastModule,
    IconFieldModule,
    InputIconModule,
    DialogModule,
    SelectModule,
    TooltipModule,
    TeamFormComponent,
    GeoMapComponent,
  ],
  providers: [MessageService],
  templateUrl: './team-list.component.html',
  styleUrl: './team-list.component.scss'
})
export class TeamListComponent implements OnInit {
  private readonly teamsClient = inject(FsmsTeamsClient);
  private readonly lookupService = inject(FsmsLookupService);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);

  teams: TeamDto[] = [];
  totalRecords = 0;
  loading = false;
  searchTerm = '';
  selectedContractorId: number | null = null;
  contractorOptions: FsmsContractorDto[] = [];
  contractorsLoading = false;
  private page = 1;
  private pageSize = 10;

  displayDialog = false;
  editingTeam: TeamDto | null = null;
  dialogTitle = '';

  mapVisible = false;
  mapPoint: GeoPoint | null = null;
  mapTitle = '';

  ngOnInit(): void {
    this.contractorsLoading = true;
    this.lookupService.getContractors()
      .pipe(finalize(() => this.contractorsLoading = false))
      .subscribe({
        next: contractors => this.contractorOptions = contractors,
      });
    this.loadTeams();
  }

  loadTeams(event?: any): void {
    this.loading = true;
    if (event) {
      this.page = event.first !== undefined && event.rows
        ? Math.floor(event.first / event.rows) + 1 : 1;
      this.pageSize = event.rows ?? 10;
    }
    this.teamsClient.fsmsTeams_GetTeamsPaged(
      this.page,
      this.pageSize,
      this.searchTerm || undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      undefined,
      this.selectedContractorId ?? undefined,
    ).pipe(finalize(() => this.loading = false)).subscribe({
      next: (res) => {
        this.teams = res?.data?.items ?? [];
        this.totalRecords = res?.data?.totalCount ?? 0;
      },
      error: () => {
        this.messageService.add({
          severity: 'error',
          summary: this.transloco.translate('common.error'),
          detail: this.transloco.translate('teams.updateError'),
        });
      }
    });
  }

  onSearch(): void {
    this.page = 1;
    this.loadTeams();
  }

  clearSearch(): void {
    this.searchTerm = '';
    this.selectedContractorId = null;
    this.onSearch();
  }

  contractorLabel(team: TeamDto): string {
    if (team.contractorNameEn && team.contractorPoNumber) {
      return `${team.contractorNameEn} (${team.contractorPoNumber})`;
    }

    return team.contractorNameEn || team.contractorPoNumber || '-';
  }

  getActiveSeverity(isActive?: boolean): 'success' | 'danger' {
    return isActive ? 'success' : 'danger';
  }

  openNew(): void {
    this.editingTeam = null;
    this.dialogTitle = this.transloco.translate('teams.newTeam');
    this.displayDialog = true;
  }

  hasLastActiveLocation(team: TeamDto | null | undefined): boolean {
    return team?.lastActiveLatitude != null && team?.lastActiveLongitude != null;
  }

  openLastActiveMap(team: TeamDto | null | undefined): void {
    if (!this.hasLastActiveLocation(team) || !team) {
      return;
    }

    this.mapPoint = {
      lat: team.lastActiveLatitude!,
      lng: team.lastActiveLongitude!,
    };
    this.mapTitle = team.name
      ? `${this.transloco.translate('teams.viewLastActiveOnMap')} — ${team.name}`
      : this.transloco.translate('teams.viewLastActiveOnMap');
    this.mapVisible = true;
  }

  openEdit(team: TeamDto): void {
    this.editingTeam = { ...team };
    this.dialogTitle = this.transloco.translate('teams.editTeam');
    this.displayDialog = true;
  }

  onDialogHide(): void {
    this.displayDialog = false;
    this.editingTeam = null;
  }

  onSaved(): void {
    this.displayDialog = false;
    this.editingTeam = null;
    this.loadTeams();
  }

  toggleStatus(team: TeamDto): void {
    if (!team.teamId) return;

    const newStatus = !team.isActive;
    this.teamsClient.fsmsTeams_UpdateTeam(team.teamId, {
      id: team.teamId,
      userCode: team.userCode,
      name: team.name,
      mobile: team.mobile || undefined,
      teamType: team.teamType || undefined,
      contractorId: team.contractorId || undefined,
      scopes: team.scopes || [],
      isActive: newStatus
    }).subscribe({
      next: () => {
        const messageKey = newStatus ? 'teams.activatedSuccess' : 'teams.deactivatedSuccess';
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('common.success'),
          detail: this.transloco.translate(messageKey)
        });
        this.loadTeams();
      },
      error: (err: unknown) => {
        const apiMessage = (err as { errors?: { message?: string }[] } | null)?.errors?.[0]?.message;
        this.messageService.add({
          severity: 'error',
          summary: this.transloco.translate('common.error'),
          detail: apiMessage ?? this.transloco.translate('teams.updateError')
        });
      }
    });
  }
}
