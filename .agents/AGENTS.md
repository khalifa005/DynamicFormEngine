# Agent Customization Rules

## Postman MCP Integration

- **Target Workspace Collection:** `FSMS_Field Survey Management System`
- **Postman Synchronization Rule:** After creating any new API endpoint in the backend (`NWC.API` / Application slices), ask the user before using the Postman MCP server. Sync to `FSMS_Field Survey Management System` only after they confirm (or when they explicitly request a Postman update).

## Angular Client App Rules

1. **Localization:** All labels, messages, and other strings must be localized (English and Arabic). Do not use hardcoded display strings in templates or components.
2. **Popups over Navigation:** For Add/Edit actions related to a list page, use a popup/modal instead of a separate page with navigation. This reduces back-and-forth navigation for the user.
3. **Comprehensive Tables:** For tables and lists, use PrimeNG `p-table` in lazy mode with server-side pagination, sorting, and filtering. Follow skill `primeng-table` and rule `.cursor/rules/primeng-table.mdc` (based on https://primeng.dev/table). Do not client-sort/filter only the current page when `[lazy]="true"`.
4. **PrimeNG Overlays in Dialogs:** When adding a PrimeNG dropdown (e.g., `<p-select>`, `<p-multiselect>`, `<p-autoComplete>`, `<p-datepicker>`) inside a modal (`<p-dialog>`), you MUST add `appendTo="body"`. When adding a paginated `<p-table>` inside a modal, you MUST add `paginatorDropdownAppendTo="body"` — the table does not use `appendTo`, which is why the rows-per-page overlay keeps closing when the dialog body scrolls. See skill `primeng-overlays`.
