---
title: Use PrimeNG Table with Server-Side Lazy Loading
impact: HIGH
impactDescription: Correct pagination, sorting, and filtering for large lists
tags: primeng, table, lazy-loading, server-sort, server-filter
globs: src/WebApps/MK.FormEngine.Web/**
---

## Use PrimeNG Table with Server-Side Lazy Loading

For list/worklist/lookup grids, use PrimeNG `p-table` in **lazy** mode per [primeng.dev/table](https://primeng.dev/table). The server owns pagination, sorting, and filtering.

**Incorrect:**

```html
<!-- Loads all rows; client-only sort/filter -->
<p-table [value]="allData" [paginator]="true" [rows]="20"></p-table>
```

```typescript
// Sorting only the current page under lazy mode
this.items.set([...pageItems].sort(compare));
```

**Correct:**

```html
<p-table
  [value]="data()"
  [lazy]="true"
  [totalRecords]="total()"
  [loading]="loading()"
  (onLazyLoad)="load($event)"
  [paginator]="true"
  [rows]="20"
  [sortField]="sortField"
  [sortOrder]="sortOrder"
  paginatorDropdownAppendTo="body"
>
  <ng-template pTemplate="header">
    <tr>
      <th pSortableColumn="nameEn">
        <span>{{ t('fields.nameEn') }}</span>
        <p-sortIcon field="nameEn" />
      </th>
    </tr>
  </ng-template>
  <ng-template pTemplate="emptymessage">
    <tr><td colspan="1">{{ t('noData') }}</td></tr>
  </ng-template>
</p-table>
```

```typescript
load(event: TableLazyLoadEvent): void {
  // Map first/rows → page/pageSize; sortField/sortOrder → API SortBy/SortDirection
  // Map filters/search → API query params; never sort/filter only the page slice
}
```

Inside `p-dialog`, `paginatorDropdownAppendTo="body"` is required — `p-table` does not use `appendTo`. Skill `primeng-overlays`.

Full workflow: skill `primeng-table`. Cursor rule: `.cursor/rules/primeng-table.mdc`.
