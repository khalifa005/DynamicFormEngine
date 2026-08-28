import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { finalize } from 'rxjs/operators';

// PrimeNG v20
import { TableModule } from 'primeng/table';
import { Tabs, TabList, Tab, TabPanel, TabPanels } from 'primeng/tabs';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { InputTextModule } from 'primeng/inputtext';
import { ToastModule } from 'primeng/toast';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { DialogModule } from 'primeng/dialog';
import { CheckboxModule } from 'primeng/checkbox';
import { InputNumberModule } from 'primeng/inputnumber';
import { TooltipModule } from 'primeng/tooltip';
import { SelectModule } from 'primeng/select';
import { MessageService } from 'primeng/api';

import {
  FsmsLookupsClient,
  FsmsCbuDto,
  FsmsClusterDto,
  FsmsContractorDto,
  FsmsDepartmentDto,
  FsmsOperationAreaDto,
  FsmsBranchDto,
  FsmsFaTypeDto,
  FsmsReturnReasonDto,
  FsmsTaskTypeDto,
  FsmsCustomerTypeDto,
  FieldCatalogItemDto,
  CreateDepartmentCommand,
  UpdateDepartmentCommand,
  CreateCbuCommand,
  UpdateCbuCommand,
  CreateBranchCommand,
  UpdateBranchCommand,
  CreateOperationAreaCommand,
  UpdateOperationAreaCommand,
  CreateReturnReasonCommand,
  UpdateReturnReasonCommand,
  CreateFaTypeCommand,
  UpdateFaTypeCommand,
  CreateTaskTypeCommand,
  UpdateTaskTypeCommand,
  CreateContractorCommand,
  CreateCustomerTypeCommand,
  UpdateContractorCommand,
  UpdateCustomerTypeCommand,
} from '../../core/api/api-client.generated';
import { FsmsLookupService } from '../../core/lookups/fsms-lookup.service';

/** One selectable parent in the org geography — the value stored on the child row is its code. */
interface ParentOption {
  readonly label: string;
  readonly value: string;
}

/** The shape every geography level shares, so one mapper can build options for all of them. */
interface ParentSource {
  readonly code?: string;
  readonly nameEn?: string;
  readonly nameAr?: string;
}

@Component({
  selector: 'app-lookups',
  standalone: true,
  imports: [
    CommonModule, FormsModule, ReactiveFormsModule, TranslocoDirective,
    TableModule, Tabs, TabList, Tab, TabPanel, TabPanels,
    ButtonModule, TagModule,
    InputTextModule, ToastModule, IconFieldModule, InputIconModule,
    DialogModule, CheckboxModule, InputNumberModule, TooltipModule, SelectModule,
  ],
  providers: [MessageService],
  templateUrl: './lookups.component.html',
  styleUrl: './lookups.component.scss',
})
export class LookupsComponent implements OnInit {
  private readonly lookupsClient = inject(FsmsLookupsClient);
  private readonly lookupService = inject(FsmsLookupService);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly fb = inject(FormBuilder);

  // ── Parent pickers ──────────────────────────────────────────────────────────
  // A CBU hangs off a cluster; a branch and an operation area both hang off a CBU, as siblings.
  // Each dialog loads just the level above it, on open, so a typo can never orphan a row.
  clusterOptions: ParentOption[] = [];
  clusterOptionsLoading = false;
  cbuOptions: ParentOption[] = [];
  cbuOptionsLoading = false;

  // ── Departments ─────────────────────────────────────────────────────────────
  departments: FsmsDepartmentDto[] = [];
  departmentsTotalRecords = 0;
  departmentsLoading = false;
  departmentsSearch = '';
  private departmentsPage = 1;
  private departmentsPageSize = 10;
  deptDialogVisible = false;
  deptSaving = false;
  deptEditing: FsmsDepartmentDto | null = null;
  deptForm!: FormGroup;

  // ── Clusters ────────────────────────────────────────────────────────────────
  clusters: FsmsClusterDto[] = [];
  clustersTotalRecords = 0;
  clustersLoading = false;
  clustersSearch = '';
  private clustersPage = 1;
  private clustersPageSize = 10;

  // ── CBUs ────────────────────────────────────────────────────────────────────
  cbus: FsmsCbuDto[] = [];
  cbusTotalRecords = 0;
  cbusLoading = false;
  cbusSearch = '';
  private cbusPage = 1;
  private cbusPageSize = 10;
  cbuDialogVisible = false;
  cbuSaving = false;
  cbuEditing: FsmsCbuDto | null = null;
  cbuForm!: FormGroup;

  // ── Branches ───────────────────────────────────────────────────────────────────
  branches: FsmsBranchDto[] = [];
  branchesTotalRecords = 0;
  branchesLoading = false;
  branchesSearch = '';
  private branchesPage = 1;
  private branchesPageSize = 10;
  branchDialogVisible = false;
  branchSaving = false;
  branchEditing: FsmsBranchDto | null = null;
  branchForm!: FormGroup;

  // ── Operation Areas ─────────────────────────────────────────────────────────
  operationAreas: FsmsOperationAreaDto[] = [];
  operationAreasTotalRecords = 0;
  operationAreasLoading = false;
  operationAreasSearch = '';
  private operationAreasPage = 1;
  private operationAreasPageSize = 10;
  areaDialogVisible = false;
  areaSaving = false;
  areaEditing: FsmsOperationAreaDto | null = null;
  areaForm!: FormGroup;

  // ── FA Types ─────────────────────────────────────────────────────────────────
  faTypes: FsmsFaTypeDto[] = [];
  faTypesTotalRecords = 0;
  faTypesLoading = false;
  faTypesSearch = '';
  private faTypesPage = 1;
  private faTypesPageSize = 10;
  faTypeDialogVisible = false;
  faTypeSaving = false;
  faTypeEditing: FsmsFaTypeDto | null = null;
  faTypeForm!: FormGroup;

  // ── Field Catalog ───────────────────────────────────────────────────────────
  fieldCatalog: FieldCatalogItemDto[] = [];
  fieldCatalogTotalRecords = 0;
  fieldCatalogLoading = false;
  fieldCatalogSearch = '';
  private fieldCatalogPage = 1;
  private fieldCatalogPageSize = 10;

