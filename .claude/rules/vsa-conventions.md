---
alwaysApply: true
description: >
  Vertical Slice Architecture conventions for NWC — MediatR slices, Result pattern,
  and cross-slice communication rules.
---

# VSA Conventions

## Slice Organization

- Group each feature under `src/modules/Application/{FeatureName}/` with `Commands/`, `Queries/`, and `Validators/` subfolders.
- Each slice owns its request, handler, validator, and response types — no shared DTOs across slices.
- Slices communicate via MediatR only; never reference another slice's handler or DTO directly.

## Handlers

- All handlers return `Result<T>` or `Result<PagedResult<T>>` from `Shared.Core.Common.Result`.
- Use `Result<T>.Success(...)` and `Result<T>.Fail(...)` — never return raw values.
- Inject `IApplicationDbContext` directly; no repository pattern over EF Core.
- Pass `CancellationToken` as the last parameter on every async method.

## MediatR Pipeline (order matters)

1. `UnhandledExceptionBehaviour`
2. `AuthorizationBehaviour`
3. `ValidationBehaviour`
4. `PerformanceBehaviour`
5. `LoggingBehaviour`

## Validation

- Every command/query that writes data must have a FluentValidation validator.
- Use `.WithMessage(...)` on all rules — no default messages in production.

## API Layer

- Controllers extend `ApiControllerBase` and dispatch via `Mediator.Send(...)`.
- Keep controllers thin — no business logic in `MK.FormEngine.API`.
- Route prefix: `/api/v{version}/...` (current version: `v1`).
- After creating any endpoint, ask before syncing Postman; only use the Postman MCP after confirmation (see `postman-sync` skill).

## What NOT to Do

- Do not add logic to `Program.cs` — use `DependencyInjection.cs` extension methods.
- Do not use `EnsureCreated()` in production.
- Do not commit connection strings or JWT secrets.
