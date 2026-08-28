---
name: primeng-table
description: >-
  Add PrimeNG p-table list pages with server-side pagination, sorting, and
  filtering per https://primeng.dev/table lazy mode. Use when creating or
  updating Angular list tables, worklists, lookup grids, or when the user asks
  for p-table, onLazyLoad, server sort, or server filter.
---

# PrimeNG Table (Server-Side)

Canonical guide for NWC FSMS list tables. Follow [PrimeNG Table](https://primeng.dev/table) **lazy** mode: the server owns page, sort, and filter — the client never sorts/filters only the current page.

## When to use

- Any feature list / worklist / lookup grid backed by a paged API
- Adding columns, filters, or sortable headers to an existing `p-table`
- Reviewing a table that loads all rows or sorts client-side under `[lazy]="true"`

## Non-negotiables

1. **`[lazy]="true"`** + **`(onLazyLoad)`** for every server-backed list
2. **`[paginator]="true"`**, **`[rows]`**, **`[totalRecords]`** from API `totalCount`
3. **`[loading]`** bound to a signal/flag; clear in `finalize`
4. **Server sort** — map `TableLazyLoadEvent.sortField` / `sortOrder` to API params; do **not** `.sort()` the page slice
5. **Server filter** — send search/column filters to the API; reset to page 1 on filter apply
6. **Localized** labels, placeholders, empty state, page report (Transloco)
7. **`emptymessage`** template when there are zero rows
8. Overlay filters inside dialogs: `appendTo="body"` (see skill `primeng-overlays`)
9. **Paginator inside a dialog:** `paginatorDropdownAppendTo="body"` — `p-table` does **not** use `appendTo`. The rows-per-page overlay renders inline by default, and PrimeNG dismisses an inline overlay the moment an ancestor (the dialog body) scrolls. Same rule as `p-select`; the table just spells the input differently, which is why it slips past.

Reference implementation patterns: `survey-list`, `team-list`, `lookups`, `template-list`.

## Checklist

Copy and track:

```
- [ ] Standalone imports: TableModule (+ Sort/Filter pieces as needed)
- [ ] lazy + onLazyLoad + paginator + rows + totalRecords + loading
- [ ] TableLazyLoadEvent typed handler maps first/rows → page/pageSize
- [ ] Sortable columns use pSortableColumn + p-sortIcon
- [ ] sortField/sortOrder forwarded to API (SortBy / SortDirection or equivalent)
- [ ] Filters forwarded to API; page reset to 1 on apply/clear
- [ ] No client-side sort/filter of the current page when lazy is true
- [ ] emptymessage + Transloco keys (en + ar)
- [ ] API supports sort/filter params (or note backend gap before shipping UI-only sort)
- [ ] Inside p-dialog: paginatorDropdownAppendTo="body" (appendTo on sibling selects is not enough)
```

## Template skeleton

```html
<p-table
  [value]="items()"
  [lazy]="true"
  (onLazyLoad)="load($event)"
  [paginator]="true"
  [rows]="pageSize"
  [rowsPerPageOptions]="[10, 25, 50]"
  paginatorDropdownAppendTo="body"
  [totalRecords]="totalRecords()"
  [loading]="loading()"
  [sortField]="sortField"
  [sortOrder]="sortOrder"
  dataKey="id"
  styleClass="p-datatable-sm"
  responsiveLayout="scroll"
  [rowHover]="true"
  [showCurrentPageReport]="true"
  currentPageReportTemplate="{{ t('lookups.pageReport') }}"
>
  <ng-template pTemplate="caption">
    <!-- Server filters: search + selects; call applyFilters() -->
  </ng-template>

  <ng-template pTemplate="header">
    <tr>
      <th pSortableColumn="nameEn">
        <div class="flex items-center justify-between gap-2">
          <span>{{ t('feature.fields.nameEn') }}</span>
          <p-sortIcon field="nameEn" />
        </div>
      </th>
      <th class="w-[6rem] text-right">{{ t('feature.fields.actions') }}</th>
    </tr>
  </ng-template>

  <ng-template pTemplate="body" let-row>
    <tr>
      <td>{{ row.nameEn }}</td>
      <td class="text-right"><!-- actions --></td>
    </tr>
  </ng-template>

  <ng-template pTemplate="emptymessage">
    <tr>
      <td colspan="2" class="p-8 text-center text-surface-500">
        {{ t('feature.noData') }}
      </td>
    </tr>
  </ng-template>
</p-table>
```

### Sort (PrimeNG docs)

- Enable with `pSortableColumn="fieldName"` on `<th>` and `<p-sortIcon field="fieldName" />`
- Default `sortMode` is `single` (preferred for server lists)
- Optional: bind `[sortField]` / `[sortOrder]` to show current sort
- With `lazy`, sort changes emit `onLazyLoad` — handle sort there, not with client `Array.sort`

### Filter (PrimeNG docs + NWC)

**Preferred for FSMS lists:** caption/toolbar filters (search, status, dates) that call the API. Reset `page = 1` then reload.

**Column filters (optional):** bind `[filters]`, use `p-columnFilter` / `filter` on columns. With lazy, read `event.filters` in `onLazyLoad` and map match modes to API params. Prefer `display="menu"` for advanced filters.

Do not enable PrimeNG client filtering without lazy — it only filters the loaded page.

### Loading / empty (PrimeNG docs)

- Overlay: `[loading]="true"` while the request is in flight
- Empty: `emptymessage` template (required)

## Component handler

```typescript
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { finalize } from 'rxjs';

protected readonly items = signal<ItemDto[]>([]);
protected readonly totalRecords = signal(0);
protected readonly loading = signal(false);

protected page = 1;
protected pageSize = 10;
protected sortField: string | null = null;
protected sortOrder = 1; // 1 = asc, -1 = desc (PrimeNG)
protected search = '';

protected load(event?: TableLazyLoadEvent): void {
  if (event) {
    const rows = event.rows ?? this.pageSize;
    this.page = event.first !== undefined && rows ? Math.floor(event.first / rows) + 1 : 1;
    this.pageSize = rows;

    if (event.sortField) {
      this.sortField = Array.isArray(event.sortField) ? event.sortField[0]! : event.sortField;
      this.sortOrder = event.sortOrder ?? 1;
    } else {
      this.sortField = null;
      this.sortOrder = 1;
    }
  }

  this.loading.set(true);
  this.api
    .list(
      this.page,
      this.pageSize,
      this.search.trim() || undefined,
      this.sortField ?? undefined,
      this.sortOrder === -1 ? 'desc' : 'asc',
    )
    .pipe(finalize(() => this.loading.set(false)))
    .subscribe({
      next: (res) => {
        this.items.set(res.data?.items ?? []);
        this.totalRecords.set(res.data?.totalCount ?? 0);
      },
      error: () => {
        this.items.set([]);
        this.totalRecords.set(0);
      },
    });
}

protected applyFilters(): void {
  this.page = 1;
  this.load({ first: 0, rows: this.pageSize });
}
```

### Event → API mapping

| `TableLazyLoadEvent` | API / query |
|----------------------|-------------|
| `first`, `rows` | `page = floor(first/rows)+1`, `pageSize = rows` |
| `sortField` | `SortBy` (whitelist on server) |
| `sortOrder` `1` / `-1` | `SortDirection` `asc` / `desc` |
| caption filters / `filters` | query params (`SearchTerm`, status, dates, …) |

Lookups already use `PaginatedLookupQuery` (`SortBy`, `SortDirection`) + `LookupQueryableExtensions`. Feature lists should follow the same idea: whitelist sort fields server-side.

## Anti-patterns

```typescript
// BAD — sorts only the current page under lazy mode
this.items.set([...rawItems].sort(...));

// BAD — loads everything then filters in the browser
this.api.listAll().subscribe(all => this.items.set(all.filter(...)));

// BAD — no totalRecords / loading
<p-table [value]="items" [lazy]="true" (onLazyLoad)="load($event)" />
```

## Backend gap

If the UI needs sortable/filterable columns but the API lacks params:

1. Add query params + FluentValidation whitelist
2. Apply `OrderBy` / `Where` **before** `Skip`/`Take` / `PaginatedList.CreateAsync`
3. Regenerate Angular client (`npm run api:generate` with API running) before wiring the table

Do not ship client-only sort/filter as a permanent workaround on lazy tables.

## Docs

- [PrimeNG Table](https://primeng.dev/table) — Sort, Filter, Pagination, Loading, Empty State
- Project rule: `.cursor/rules/primeng-table.mdc`
- Related: `angular-best-practices-primeng`, skill `primeng-overlays`, Transloco localization