  // ── Return Reasons ──────────────────────────────────────────────────────────
  returnReasons: FsmsReturnReasonDto[] = [];
  returnReasonsTotalRecords = 0;
  returnReasonsLoading = false;
  returnReasonsSearch = '';
  private returnReasonsPage = 1;
  private returnReasonsPageSize = 10;
  reasonDialogVisible = false;
  reasonSaving = false;
  reasonEditing: FsmsReturnReasonDto | null = null;
  reasonForm!: FormGroup;

  // ── Task Types ──────────────────────────────────────────────────────────────
  taskTypes: FsmsTaskTypeDto[] = [];
  taskTypesTotalRecords = 0;
  taskTypesLoading = false;
  taskTypesSearch = '';
  private taskTypesPage = 1;
  private taskTypesPageSize = 10;
  taskTypeDialogVisible = false;
  taskTypeSaving = false;
  taskTypeEditing: FsmsTaskTypeDto | null = null;
  taskTypeForm!: FormGroup;

  // ── Customer Types ──────────────────────────────────────────────────────────
  customerTypes: FsmsCustomerTypeDto[] = [];
  customerTypesTotalRecords = 0;
  customerTypesLoading = false;
  customerTypesSearch = '';
  private customerTypesPage = 1;
  private customerTypesPageSize = 10;
  customerTypeDialogVisible = false;
  customerTypeSaving = false;
  customerTypeEditing: FsmsCustomerTypeDto | null = null;
  customerTypeForm!: FormGroup;

  // ── Contractors ─────────────────────────────────────────────────────────────
  contractors: FsmsContractorDto[] = [];
  contractorsTotalRecords = 0;
  contractorsLoading = false;
  contractorsSearch = '';
  private contractorsPage = 1;
  private contractorsPageSize = 10;
  contractorDialogVisible = false;
  contractorSaving = false;
  contractorEditing: FsmsContractorDto | null = null;
  contractorForm!: FormGroup;

  // Active tab value — PrimeNG v20 Tabs use string value
  activeTab = '0';
  private loadedTabs = new Set<string>();

  ngOnInit(): void {
    this.initForms();
    this.loadDepartments();
    this.loadedTabs.add('0');
  }

  private initForms(): void {
    this.deptForm = this.fb.group({
      nameEn: ['', [Validators.required, Validators.maxLength(250)]],
      nameAr: ['', [Validators.required, Validators.maxLength(250)]],
      isActive: [true],
    });

    this.cbuForm = this.fb.group({
      code: ['', [Validators.required, Validators.maxLength(50)]],
      clusterCode: ['', [Validators.required, Validators.maxLength(50)]],
      nameEn: ['', [Validators.required, Validators.maxLength(250)]],
      nameAr: ['', [Validators.required, Validators.maxLength(250)]],
      orgId: [null],
      orgCode: [null],
      defaultTaskZone: [null],
      isActive: [true],
    });

    this.branchForm = this.fb.group({
      code: ['', [Validators.required, Validators.maxLength(50)]],
      cbuCode: [null],
      taskZone: [null],
      branchCode: [null],
      nameEn: ['', [Validators.required, Validators.maxLength(250)]],
      nameAr: ['', [Validators.required, Validators.maxLength(250)]],
      isActive: [true],
    });

    this.areaForm = this.fb.group({
      code: ['', [Validators.required, Validators.maxLength(50)]],
      cbuCode: ['', [Validators.required, Validators.maxLength(50)]],
      mainAreaCode: [null],
      nameEn: ['', [Validators.required, Validators.maxLength(250)]],
      nameAr: ['', [Validators.required, Validators.maxLength(250)]],
      isActive: [true],
    });

    this.faTypeForm = this.fb.group({
      faTypeCode: ['', [Validators.required, Validators.maxLength(50)]],
      taskTypeId: [null, [Validators.required, Validators.min(1)]],
      nameEn: ['', [Validators.required, Validators.maxLength(250)]],
      nameAr: ['', [Validators.required, Validators.maxLength(250)]],
      isActive: [true],
    });

    this.reasonForm = this.fb.group({
      code: ['', [Validators.required, Validators.maxLength(50)]],
      nameEn: ['', [Validators.required, Validators.maxLength(250)]],
      nameAr: ['', [Validators.required, Validators.maxLength(250)]],
      sortOrder: [0, [Validators.required, Validators.min(0)]],
      isActive: [true],
    });

    this.taskTypeForm = this.fb.group({
      id: [null, [Validators.required, Validators.min(1)]],
      code: ['', [Validators.required, Validators.maxLength(50)]],
      nameEn: ['', [Validators.required, Validators.maxLength(250)]],
      nameAr: ['', [Validators.required, Validators.maxLength(250)]],
      isActive: [true],
    });

    this.customerTypeForm = this.fb.group({
      code: ['', [Validators.required, Validators.maxLength(50)]],
      nameEn: ['', [Validators.required, Validators.maxLength(250)]],
      nameAr: ['', [Validators.required, Validators.maxLength(250)]],
      isActive: [true],
    });

    this.contractorForm = this.fb.group({
      poNumber: ['', [Validators.required, Validators.maxLength(50)]],
      nameEn: ['', [Validators.required, Validators.maxLength(250)]],
      nameAr: ['', [Validators.required, Validators.maxLength(250)]],
      commercialRegistration: [null, [Validators.maxLength(50)]],
      isActive: [true],
    });
  }

  /**
   * Tabs load once, on first visit. The three geography tabs sit together in hierarchy order —
   * cluster, CBU, branch, operation area — so the page reads top-down the way the data nests.
   */
  onTabChange(value: string | number | undefined): void {
    const tab = String(value ?? '0');
    this.activeTab = tab;

    if (this.loadedTabs.has(tab)) {
      return;
    }

    this.loadedTabs.add(tab);

    const loaders: Record<string, () => void> = {
      '1': () => this.loadClusters(),
      '2': () => this.loadCbus(),
      '3': () => this.loadBranches(),
      '4': () => this.loadOperationAreas(),
      '5': () => this.loadFaTypes(),
      '6': () => this.loadFieldCatalog(),
      '7': () => this.loadReturnReasons(),
      '8': () => this.loadTaskTypes(),
      '9': () => this.loadCustomerTypes(),
      '10': () => this.loadContractors(),
    };

    loaders[tab]?.();
  }

