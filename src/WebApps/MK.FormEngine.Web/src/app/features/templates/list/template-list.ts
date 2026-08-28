import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { ButtonModule } from 'primeng/button';
import { TagModule } from 'primeng/tag';
import { ToolbarModule } from 'primeng/toolbar';
import { MenuModule } from 'primeng/menu';
import { MenuItem, MessageService, ConfirmationService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { finalize } from 'rxjs';
import { FsmsTemplatesClient, TemplateListItemDto, TemplateVersionDto } from '../../../core/api/api-client.generated';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TemplateForm } from '../form/template-form';
import {
  canArchive,
  canDeprecate,
  canPublish,
  templateStatusLabelKey,
  templateStatusSeverity,
  TemplateTagSeverity,
} from '../template-status';

/** The lifecycle transitions the row offers. Each maps to a POST on the templates controller. */
type TemplateAction = 'publish' | 'deprecate' | 'archive';

@Component({
  selector: 'app-template-list',
  standalone: true,
  imports: [
    CommonModule, RouterModule, ReactiveFormsModule,
    TableModule, ButtonModule, TagModule, ToolbarModule,
    MenuModule, ConfirmDialogModule, DialogModule, InputTextModule, ToastModule,
    TooltipModule, TranslocoDirective, TemplateForm
  ],
  providers: [MessageService, ConfirmationService],
  templateUrl: './template-list.html',
  styleUrl: './template-list.scss',
})
export class TemplateList implements OnInit {
  private templatesClient = inject(FsmsTemplatesClient);
  private router = inject(Router);
  private confirmationService = inject(ConfirmationService);
  private messageService = inject(MessageService);
  private fb = inject(FormBuilder);
  private transloco = inject(TranslocoService);

  templates: TemplateListItemDto[] = [];
  totalRecords: number = 0;
  loading: boolean = false;

  /** The row a lifecycle action is currently running for, so only that row shows a spinner. */
  actioningTemplateId?: number;

  /**
   * The last paging/filter state the table asked for. Reloading with no argument would send the
   * grid back to page 1 with its filters cleared, which is not what finishing an action should do.
   */
  private lastLazyEvent?: any;

  // Status helpers are bound from the template; re-exported so the markup stays declarative.
  readonly canPublish = canPublish;
  readonly canDeprecate = canDeprecate;
  readonly canArchive = canArchive;

  formDialogVisible: boolean = false;
  selectedTemplateIdForForm?: number;

  // ── Clone dialog ─────────────────────────────────────────────────────────
  cloneDialogVisible: boolean = false;
  cloning: boolean = false;
  selectedTemplateIdForClone?: number;
  cloneForm: FormGroup = this.fb.group({
    newTemplateCode: ['', Validators.required],
    newTemplateNameEn: ['', Validators.required],
    newTemplateNameAr: ['', Validators.required]
  });

  // ── Versions dialog ──────────────────────────────────────────────────────
  versionsDialogVisible: boolean = false;
  templateVersions: TemplateVersionDto[] = [];
  versionsLoading: boolean = false;
  selectedTemplateNameForVersions: string = '';

  menuItems: MenuItem[] = [];

  ngOnInit() {}

  loadTemplates(event?: any) {
    // Fall back to the last state the table asked for so a post-action reload keeps the user's page.
    if (event) {
      this.lastLazyEvent = event;
    } else {
      event = this.lastLazyEvent;
    }

    this.loading = true;
    const page = event && event.first !== undefined ? (event.first / (event.rows || 10)) + 1 : 1;
    const rows = event && event.rows ? event.rows : 10;

    let search: string | undefined;
    let category: string | undefined;
    let status: string | undefined;

    if (event && event.filters) {
      const getFilter = (field: string) => {
        const filter = event.filters[field];
        if (Array.isArray(filter)) {
          return filter[0]?.value;
        }
        return filter?.value;
      };

      search = getFilter('search') || undefined;
      category = getFilter('category') || undefined;
      status = getFilter('status') || undefined;
    }

    this.templatesClient.fsmsTemplates_GetTemplates(page, rows, category, status, undefined, undefined, search).subscribe({
      next: (res) => {
        if (res.data) {
          this.templates = res.data.items ?? [];
          this.totalRecords = res.data.totalCount ?? res.data.items?.length ?? 0;
        }
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      }
    });
  }

  getSeverity(status: string | undefined): TemplateTagSeverity {
    return templateStatusSeverity(status);
  }

  /** Localized status text, so the tag never shows the raw `PUBLISHED` constant. */
  getStatusLabel(status: string | undefined): string {
    return this.transloco.translate(templateStatusLabelKey(status));
  }

