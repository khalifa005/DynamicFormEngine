# Angular Primeng Best Practices

> Use with the core `angular-best-practices` skill.

---

## 1. PrimeNG

**Impact: MEDIUM** (UI components)

### 1.1 Tree-Shake PrimeNG Imports

**Impact: MEDIUM** (Reduces bundle size)

Import PrimeNG components individually using standalone component imports. Avoid importing entire PrimeNG modules. Use the standalone API available since PrimeNG v17+.

**Incorrect:**

```typescript
// Importing full module pulls in all components
import { ButtonModule } from 'primeng/button';
@NgModule({ imports: [ButtonModule, TableModule, DialogModule] })
```

**Correct:**

```typescript
@Component({
  imports: [Button, Select], // Standalone components — only what's needed
})
```

### 1.2 Use PrimeNG Table with Lazy Loading

**Impact: HIGH** (Handles 100k+ rows efficiently)

Use `[lazy]="true"` with `(onLazyLoad)` for **server-side** pagination, sorting, and filtering per [primeng.dev/table](https://primeng.dev/table). Map `TableLazyLoadEvent` (`first`, `rows`, `sortField`, `sortOrder`, filters) to the API. Sortable columns need `pSortableColumn` + `p-sortIcon`. Never client-sort/filter only the current page when lazy is on. Full workflow: skill `primeng-table`.

**Incorrect:**

```html
<!-- Loads all 10,000 rows into DOM -->
<p-table [value]="allData"></p-table>
```

**Correct:**

```html
<p-table [value]="data" [lazy]="true" [totalRecords]="total" [loading]="loading"
         (onLazyLoad)="load($event)" [paginator]="true" [rows]="20">
  <ng-template pTemplate="header">
    <tr>
      <th pSortableColumn="name">Name <p-sortIcon field="name" /></th>
    </tr>
  </ng-template>
</p-table>
```
### 1.3 Append Overlays to Body in Dialogs

**Impact: HIGH** (Dropdowns close on dialog scroll without this)

Every PrimeNG overlay inside `<p-dialog>` must append to `body`. `p-select` / `p-multiselect` / `p-datepicker` use `appendTo="body"`. A paginated `p-table` uses **`paginatorDropdownAppendTo="body"`** — it does not accept `appendTo`.

The rows-per-page overlay renders inline by default, and PrimeNG dismisses an inline overlay the moment an ancestor scrolls. In a dialog the ancestor is the dialog body — so opening the dropdown and scrolling down to reach the options was itself the thing that closed it. The table just spells the input differently, which is why it slips past even when the filter select above it already has `appendTo="body"`.

**Incorrect:**

```html
<p-select [options]="filters" appendTo="body" />
<p-table [value]="rows" [paginator]="true" [rowsPerPageOptions]="[20, 50]" />
```

**Correct:**

```html
<p-select [options]="filters" appendTo="body" />
<p-table [value]="rows" [paginator]="true" [rowsPerPageOptions]="[20, 50]"
         paginatorDropdownAppendTo="body" />
```

Full workflow: skill `primeng-overlays`.

### 1.4 Use PrimeNG Theme System

**Impact: MEDIUM** (Consistent design with design tokens)

Use PrimeNG's styled mode with Aura or Lara presets. Customize via design tokens in `providePrimeNG()` instead of overriding CSS classes. Use `dt()` function for accessing tokens in custom styles.

**Incorrect:**

```css
.p-button { background: #1976d2 !important; } /* Breaks theming */
```

**Correct:**

```typescript
providePrimeNG({
  theme: { preset: Aura, options: { darkModeSelector: '.dark' } }
})
```

---