  // ── Departments ─────────────────────────────────────────────────────────────

  loadDepartments(event?: any): void {
    this.departmentsLoading = true;
    if (event) {
      this.departmentsPage = event.first !== undefined && event.rows
        ? Math.floor(event.first / event.rows) + 1 : 1;
      this.departmentsPageSize = event.rows ?? 10;
    }
    this.lookupsClient.fsmsLookups_GetDepartments(
      this.departmentsPage,
      this.departmentsPageSize,
      this.departmentsSearch || undefined,
      undefined
    ).pipe(finalize(() => this.departmentsLoading = false)).subscribe({
      next: res => {
        this.departments = res?.data?.items ?? [];
        this.departmentsTotalRecords = res?.data?.totalCount ?? 0;
      },
      error: () => this.toastLoadError('lookups.departments.loadFailed'),
    });
  }

  onDepartmentsSearch(): void { this.departmentsPage = 1; this.loadDepartments(); }
  clearDepartmentsSearch(): void { this.departmentsSearch = ''; this.onDepartmentsSearch(); }

  openAddDept(): void {
    this.deptEditing = null;
    this.deptForm.reset({ isActive: true });
    this.deptDialogVisible = true;
  }

  openEditDept(dept: FsmsDepartmentDto): void {
    this.deptEditing = dept;
    this.deptForm.patchValue({ nameEn: dept.nameEn, nameAr: dept.nameAr, isActive: dept.isActive });
    this.deptDialogVisible = true;
  }

  saveDept(): void {
    if (this.deptForm.invalid) { this.deptForm.markAllAsTouched(); return; }
    this.deptSaving = true;

    const editing = this.deptEditing;
    const v = this.deptForm.getRawValue();

    const obs = editing
      ? this.lookupsClient.fsmsLookups_UpdateDepartment(editing.id!, {
          id: editing.id, nameEn: v.nameEn, nameAr: v.nameAr, isActive: v.isActive,
        } as UpdateDepartmentCommand)
      : this.lookupsClient.fsmsLookups_CreateDepartment({
          nameEn: v.nameEn, nameAr: v.nameAr, isActive: v.isActive,
        } as CreateDepartmentCommand);

    obs.pipe(finalize(() => this.deptSaving = false)).subscribe({
      next: () => {
        this.deptDialogVisible = false;
        this.lookupService.clear();
        this.loadedTabs.delete('0');
        this.loadDepartments();
        this.toastSuccess(editing ? 'lookups.departments.updateSuccess' : 'lookups.departments.createSuccess');
      },
      error: () => this.toastError(editing ? 'lookups.departments.updateError' : 'lookups.departments.createError'),
    });
  }

  toggleDeptActive(dept: FsmsDepartmentDto): void {
    this.lookupsClient.fsmsLookups_UpdateDepartment(dept.id!, {
      id: dept.id, nameEn: dept.nameEn, nameAr: dept.nameAr, isActive: !dept.isActive,
    } as UpdateDepartmentCommand).subscribe({
      next: () => { this.lookupService.clear(); this.loadedTabs.delete('0'); this.loadDepartments(); },
      error: () => this.toastError('lookups.departments.updateError'),
    });
  }

  // ── Clusters ────────────────────────────────────────────────────────────────

  loadClusters(event?: any): void {
    this.clustersLoading = true;
    if (event) {
      this.clustersPage = event.first !== undefined && event.rows
        ? Math.floor(event.first / event.rows) + 1 : 1;
      this.clustersPageSize = event.rows ?? 10;
    }
    this.lookupsClient.fsmsLookups_GetClusters(
      this.clustersPage,
      this.clustersPageSize,
      this.clustersSearch || undefined,
      undefined
    ).pipe(finalize(() => this.clustersLoading = false)).subscribe({
      next: res => {
        this.clusters = res?.data?.items ?? [];
        this.clustersTotalRecords = res?.data?.totalCount ?? 0;
      },
      error: () => this.toastLoadError('lookups.clusters.loadFailed'),
    });
  }

  onClustersSearch(): void { this.clustersPage = 1; this.loadClusters(); }
  clearClustersSearch(): void { this.clustersSearch = ''; this.onClustersSearch(); }

  // ── CBUs ────────────────────────────────────────────────────────────────────

  loadCbus(event?: any): void {
    this.cbusLoading = true;
    if (event) {
      this.cbusPage = event.first !== undefined && event.rows
        ? Math.floor(event.first / event.rows) + 1 : 1;
      this.cbusPageSize = event.rows ?? 10;
    }
    this.lookupsClient.fsmsLookups_GetCbus(
      this.cbusPage,
      this.cbusPageSize,
      this.cbusSearch || undefined,
      undefined,
      undefined
    ).pipe(finalize(() => this.cbusLoading = false)).subscribe({
      next: res => {
        this.cbus = res?.data?.items ?? [];
        this.cbusTotalRecords = res?.data?.totalCount ?? 0;
      },
      error: () => this.toastLoadError('lookups.cbus.loadFailed'),
    });
  }

  onCbusSearch(): void { this.cbusPage = 1; this.loadCbus(); }
  clearCbusSearch(): void { this.cbusSearch = ''; this.onCbusSearch(); }

  openAddCbu(): void {
    this.cbuEditing = null;
    this.cbuForm.reset({ isActive: true });
    this.loadClusterOptions(null);
    this.cbuDialogVisible = true;
  }

  openEditCbu(cbu: FsmsCbuDto): void {
    this.cbuEditing = cbu;
    this.cbuForm.patchValue({
      code: cbu.code, clusterCode: cbu.clusterCode, nameEn: cbu.nameEn, nameAr: cbu.nameAr,
      orgId: cbu.orgId, orgCode: cbu.orgCode, defaultTaskZone: cbu.defaultTaskZone, isActive: cbu.isActive,
    });
    this.loadClusterOptions(cbu.clusterCode);
    this.cbuDialogVisible = true;
  }

