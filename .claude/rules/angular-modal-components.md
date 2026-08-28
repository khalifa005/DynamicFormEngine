---
alwaysApply: true
description: >
  Angular modal/popup dialogs must be standalone components; group multiple
  modals for a page under a modals/ folder.
---

# Angular Modal Components (Required)

When working in `src/WebApps/MK.FormEngine.Web`, never embed modal markup directly in a page or list template. Each modal is its own standalone component.

## Rules

1. **One modal = one component** — wrap `p-dialog` (or equivalent) in a dedicated component with a clear name ending in `-dialog` or `-modal`.
2. **Group when there are many** — if a page/feature has two or more modals, place them in a `modals/` subfolder next to that page (e.g. `features/teams/list/modals/`).
3. **Thin parent** — the page only controls visibility and passes data via `input()` / `model()`; emit `output()` for save, cancel, and close.
4. **Prefer modals over navigation** — for add/edit on a list page, open a modal component instead of routing to a separate page.
5. **Overlays append to body** — every PrimeNG dropdown/datepicker/menu in the dialog needs `appendTo="body"`. A paginated `p-table` needs `paginatorDropdownAppendTo="body"` (it does not use `appendTo`). See skill `primeng-overlays`.

**Incorrect:** inline `<p-dialog>` in `team-list.component.html`.

**Correct:** `team-list/modals/team-form-dialog.component.ts` imported and used from the list page.

## Checklist

- [ ] No inline `p-dialog` / overlay markup in page templates
- [ ] Each modal is a standalone component with localized strings
- [ ] Multiple modals for one page live under `modals/`
