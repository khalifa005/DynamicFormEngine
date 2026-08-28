# Angular Client App Rules

When working on the Angular client app (`src/WebApps/NWC.Web`), adhere to the following rules:

1. **Localization:** All labels, messages, and other strings must be localized (English and Arabic). Do not use hardcoded display strings in templates or components.
2. **Popups over Navigation:** For Add/Edit actions related to a list page, use a popup/modal instead of a separate page with navigation. This reduces back-and-forth navigation for the user.
3. **Comprehensive Tables:** For tables and lists, use the most comprehensive table component available in the project (review its related skill or shared component if it exists). Ensure it implements server-side pagination and server-side main filtering.