  saveCbu(): void {
    if (this.cbuForm.invalid) { this.cbuForm.markAllAsTouched(); return; }
    this.cbuSaving = true;

    const editing = this.cbuEditing;
    const v = this.cbuForm.getRawValue();

    const obs = editing
      ? this.lookupsClient.fsmsLookups_UpdateCbu(editing.id!, {
          id: editing.id,
          code: v.code, clusterCode: v.clusterCode, nameEn: v.nameEn, nameAr: v.nameAr,
          orgId: v.orgId ?? undefined, orgCode: v.orgCode ?? undefined,
          defaultTaskZone: v.defaultTaskZone ?? undefined, isActive: v.isActive,
        } as UpdateCbuCommand)
      : this.lookupsClient.fsmsLookups_CreateCbu({
          code: v.code, clusterCode: v.clusterCode, nameEn: v.nameEn, nameAr: v.nameAr,
          orgId: v.orgId ?? undefined, orgCode: v.orgCode ?? undefined,
          defaultTaskZone: v.defaultTaskZone ?? undefined, isActive: v.isActive,
        } as CreateCbuCommand);

    obs.pipe(finalize(() => this.cbuSaving = false)).subscribe({
      next: () => {
        this.cbuDialogVisible = false;
        this.lookupService.clear();
        this.loadedTabs.delete('2');
        this.loadCbus();
        this.toastSuccess(editing ? 'lookups.cbus.updateSuccess' : 'lookups.cbus.createSuccess');
      },
      error: () => this.toastError(editing ? 'lookups.cbus.updateError' : 'lookups.cbus.createError'),
    });
  }

  toggleCbuActive(cbu: FsmsCbuDto): void {
    this.lookupsClient.fsmsLookups_UpdateCbu(cbu.id!, {
      id: cbu.id, code: cbu.code, clusterCode: cbu.clusterCode,
      nameEn: cbu.nameEn, nameAr: cbu.nameAr, orgId: cbu.orgId, orgCode: cbu.orgCode,
      defaultTaskZone: cbu.defaultTaskZone, isActive: !cbu.isActive,
    } as UpdateCbuCommand).subscribe({
      next: () => { this.lookupService.clear(); this.loadedTabs.delete('2'); this.loadCbus(); },
      error: () => this.toastError('lookups.cbus.updateError'),
    });
  }

  // ── Branches ───────────────────────────────────────────────────────────────────

  loadBranches(event?: any): void {
    this.branchesLoading = true;
    if (event) {
      this.branchesPage = event.first !== undefined && event.rows
        ? Math.floor(event.first / event.rows) + 1 : 1;
      this.branchesPageSize = event.rows ?? 10;
    }
    this.lookupsClient.fsmsLookups_GetBranches(
      this.branchesPage,
      this.branchesPageSize,
      this.branchesSearch || undefined,
      undefined
    ).pipe(finalize(() => this.branchesLoading = false)).subscribe({
      next: res => {
        this.branches = res?.data?.items ?? [];
        this.branchesTotalRecords = res?.data?.totalCount ?? 0;
      },
      error: () => this.toastLoadError('lookups.branches.loadFailed'),
    });
  }

  onBranchesSearch(): void { this.branchesPage = 1; this.loadBranches(); }
  clearBranchesSearch(): void { this.branchesSearch = ''; this.onBranchesSearch(); }

  openAddBranch(): void {
    this.branchEditing = null;
    this.branchForm.reset({ isActive: true });
    this.loadCbuOptions(null);
    this.branchDialogVisible = true;
  }

  openEditBranch(branch: FsmsBranchDto): void {
    this.branchEditing = branch;
    this.branchForm.patchValue({
      code: branch.code, cbuCode: branch.cbuCode, taskZone: branch.taskZone,
      branchCode: branch.branchCode, nameEn: branch.nameEn, nameAr: branch.nameAr, isActive: branch.isActive,
    });
    this.loadCbuOptions(branch.cbuCode);
    this.branchDialogVisible = true;
  }

  saveBranch(): void {
    if (this.branchForm.invalid) { this.branchForm.markAllAsTouched(); return; }
    this.branchSaving = true;

    const editing = this.branchEditing;
    const v = this.branchForm.getRawValue();

    const obs = editing
      ? this.lookupsClient.fsmsLookups_UpdateBranch(editing.id!, {
          id: editing.id,
          code: v.code, cbuCode: v.cbuCode ?? undefined, taskZone: v.taskZone ?? undefined,
          branchCode: v.branchCode ?? undefined, nameEn: v.nameEn, nameAr: v.nameAr, isActive: v.isActive,
        } as UpdateBranchCommand)
      : this.lookupsClient.fsmsLookups_CreateBranch({
          code: v.code, cbuCode: v.cbuCode ?? undefined, taskZone: v.taskZone ?? undefined,
          branchCode: v.branchCode ?? undefined, nameEn: v.nameEn, nameAr: v.nameAr, isActive: v.isActive,
        } as CreateBranchCommand);

    obs.pipe(finalize(() => this.branchSaving = false)).subscribe({
      next: () => {
        this.branchDialogVisible = false;
        this.lookupService.clear();
        this.loadedTabs.delete('3');
        this.loadBranches();
        this.toastSuccess(editing ? 'lookups.branches.updateSuccess' : 'lookups.branches.createSuccess');
      },
      error: () => this.toastError(editing ? 'lookups.branches.updateError' : 'lookups.branches.createError'),
    });
  }

  toggleBranchActive(branch: FsmsBranchDto): void {
    this.lookupsClient.fsmsLookups_UpdateBranch(branch.id!, {
      id: branch.id, code: branch.code, cbuCode: branch.cbuCode, taskZone: branch.taskZone,
      branchCode: branch.branchCode, nameEn: branch.nameEn, nameAr: branch.nameAr, isActive: !branch.isActive,
    } as UpdateBranchCommand).subscribe({
      next: () => { this.lookupService.clear(); this.loadedTabs.delete('3'); this.loadBranches(); },
      error: () => this.toastError('lookups.branches.updateError'),
    });
  }

  // ── Operation Areas ─────────────────────────────────────────────────────────

  loadOperationAreas(event?: any): void {
    this.operationAreasLoading = true;
    if (event) {
      this.operationAreasPage = event.first !== undefined && event.rows
        ? Math.floor(event.first / event.rows) + 1 : 1;
      this.operationAreasPageSize = event.rows ?? 10;
    }
    this.lookupsClient.fsmsLookups_GetOperationAreas(
      this.operationAreasPage,
      this.operationAreasPageSize,
      this.operationAreasSearch || undefined,
      undefined,
      undefined
    ).pipe(finalize(() => this.operationAreasLoading = false)).subscribe({
      next: res => {
        this.operationAreas = res?.data?.items ?? [];
        this.operationAreasTotalRecords = res?.data?.totalCount ?? 0;
      },
      error: () => this.toastLoadError('lookups.operationAreas.loadFailed'),
    });
  }