  editTemplate(id?: number) {
    this.selectedTemplateIdForForm = id;
    this.formDialogVisible = true;
  }

  onFormSaved() {
    this.formDialogVisible = false;
    this.loadTemplates();
  }

  designFields(id: number) {
    this.router.navigate(['/templates', id, 'designer']);
  }

  // ── Versions ─────────────────────────────────────────────────────────────
  openVersionsDialog(template: TemplateListItemDto) {
    this.selectedTemplateNameForVersions = template.templateNameEn ?? template.templateCode ?? '';
    this.templateVersions = [];
    this.versionsDialogVisible = true;
    this.versionsLoading = true;

    this.templatesClient
      .fsmsTemplates_GetTemplateVersions(template.templateId!)
      .pipe(finalize(() => (this.versionsLoading = false)))
      .subscribe({
        next: (res) => {
          this.templateVersions = res.data ?? [];
        },
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: this.transloco.translate('templates.versionsLoadError'),
          });
        }
      });
  }

  // ── Context menu ──────────────────────────────────────────────────────────
  getMenuItems(template: TemplateListItemDto): MenuItem[] {
    return [
      {
        label: this.transloco.translate('templates.viewVersions'),
        icon: 'pi pi-history',
        command: () => this.openVersionsDialog(template)
      },
      {
        label: this.transloco.translate('templates.clone'),
        icon: 'pi pi-copy',
        command: () => this.openCloneDialog(template.templateId!)
      },
      {
        label: this.transloco.translate('templates.publish'),
        icon: 'pi pi-check-circle',
        disabled: !canPublish(template.status),
        command: () => this.confirmAction(template.templateId!, 'publish')
      },
      {
        label: this.transloco.translate('templates.deprecate'),
        icon: 'pi pi-exclamation-circle',
        disabled: !canDeprecate(template.status),
        command: () => this.confirmAction(template.templateId!, 'deprecate')
      },
      {
        label: this.transloco.translate('templates.archive'),
        icon: 'pi pi-box',
        disabled: !canArchive(template.status),
        command: () => this.confirmAction(template.templateId!, 'archive')
      }
    ];
  }

  toggleMenu(event: Event, template: TemplateListItemDto, menu: any) {
    this.menuItems = this.getMenuItems(template);
    menu.toggle(event);
  }

  confirmAction(id: number, action: TemplateAction) {
    if (this.actioningTemplateId !== undefined) {
      return;
    }

    this.confirmationService.confirm({
      message: this.transloco.translate(`templates.confirm.${action}`),
      header: this.transloco.translate('templates.confirm.header'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.transloco.translate('common.confirm'),
      rejectLabel: this.transloco.translate('common.cancel'),
      accept: () => this.runAction(id, action)
    });
  }

  private runAction(id: number, action: TemplateAction) {
    this.actioningTemplateId = id;

    const request$ =
      action === 'publish' ? this.templatesClient.fsmsTemplates_Publish(id)
      : action === 'deprecate' ? this.templatesClient.fsmsTemplates_Deprecate(id)
      : this.templatesClient.fsmsTemplates_Archive(id);

    request$
      .pipe(finalize(() => (this.actioningTemplateId = undefined)))
      .subscribe({
        next: () => {
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('common.success'),
            detail: this.transloco.translate(`templates.${action}Success`),
          });
          this.loadTemplates();
        },
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: this.transloco.translate(`templates.${action}Error`),
          });
        }
      });
  }

  openCloneDialog(id: number) {
    this.selectedTemplateIdForClone = id;
    this.cloneForm.reset();
    this.cloneDialogVisible = true;
  }

  submitClone() {
    if (this.cloneForm.invalid || !this.selectedTemplateIdForClone || this.cloning) {
      this.cloneForm.markAllAsTouched();
      return;
    }

    this.cloning = true;
    // Only send the three rename fields — templateId comes from the URL route, NOT the body
    const { newTemplateCode, newTemplateNameEn, newTemplateNameAr } = this.cloneForm.value;

    this.templatesClient
      .fsmsTemplates_CloneTemplate(this.selectedTemplateIdForClone, {
        newTemplateCode,
        newTemplateNameEn,
        newTemplateNameAr
      })
      .pipe(finalize(() => (this.cloning = false)))
      .subscribe({
        next: () => {
          this.cloneDialogVisible = false;
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('common.success'),
            detail: this.transloco.translate('templates.cloneSuccess'),
          });
          this.loadTemplates();
        },
        error: () => {
          this.messageService.add({
            severity: 'error',
            summary: this.transloco.translate('common.error'),
            detail: this.transloco.translate('templates.cloneError'),
          });
        }
      });
  }
}
