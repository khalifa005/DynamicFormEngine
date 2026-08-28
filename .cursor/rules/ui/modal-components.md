---
title: Extract Modal Popups into Components
impact: MEDIUM
impactDescription: Smaller page components, reusable dialogs, easier testing
tags: ui, modal, dialog, popup, components
globs: src/WebApps/MK.FormEngine.Web/**
---

## Extract Modal Popups into Components

Every modal or popup (`p-dialog`, PrimeNG dynamic dialog, CDK overlay, etc.) must live in its own standalone component — not inline in a page or list template. When a feature/page owns two or more modals, group them under a `modals/` folder next to that page.

**Incorrect:**

```html
<!-- team-list.component.html — dialog markup mixed into the page -->
<p-dialog [(visible)]="displayDialog" [header]="dialogTitle">
  <app-team-form [team]="editingTeam" (saved)="onSaved()" />
</p-dialog>
```

**Correct:**

```typescript
// team-list/modals/team-form-dialog.component.ts
@Component({ selector: 'app-team-form-dialog', /* ... */ })
export class TeamFormDialogComponent {
  visible = model(false);
  team = input<Team | null>(null);
  saved = output<void>();
}
```

```html
<!-- team-list.component.html -->
<app-team-form-dialog [(visible)]="displayDialog" [team]="editingTeam" (saved)="onSaved()" />
```

**Folder layout (multiple modals):**

```text
features/form-builder/
  form-builder.component.ts
  modals/
    field-editor-dialog.component.ts
    rules-dialog.component.ts
    form-preview-dialog.component.ts
```

Use `-dialog` or `-modal` in the component filename. The parent page only toggles visibility and passes inputs/outputs.

Inside the dialog: `appendTo="body"` on every PrimeNG dropdown/datepicker/menu, and `paginatorDropdownAppendTo="body"` on every paginated `p-table` (the table does not use `appendTo`). See skill `primeng-overlays`.