  onOperationAreasSearch(): void { this.operationAreasPage = 1; this.loadOperationAreas(); }
  clearOperationAreasSearch(): void { this.operationAreasSearch = ''; this.onOperationAreasSearch(); }

  openAddArea(): void {
    this.areaEditing = null;
    this.areaForm.reset({ isActive: true });
    this.loadCbuOptions(null);
    this.areaDialogVisible = true;
  }

  openEditArea(area: FsmsOperationAreaDto): void {
    this.areaEditing = area;
    this.areaForm.patchValue({
      code: area.code, cbuCode: area.cbuCode, mainAreaCode: area.mainAreaCode,
      nameEn: area.nameEn, nameAr: area.nameAr, isActive: area.isActive,
    });
    this.loadCbuOptions(area.cbuCode);
    this.areaDialogVisible = true;
  }

  saveArea(): void {
    if (this.areaForm.invalid) { this.areaForm.markAllAsTouched(); return; }
    this.areaSaving = true;

    const editing = this.areaEditing;
    const v = this.areaForm.getRawValue();

    const obs = editing
      ? this.lookupsClient.fsmsLookups_UpdateOperationArea(editing.id!, {
          id: editing.id,
          code: v.code, cbuCode: v.cbuCode, mainAreaCode: v.mainAreaCode ?? undefined,
          nameEn: v.nameEn, nameAr: v.nameAr, isActive: v.isActive,
        } as UpdateOperationAreaCommand)
      : this.lookupsClient.fsmsLookups_CreateOperationArea({
          code: v.code, cbuCode: v.cbuCode, mainAreaCode: v.mainAreaCode ?? undefined,
          nameEn: v.nameEn, nameAr: v.nameAr, isActive: v.isActive,
        } as CreateOperationAreaCommand);

    obs.pipe(finalize(() => this.areaSaving = false)).subscribe({
      next: () => {
        this.areaDialogVisible = false;
        this.lookupService.clear();
        this.loadedTabs.delete('4');
        this.loadOperationAreas();
        this.toastSuccess(editing ? 'lookups.operationAreas.updateSuccess' : 'lookups.operationAreas.createSuccess');
      },
      error: () => this.toastError(editing ? 'lookups.operationAreas.updateError' : 'lookups.operationAreas.createError'),
    });
  }

  toggleAreaActive(area: FsmsOperationAreaDto): void {
    this.lookupsClient.fsmsLookups_UpdateOperationArea(area.id!, {
      id: area.id, code: area.code, cbuCode: area.cbuCode,
      mainAreaCode: area.mainAreaCode, nameEn: area.nameEn, nameAr: area.nameAr, isActive: !area.isActive,
    } as UpdateOperationAreaCommand).subscribe({
      next: () => { this.lookupService.clear(); this.loadedTabs.delete('4'); this.loadOperationAreas(); },
      error: () => this.toastError('lookups.operationAreas.updateError'),
    });
  }

  // ── FA Types ─────────────────────────────────────────────────────────────────

  loadFaTypes(event?: any): void {
    this.faTypesLoading = true;
    if (event) {
      this.faTypesPage = event.first !== undefined && event.rows
        ? Math.floor(event.first / event.rows) + 1 : 1;
      this.faTypesPageSize = event.rows ?? 10;
    }
    this.lookupsClient.fsmsLookups_GetFaTypes(
      this.faTypesPage,
      this.faTypesPageSize,
      this.faTypesSearch || undefined,
      undefined
    ).pipe(finalize(() => this.faTypesLoading = false)).subscribe({
      next: res => {
        this.faTypes = res?.data?.items ?? [];
        this.faTypesTotalRecords = res?.data?.totalCount ?? 0;
      },
      error: () => this.toastLoadError('lookups.faTypes.loadFailed'),
    });
  }

  onFaTypesSearch(): void { this.faTypesPage = 1; this.loadFaTypes(); }
  clearFaTypesSearch(): void { this.faTypesSearch = ''; this.onFaTypesSearch(); }

  openAddFaType(): void {
    this.faTypeEditing = null;
    this.faTypeForm.reset({ isActive: true });
    this.faTypeDialogVisible = true;
  }

  openEditFaType(faType: FsmsFaTypeDto): void {
    this.faTypeEditing = faType;
    this.faTypeForm.patchValue({
      faTypeCode: faType.faTypeCode, taskTypeId: faType.taskTypeId,
      nameEn: faType.nameEn, nameAr: faType.nameAr, isActive: faType.isActive,
    });
    this.faTypeDialogVisible = true;
  }

  saveFaType(): void {
    if (this.faTypeForm.invalid) { this.faTypeForm.markAllAsTouched(); return; }
    this.faTypeSaving = true;

    const editing = this.faTypeEditing;
    const v = this.faTypeForm.getRawValue();

    const obs = editing
      ? this.lookupsClient.fsmsLookups_UpdateFaType(editing.id!, {
          id: editing.id, faTypeCode: v.faTypeCode, taskTypeId: v.taskTypeId,
          nameEn: v.nameEn, nameAr: v.nameAr, isActive: v.isActive,
        } as UpdateFaTypeCommand)
      : this.lookupsClient.fsmsLookups_CreateFaType({
          faTypeCode: v.faTypeCode, taskTypeId: v.taskTypeId,
          nameEn: v.nameEn, nameAr: v.nameAr, isActive: v.isActive,
        } as CreateFaTypeCommand);

    obs.pipe(finalize(() => this.faTypeSaving = false)).subscribe({
      next: () => {
        this.faTypeDialogVisible = false;
        this.lookupService.clear();
        this.loadedTabs.delete('5');
        this.loadFaTypes();
        this.toastSuccess(editing ? 'lookups.faTypes.updateSuccess' : 'lookups.faTypes.createSuccess');
      },
      error: () => this.toastError(editing ? 'lookups.faTypes.updateError' : 'lookups.faTypes.createError'),
    });
  }

