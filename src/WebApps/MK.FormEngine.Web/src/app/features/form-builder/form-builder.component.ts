import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { InputTextModule } from 'primeng/inputtext';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { MessageService } from 'primeng/api';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { FormBuilderStore } from './store/form-builder.store';
import { FieldPaletteComponent } from './components/field-palette.component';
import { BuilderCanvasComponent } from './components/builder-canvas.component';
import { FieldEditorDialogComponent } from './components/field-editor-dialog.component';
import { FormPreviewDialogComponent } from './components/form-preview-dialog.component';
import { JsonOutputComponent } from './components/json-output.component';
import { FORM_BUILDER_SAMPLE_FILES, formBuilderSampleUrl } from './data/form-builder-samples';
import { DROP_IDS, ELEMENT_TYPES, type ElementType } from './models/form-builder.types';
import { FsmsTemplatesClient, SaveTemplateDefinitionCommand } from '../../core/api/api-client.generated';

@Component({
  selector: 'app-form-builder',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [FormBuilderStore, MessageService],
  imports: [
    CommonModule,
    FormsModule,
    InputTextModule,
    ButtonModule,
    ToastModule,
    TranslocoDirective,
    FieldPaletteComponent,
    BuilderCanvasComponent,
    FieldEditorDialogComponent,
    FormPreviewDialogComponent,
    JsonOutputComponent,
  ],
  templateUrl: './form-builder.component.html',
  styleUrl: './form-builder.component.scss',
})
export class FormBuilderComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly templatesClient = inject(FsmsTemplatesClient);
  private readonly router = inject(Router);
  protected readonly store = inject(FormBuilderStore);

  /** Bound from the `:id` route param (template designer mode). Absent on the standalone builder. */
  readonly id = input<string>();

  protected readonly editorVisible = signal(false);
  protected readonly editingKey = signal<string | null>(null);
  protected readonly loadingSample = signal(false);
  protected readonly previewVisible = signal(false);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly publishing = signal(false);

  /** Numeric template id when acting as a template designer, else null (standalone builder). */
  protected readonly templateId = computed<number | null>(() => {
    const raw = this.id();
    if (!raw) {
      return null;
    }
    const parsed = Number(raw);
    return Number.isFinite(parsed) ? parsed : null;
  });

  protected readonly isDesigner = computed<boolean>(() => this.templateId() !== null);

  /** CDK drop-list ids the palette items can drop onto (root + top-level sections). */
  protected readonly listIds = computed<string[]>(() => [
    DROP_IDS.CanvasRoot,
    ...this.store.elements().filter((el) => el.type === ELEMENT_TYPES.Section).map((el) => el.key),
  ]);

  async ngOnInit(): Promise<void> {
    if (this.templateId() !== null) {
      await this.loadTemplate();
    } else {
      await this.loadSample();
    }
  }

  private async loadTemplate(): Promise<void> {
    const id = this.templateId();
    if (id === null) {
      return;
    }

    this.loading.set(true);
    try {
      const res = await firstValueFrom(this.templatesClient.fsmsTemplates_GetTemplateById(id));
      const template = res.data;
      const definitionJson = template?.definitionJson;
      if (definitionJson) {
        this.store.loadFromJson(JSON.parse(definitionJson) as Record<string, unknown>);
      } else {
        this.store.resetForm();
      }

      if (template?.templateNameEn) {
        this.store.setNameEn(template.templateNameEn);
      }
      if (template?.templateNameAr) {
        this.store.setNameAr(template.templateNameAr);
      }
    } catch {
      this.messageService.add({
        severity: 'error',
        summary: this.transloco.translate('formBuilder.loadError'),
      });
    } finally {
      this.loading.set(false);
    }
  }

  protected async save(): Promise<void> {
    const id = this.templateId();
    if (id === null || this.saving() || this.publishing() || this.loading()) {
      return;
    }

    this.saving.set(true);
    try {
      // templateId comes from the URL route, NOT the body
      const command: SaveTemplateDefinitionCommand = { definitionJson: this.store.json() };
      await firstValueFrom(this.templatesClient.fsmsTemplates_SaveDefinition(id, command));
      this.messageService.add({
        severity: 'success',
        summary: this.transloco.translate('formBuilder.saveSuccess'),
      });
    } catch {
      this.messageService.add({
        severity: 'error',
        summary: this.transloco.translate('formBuilder.saveError'),
      });
    } finally {
      this.saving.set(false);
    }
  }

  protected async publish(): Promise<void> {
    const id = this.templateId();
    if (id === null || this.publishing() || this.saving() || this.loading()) {
      return;
    }

    // Persist the latest design before publishing so the version snapshot is current.
    this.publishing.set(true);
    try {
      const command: SaveTemplateDefinitionCommand = { definitionJson: this.store.json() };
      await firstValueFrom(this.templatesClient.fsmsTemplates_SaveDefinition(id, command));
      await firstValueFrom(this.templatesClient.fsmsTemplates_Publish(id));
      this.messageService.add({
        severity: 'success',
        summary: this.transloco.translate('formBuilder.publishSuccess'),
      });
    } catch {
      this.messageService.add({
        severity: 'error',
        summary: this.transloco.translate('formBuilder.publishError'),
      });
    } finally {
      this.publishing.set(false);
    }
  }

  protected backToTemplates(): void {
    void this.router.navigate(['/templates']);
  }

  protected async loadSample(): Promise<void> {
    this.loadingSample.set(true);
    try {
      const url = formBuilderSampleUrl(FORM_BUILDER_SAMPLE_FILES.AllFieldTypes);
      const raw = await firstValueFrom(this.http.get<Record<string, unknown>>(url));
      this.store.loadFromJson(raw);
      this.messageService.add({
        severity: 'success',
        summary: this.transloco.translate('formBuilder.loadSampleSuccess'),
      });
    } catch {
      this.messageService.add({
        severity: 'error',
        summary: this.transloco.translate('formBuilder.loadSampleError'),
      });
    } finally {
      this.loadingSample.set(false);
    }
  }

  protected newForm(): void {
    this.store.resetForm();
    this.messageService.add({
      severity: 'info',
      summary: this.transloco.translate('formBuilder.newFormSuccess'),
    });
  }

  protected onPaletteAdd(type: ElementType): void {
    const element = this.store.addFromType(type);
    // Sections are configured inline on the canvas; open the editor for other fields.
    if (type !== ELEMENT_TYPES.Section) {
      this.openEditor(element.key);
    }
  }

  protected openPreview(): void {
    this.previewVisible.set(true);
  }

  protected openEditor(key: string): void {
    this.editingKey.set(key);
    this.editorVisible.set(true);
  }
}
