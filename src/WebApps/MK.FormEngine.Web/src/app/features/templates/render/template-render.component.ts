import { ChangeDetectionStrategy, Component, computed, effect, inject, input, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { MessageModule } from 'primeng/message';
import { MessageService } from 'primeng/api';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { FsmsTemplatesClient } from '../../../core/api/api-client.generated';
import { LanguageService } from '../../../core/i18n/language.service';
import { PageHeaderService } from '../../../core/layout/page-header.service';
import { DynamicFormRendererComponent } from '../../../shared/components/dynamic-form/dynamic-form-renderer.component';

/**
 * Renders a stored template's form-builder definition as a live ngx-formly form, so a designer can
 * see what a crew will be handed. It is a preview and nothing more: a fill is always recorded
 * against a survey, so there is no submit here — filling happens from the survey worklist. The form
 * itself is `app-dynamic-form-renderer`, shared with the builder's preview dialog and the survey
 * fill dialog.
 */
@Component({
  selector: 'app-template-render',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ButtonModule,
    ToastModule,
    MessageModule,
    TranslocoDirective,
    DynamicFormRendererComponent,
  ],
  providers: [MessageService],
  template: `
    <ng-container *transloco="let t">
      <p-toast />
      <div class="p-0 md:p-0 space-y-4 max-w-4xl mx-auto">
        <!-- Page title/subtitle are rendered by the dashboard topbar (PageHeaderService). -->
        <div class="flex items-center justify-end gap-3">
          <p-button
            [label]="t('formBuilder.backToTemplates')"
            icon="pi pi-arrow-left"
            [text]="true"
            severity="secondary"
            (onClick)="back()"
          />
        </div>

        @if (loading()) {
          <p-message severity="info" [text]="t('templates.render.loading')" styleClass="w-full" />
        } @else {
          <p-message
            severity="info"
            [text]="t('templates.render.previewOnly')"
            styleClass="w-full"
          />
          <app-dynamic-form-renderer
            [definition]="definition()"
            [templateId]="templateId()"
            [emptyMessage]="t('templates.render.empty')"
          />
        }
      </div>
    </ng-container>
  `,
})
export class TemplateRenderComponent implements OnInit {
  readonly id = input<string>();

  private readonly templatesClient = inject(FsmsTemplatesClient);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);
  private readonly pageHeader = inject(PageHeaderService);
  private readonly router = inject(Router);

  protected readonly loading = signal(true);
  /** The template's definition document, parsed once and handed to the renderer. */
  protected readonly definition = signal<Record<string, unknown> | null>(null);
  protected readonly titleEn = signal('');
  protected readonly titleAr = signal('');

  protected readonly titleText = computed(() =>
    this.language.current() === 'ar' ? this.titleAr() || this.titleEn() : this.titleEn() || this.titleAr(),
  );

  constructor() {
    // The template name is only known after loading, so push it into the topbar header.
    effect(() => {
      const title = this.titleText();
      this.pageHeader.set({
        titleKey: 'templates.render.title',
        titleText: title || undefined,
        subtitleKey: 'templates.render.subtitle',
      });
    });
  }

  /** Also handed to the renderer, so media fields upload each picked file straight away. */
  protected readonly templateId = computed<number | null>(() => {
    const raw = this.id();
    if (!raw) {
      return null;
    }
    const parsed = Number(raw);
    return Number.isFinite(parsed) ? parsed : null;
  });

  async ngOnInit(): Promise<void> {
    const id = this.templateId();
    if (id === null) {
      this.loading.set(false);
      return;
    }

    try {
      const res = await firstValueFrom(this.templatesClient.fsmsTemplates_GetTemplateById(id));
      const definitionJson = res.data?.definitionJson;
      this.titleEn.set(res.data?.templateNameEn ?? '');
      this.titleAr.set(res.data?.templateNameAr ?? '');

      if (definitionJson) {
        this.definition.set(JSON.parse(definitionJson) as Record<string, unknown>);
      }
    } catch {
      this.messageService.add({
        severity: 'error',
        summary: this.transloco.translate('templates.render.loadError'),
      });
    } finally {
      this.loading.set(false);
    }
  }

  protected back(): void {
    void this.router.navigate(['/templates']);
  }
}