  toggleFaTypeActive(faType: FsmsFaTypeDto): void {
    this.lookupsClient.fsmsLookups_UpdateFaType(faType.id!, {
      id: faType.id, faTypeCode: faType.faTypeCode, taskTypeId: faType.taskTypeId,
      nameEn: faType.nameEn, nameAr: faType.nameAr, isActive: !faType.isActive,
    } as UpdateFaTypeCommand).subscribe({
      next: () => { this.lookupService.clear(); this.loadedTabs.delete('5'); this.loadFaTypes(); },
      error: () => this.toastError('lookups.faTypes.updateError'),
    });
  }

  // ── Field Catalog ───────────────────────────────────────────────────────────

  loadFieldCatalog(event?: any): void {
    this.fieldCatalogLoading = true;
    if (event) {
      this.fieldCatalogPage = event.first !== undefined && event.rows
        ? Math.floor(event.first / event.rows) + 1 : 1;
      this.fieldCatalogPageSize = event.rows ?? 10;
    }
    this.lookupsClient.fsmsLookups_GetFieldCatalog(
      this.fieldCatalogPage,
      this.fieldCatalogPageSize,
      this.fieldCatalogSearch || undefined
    ).pipe(finalize(() => this.fieldCatalogLoading = false)).subscribe({
      next: res => {
        this.fieldCatalog = res?.data?.items ?? [];
        this.fieldCatalogTotalRecords = res?.data?.totalCount ?? 0;
      },
      error: () => this.toastLoadError('lookups.fieldCatalog.loadFailed'),
    });
  }

  onFieldCatalogSearch(): void { this.fieldCatalogPage = 1; this.loadFieldCatalog(); }
  clearFieldCatalogSearch(): void { this.fieldCatalogSearch = ''; this.onFieldCatalogSearch(); }

  // ── Return Reasons ──────────────────────────────────────────────────────────

  loadReturnReasons(event?: any): void {
    this.returnReasonsLoading = true;
    if (event) {
      this.returnReasonsPage = event.first !== undefined && event.rows
        ? Math.floor(event.first / event.rows) + 1 : 1;
      this.returnReasonsPageSize = event.rows ?? 10;
    }
    this.lookupsClient.fsmsLookups_GetReturnReasons(
      this.returnReasonsPage,
      this.returnReasonsPageSize,
      this.returnReasonsSearch || undefined,
      undefined
    ).pipe(finalize(() => this.returnReasonsLoading = false)).subscribe({
      next: res => {
        this.returnReasons = res?.data?.items ?? [];
        this.returnReasonsTotalRecords = res?.data?.totalCount ?? 0;
      },
      error: () => this.toastLoadError('lookups.returnReasons.loadFailed'),
    });
  }

  onReturnReasonsSearch(): void { this.returnReasonsPage = 1; this.loadReturnReasons(); }
  clearReturnReasonsSearch(): void { this.returnReasonsSearch = ''; this.onReturnReasonsSearch(); }

  openAddReason(): void {
    this.reasonEditing = null;
    this.reasonForm.reset({ sortOrder: 0, isActive: true });
    this.reasonDialogVisible = true;
  }

  openEditReason(reason: FsmsReturnReasonDto): void {
    this.reasonEditing = reason;
    this.reasonForm.patchValue({
      code: reason.code, nameEn: reason.nameEn, nameAr: reason.nameAr,
      sortOrder: reason.sortOrder, isActive: reason.isActive,
    });
    this.reasonDialogVisible = true;
  }

  saveReason(): void {
    if (this.reasonForm.invalid) { this.reasonForm.markAllAsTouched(); return; }
    this.reasonSaving = true;

    const editing = this.reasonEditing;
    const v = this.reasonForm.getRawValue();

    const obs = editing
      ? this.lookupsClient.fsmsLookups_UpdateReturnReason(editing.id!, {
          id: editing.id, code: v.code, nameEn: v.nameEn, nameAr: v.nameAr,
          sortOrder: v.sortOrder, isActive: v.isActive,
        } as UpdateReturnReasonCommand)
      : this.lookupsClient.fsmsLookups_CreateReturnReason({
          code: v.code, nameEn: v.nameEn, nameAr: v.nameAr,
          sortOrder: v.sortOrder, isActive: v.isActive,
        } as CreateReturnReasonCommand);

    obs.pipe(finalize(() => this.reasonSaving = false)).subscribe({
      next: () => {
        this.reasonDialogVisible = false;
        this.lookupService.clear();
        this.loadedTabs.delete('7');
        this.loadReturnReasons();
        this.toastSuccess(editing ? 'lookups.returnReasons.updateSuccess' : 'lookups.returnReasons.createSuccess');
      },
      error: () => this.toastError(editing ? 'lookups.returnReasons.updateError' : 'lookups.returnReasons.createError'),
    });
  }

  toggleReasonActive(reason: FsmsReturnReasonDto): void {
    this.lookupsClient.fsmsLookups_UpdateReturnReason(reason.id!, {
      id: reason.id, code: reason.code, nameEn: reason.nameEn,
      nameAr: reason.nameAr, sortOrder: reason.sortOrder, isActive: !reason.isActive,
    } as UpdateReturnReasonCommand).subscribe({
      next: () => { this.lookupService.clear(); this.loadedTabs.delete('7'); this.loadReturnReasons(); },
      error: () => this.toastError('lookups.returnReasons.updateError'),
    });
  }

  // ── Task Types ──────────────────────────────────────────────────────────────

  loadTaskTypes(event?: any): void {
    this.taskTypesLoading = true;
    if (event) {
      this.taskTypesPage = event.first !== undefined && event.rows
        ? Math.floor(event.first / event.rows) + 1 : 1;
      this.taskTypesPageSize = event.rows ?? 10;
    }
    this.lookupsClient.fsmsLookups_GetTaskTypes(
      this.taskTypesPage,
      this.taskTypesPageSize,
      this.taskTypesSearch || undefined,
      undefined
    ).pipe(finalize(() => this.taskTypesLoading = false)).subscribe({
      next: res => {
        this.taskTypes = res?.data?.items ?? [];
        this.taskTypesTotalRecords = res?.data?.totalCount ?? 0;
      },
      error: () => this.toastLoadError('lookups.taskTypes.loadFailed'),
    });
  }

  onTaskTypesSearch(): void { this.taskTypesPage = 1; this.loadTaskTypes(); }
  clearTaskTypesSearch(): void { this.taskTypesSearch = ''; this.onTaskTypesSearch(); }

