---
description: >-
  PrimeNG overlays inside p-dialog must append to body, including p-table
  paginatorDropdownAppendTo="body" (the table spells the input differently).
globs:
  - "src/WebApps/MK.FormEngine.Web/**/*.html"
  - "src/WebApps/MK.FormEngine.Web/**/*.ts"
  - "**/modals/**"
---

# PrimeNG Overlays in Dialogs

When adding a PrimeNG dropdown overlay (e.g. `<p-select>`, `<p-multiselect>`, `<p-autoComplete>`, `<p-datepicker>`, `<p-menu>`) inside a modal (`<p-dialog>`), add `appendTo="body"`.

When adding a paginated `<p-table>` inside a modal, add `paginatorDropdownAppendTo="body"`. The table does **not** use `appendTo` — that is why this keeps getting missed even when the filter select above the table already has `appendTo="body"`.

## Why

The rows-per-page overlay renders inline by default, and PrimeNG dismisses an inline overlay the moment an ancestor scrolls. In a dialog the ancestor is the dialog body — so opening the dropdown and scrolling down to reach the options was itself the thing that closed it. Appending to body takes the overlay out of the scrolling container, and it now follows the trigger instead of dying with it.

This is the same rule the project already applies to p-select inside p-dialog; the table just spells the input differently, which is why it slipped past.

**Incorrect:** `<p-table [paginator]="true">` inside `p-dialog` with no `paginatorDropdownAppendTo`.

**Correct:** `paginatorDropdownAppendTo="body"` on that table; `appendTo="body"` on every select/datepicker/menu in the same dialog.

See skill `primeng-overlays` and `.cursor/rules/primeng-overlays.mdc`.
