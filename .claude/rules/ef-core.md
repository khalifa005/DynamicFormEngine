---
paths:
  - "src/modules/Infrastructure/**/*.cs"
  - "**/*DbContext*.cs"
  - "**/Migrations/**"
description: EF Core and migration rules for NWC Infrastructure layer.
---

# EF Core Rules

- Data access lives in `ApplicationDbContext` implementing `IApplicationDbContext`.
- Use migrations — never `EnsureCreated()` in production.
- Connection string key: `"ConnectionStrings:DefaultConnection"`.

## Migrations

```bash
dotnet ef migrations add <Name> --project src/modules/Infrastructure --startup-project src/WebApps/MK.FormEngine.API
dotnet ef database update --project src/modules/Infrastructure --startup-project src/WebApps/MK.FormEngine.API
```

- Migration names must be descriptive (e.g. `AddTodoItemIndex`, not `Migration_001`).
- Always verify the generated `Up()` and `Down()` methods.

## Configuration

- Use `HasMaxLength()` on string properties — never rely on defaults.
- Add indexes for foreign key columns.
- Use `ValueConverter` for enum-to-string mapping when needed.