  openAddTaskType(): void {
    this.taskTypeEditing = null;
    this.taskTypeForm.reset({ isActive: true });
    this.taskTypeForm.get('id')?.enable();
    this.taskTypeDialogVisible = true;
  }

  openEditTaskType(taskType: FsmsTaskTypeDto): void {
    this.taskTypeEditing = taskType;
    this.taskTypeForm.patchValue({
      id: taskType.id, code: taskType.code,
      nameEn: taskType.nameEn, nameAr: taskType.nameAr, isActive: taskType.isActive,
    });
    this.taskTypeForm.get('id')?.disable();
    this.taskTypeDialogVisible = true;
  }

  saveTaskType(): void {
    if (this.taskTypeForm.invalid) { this.taskTypeForm.markAllAsTouched(); return; }
    this.taskTypeSaving = true;

    const editing = this.taskTypeEditing;
    const v = this.taskTypeForm.getRawValue();

    const obs = editing
      ? this.lookupsClient.fsmsLookups_UpdateTaskType(editing.id!, {
          id: editing.id, code: v.code, nameEn: v.nameEn, nameAr: v.nameAr, isActive: v.isActive,
        } as UpdateTaskTypeCommand)
      : this.lookupsClient.fsmsLookups_CreateTaskType({
          id: v.id, code: v.code, nameEn: v.nameEn, nameAr: v.nameAr, isActive: v.isActive,
        } as CreateTaskTypeCommand);

    obs.pipe(finalize(() => this.taskTypeSaving = false)).subscribe({
      next: () => {
        this.taskTypeDialogVisible = false;
        this.lookupService.clear();
        this.loadedTabs.delete('8');
        this.loadTaskTypes();
        this.toastSuccess(editing ? 'lookups.taskTypes.updateSuccess' : 'lookups.taskTypes.createSuccess');
      },
      error: () => this.toastError(editing ? 'lookups.taskTypes.updateError' : 'lookups.taskTypes.createError'),
    });
  }

  toggleTaskTypeActive(taskType: FsmsTaskTypeDto): void {
    this.lookupsClient.fsmsLookups_UpdateTaskType(taskType.id!, {
      id: taskType.id, code: taskType.code, nameEn: taskType.nameEn,
      nameAr: taskType.nameAr, isActive: !taskType.isActive,
    } as UpdateTaskTypeCommand).subscribe({
      next: () => { this.lookupService.clear(); this.loadedTabs.delete('8'); this.loadTaskTypes(); },
      error: () => this.toastError('lookups.taskTypes.updateError'),
    });
  }

  // ── Customer Types ──────────────────────────────────────────────────────────

  loadCustomerTypes(event?: any): void {
    this.customerTypesLoading = true;
    if (event) {
      this.customerTypesPage = event.first !== undefined && event.rows
        ? Math.floor(event.first / event.rows) + 1 : 1;
      this.customerTypesPageSize = event.rows ?? 10;
    }
    this.lookupsClient.fsmsLookups_GetCustomerTypes(
      this.customerTypesPage,
      this.customerTypesPageSize,
      this.customerTypesSearch || undefined,
      undefined
    ).pipe(finalize(() => this.customerTypesLoading = false)).subscribe({
      next: res => {
        this.customerTypes = res?.data?.items ?? [];
        this.customerTypesTotalRecords = res?.data?.totalCount ?? 0;
      },
      error: () => this.toastLoadError('lookups.customerTypes.loadFailed'),
    });
  }

  onCustomerTypesSearch(): void { this.customerTypesPage = 1; this.loadCustomerTypes(); }
  clearCustomerTypesSearch(): void { this.customerTypesSearch = ''; this.onCustomerTypesSearch(); }

  openAddCustomerType(): void {
    this.customerTypeEditing = null;
    this.customerTypeForm.reset({ isActive: true });
    this.customerTypeDialogVisible = true;
  }

  openEditCustomerType(customerType: FsmsCustomerTypeDto): void {
    this.customerTypeEditing = customerType;
    this.customerTypeForm.patchValue({
      code: customerType.code, nameEn: customerType.nameEn,
      nameAr: customerType.nameAr, isActive: customerType.isActive,
    });
    this.customerTypeDialogVisible = true;
  }

  saveCustomerType(): void {
    if (this.customerTypeForm.invalid) { this.customerTypeForm.markAllAsTouched(); return; }
    this.customerTypeSaving = true;

    const editing = this.customerTypeEditing;
    const v = this.customerTypeForm.getRawValue();

    const obs = editing
      ? this.lookupsClient.fsmsLookups_UpdateCustomerType(editing.id!, {
          id: editing.id, code: v.code, nameEn: v.nameEn, nameAr: v.nameAr, isActive: v.isActive,
        } as UpdateCustomerTypeCommand)
      : this.lookupsClient.fsmsLookups_CreateCustomerType({
          code: v.code, nameEn: v.nameEn, nameAr: v.nameAr, isActive: v.isActive,
        } as CreateCustomerTypeCommand);

    obs.pipe(finalize(() => this.customerTypeSaving = false)).subscribe({
      next: () => {
        this.customerTypeDialogVisible = false;
        this.lookupService.clear();
        this.loadedTabs.delete('9');
        this.loadCustomerTypes();
        this.toastSuccess(editing ? 'lookups.customerTypes.updateSuccess' : 'lookups.customerTypes.createSuccess');
      },
      error: () => this.toastError(editing ? 'lookups.customerTypes.updateError' : 'lookups.customerTypes.createError'),
    });
  }

  toggleCustomerTypeActive(customerType: FsmsCustomerTypeDto): void {
    this.lookupsClient.fsmsLookups_UpdateCustomerType(customerType.id!, {
      id: customerType.id, code: customerType.code, nameEn: customerType.nameEn,
      nameAr: customerType.nameAr, isActive: !customerType.isActive,
    } as UpdateCustomerTypeCommand).subscribe({
      next: () => { this.lookupService.clear(); this.loadedTabs.delete('9'); this.loadCustomerTypes(); },
      error: () => this.toastError('lookups.customerTypes.updateError'),
    });
  }

  // ── Contractors ─────────────────────────────────────────────────────────────

