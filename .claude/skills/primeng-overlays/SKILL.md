---
name: primeng-overlays
description: >-
  Append PrimeNG overlays to body inside p-dialog so they do not close on
  dialog scroll. Use when adding or reviewing p-select, p-multiselect,
  p-autoComplete, p-datepicker, p-menu, p-table, paginator, or any overlay
  inside a modal. Covers appendTo="body" and p-table
  paginatorDropdownAppendTo="body" (the table spells the input differently).
---

# PrimeNG Overlays in Dialogs

Every PrimeNG overlay inside `<p-dialog>` must append to `body`. The rows-per-page overlay is the one that keeps slipping: `p-table` does **not** use `appendTo`.

## Why

The rows-per-page overlay renders inline by default, and PrimeNG dismisses an inline overlay the moment an ancestor scrolls. In a dialog the ancestor is the dialog body — so opening the dropdown and scrolling down to reach the options was itself the thing that closed it. Appending to body takes the overlay out of the scrolling container, and it now follows the trigger instead of dying with it.

This is the same rule the project already applies to p-select inside p-dialog; the table just spells the input differently, which is why it slipped past — the filter select right above it already had appendTo="body".

## Required inputs

| Component | Input | When |
|-----------|--------|------|
| `<p-select>`, `<p-multiselect>`, `<p-autoComplete>`, `<p-datepicker>`, `<p-menu>`, `<p-contextMenu>` | `appendTo="body"` | Inside `<p-dialog>` (and any other scrollable overlay) |
| `<p-table>` with `[paginator]="true"` | `paginatorDropdownAppendTo="body"` | Inside `<p-dialog>` |

Leave a short HTML comment on the table when it lives in a dialog, so the next person adding a table to a dialog will hit this instead of rediscovering it.

## Correct

```html
<p-select [options]="filterOptions" appendTo="body" />

<!--
  paginatorDropdownAppendTo: same reason every p-select in a dialog needs
  appendTo="body"; the table only spells the input differently.
-->
<p-table
  [value]="records()"
  [paginator]="true"
  [rows]="pageSize"
  [rowsPerPageOptions]="[20, 50, 100]"
  paginatorDropdownAppendTo="body"
>
```

## Incorrect

```html
<!-- Filter has appendTo; table paginator does not — overlay dies on dialog scroll -->
<p-select [options]="filterOptions" appendTo="body" />
<p-table [value]="records()" [paginator]="true" [rowsPerPageOptions]="[20, 50, 100]" />
```

## Checklist

- [ ] Every dropdown/datepicker/menu in the dialog has `appendTo="body"`
- [ ] Every paginated `p-table` in the dialog has `paginatorDropdownAppendTo="body"`
- [ ] Do not assume `appendTo` on a sibling select covers the table

Project rule: `.cursor/rules/primeng-overlays.mdc`. Related: skill `primeng-table`.