  loadContractors(event?: any): void {
    this.contractorsLoading = true;
    if (event) {
      this.contractorsPage = event.first !== undefined && event.rows
        ? Math.floor(event.first / event.rows) + 1 : 1;
      this.contractorsPageSize = event.rows ?? 10;
    }
    this.lookupsClient.fsmsLookups_GetContractors(
      this.contractorsPage,
      this.contractorsPageSize,
      this.contractorsSearch || undefined,
      undefined
    ).pipe(finalize(() => this.contractorsLoading = false)).subscribe({
      next: res => {
        this.contractors = res?.data?.items ?? [];
        this.contractorsTotalRecords = res?.data?.totalCount ?? 0;
      },
      error: () => this.toastLoadError('lookups.contractors.loadFailed'),
    });
  }

  onContractorsSearch(): void { this.contractorsPage = 1; this.loadContractors(); }
  clearContractorsSearch(): void { this.contractorsSearch = ''; this.onContractorsSearch(); }

  openAddContractor(): void {
    this.contractorEditing = null;
    this.contractorForm.reset({ isActive: true });
    this.contractorDialogVisible = true;
  }

  openEditContractor(contractor: FsmsContractorDto): void {
    this.contractorEditing = contractor;
    this.contractorForm.patchValue({
      poNumber: contractor.poNumber,
      nameEn: contractor.nameEn,
      nameAr: contractor.nameAr,
      commercialRegistration: contractor.commercialRegistration,
      isActive: contractor.isActive,
    });
    this.contractorDialogVisible = true;
  }

  saveContractor(): void {
    if (this.contractorForm.invalid) { this.contractorForm.markAllAsTouched(); return; }
    this.contractorSaving = true;

    const editing = this.contractorEditing;
    const v = this.contractorForm.getRawValue();

    const obs = editing
      ? this.lookupsClient.fsmsLookups_UpdateContractor(editing.id!, {
          id: editing.id,
          poNumber: v.poNumber,
          nameEn: v.nameEn,
          nameAr: v.nameAr,
          commercialRegistration: v.commercialRegistration || undefined,
          isActive: v.isActive,
        } as UpdateContractorCommand)
      : this.lookupsClient.fsmsLookups_CreateContractor({
          poNumber: v.poNumber,
          nameEn: v.nameEn,
          nameAr: v.nameAr,
          commercialRegistration: v.commercialRegistration || undefined,
          isActive: v.isActive,
        } as CreateContractorCommand);

    obs.pipe(finalize(() => this.contractorSaving = false)).subscribe({
      next: () => {
        this.contractorDialogVisible = false;
        this.lookupService.clear();
        this.loadedTabs.delete('10');
        this.loadContractors();
        this.toastSuccess(editing ? 'lookups.contractors.updateSuccess' : 'lookups.contractors.createSuccess');
      },
      error: () => this.toastError(editing ? 'lookups.contractors.updateError' : 'lookups.contractors.createError'),
    });
  }

  toggleContractorActive(contractor: FsmsContractorDto): void {
    this.lookupsClient.fsmsLookups_UpdateContractor(contractor.id!, {
      id: contractor.id,
      poNumber: contractor.poNumber,
      nameEn: contractor.nameEn,
      nameAr: contractor.nameAr,
      commercialRegistration: contractor.commercialRegistration,
      isActive: !contractor.isActive,
    } as UpdateContractorCommand).subscribe({
      next: () => { this.lookupService.clear(); this.loadedTabs.delete('10'); this.loadContractors(); },
      error: () => this.toastError('lookups.contractors.updateError'),
    });
  }

  // ── Parent option loaders ───────────────────────────────────────────────────

  /** Clusters for the CBU dialog. */
  private loadClusterOptions(currentCode?: string | null): void {
    this.clusterOptionsLoading = true;
    this.lookupService.getClusters()
      .pipe(finalize(() => this.clusterOptionsLoading = false))
      .subscribe({
        next: items => this.clusterOptions = this.toParentOptions(items, currentCode),
        error: () => {
          this.clusterOptions = this.toParentOptions([], currentCode);
          this.toastLoadError('lookups.clusters.loadFailed');
        },
      });
  }

  /** CBUs for the branch dialog. */
  private loadCbuOptions(currentCode?: string | null): void {
    this.cbuOptionsLoading = true;
    this.lookupService.getCbus()
      .pipe(finalize(() => this.cbuOptionsLoading = false))
      .subscribe({
        next: items => this.cbuOptions = this.toParentOptions(items, currentCode),
        error: () => {
          this.cbuOptions = this.toParentOptions([], currentCode);
          this.toastLoadError('lookups.cbus.loadFailed');
        },
      });
  }

  /**
   * The service only serves active parents, so a row still pointing at a retired one would lose its
   * value the moment the dialog opened. The current code is kept as an option in that case, which
   * makes the stale link visible and lets the editor either keep or replace it deliberately.
   */
  private toParentOptions(items: readonly ParentSource[], currentCode?: string | null): ParentOption[] {
    const options = items
      .filter(item => !!item.code)
      .map(item => ({ value: item.code!, label: this.parentLabel(item.code!, item.nameEn, item.nameAr) }));

    const code = currentCode?.trim();

    if (code && !options.some(option => option.value === code)) {
      options.unshift({ value: code, label: code });
    }

    return options;
  }

  private parentLabel(code: string, nameEn?: string, nameAr?: string): string {
    const name = this.transloco.getActiveLang() === 'ar'
      ? (nameAr || nameEn)
      : (nameEn || nameAr);

    return name ? `${code} — ${name}` : code;
  }

  // ── Helpers ─────────────────────────────────────────────────────────────────

  private toastLoadError(detailKey: string): void {
    this.messageService.add({
      severity: 'error',
      summary: this.transloco.translate('common.error'),
      detail: this.transloco.translate(detailKey),
    });
  }

  private toastSuccess(detailKey: string): void {
    this.messageService.add({
      severity: 'success',
      summary: this.transloco.translate('common.success'),
      detail: this.transloco.translate(detailKey),
    });
  }

  private toastError(detailKey: string): void {
    this.messageService.add({
      severity: 'error',
      summary: this.transloco.translate('common.error'),
      detail: this.transloco.translate(detailKey),
    });
  }

  getActiveSeverity(isActive?: boolean): 'success' | 'danger' {
    return isActive ? 'success' : 'danger';
  }
}
