# MK.FormEngine — Dynamic Form Engine (Claude Code Project Guide)

## Project Overview

- **Solution:** MK.FormEngine.slnx
- **Type:** ASP.NET Core Web API (.NET 10)
- **Architecture:** Vertical Slice Architecture (VSA)
- **Team size:** Small (2–5)
- **Domain complexity:** Moderate (business rules, validation pipelines)

---

## Architecture & Project Structure

The project follows a Vertical Slice Architecture (VSA) combined with some Clean Architecture principles, organized into modules:

```text
src/
  Shared/                 ← Cross-cutting concerns
    Shared.Core/          ← Common structures (Result pattern), base classes
    Shared.Logs/          ← Serilog / Elasticsearch logging configuration
    Shared.Swagger/       ← OpenAPI / Scalar setup
  modules/
    Domain/               ← Domain models, Entities, Enums, Exceptions, Domain Events
    Application/          ← Vertical Slices (Commands, Queries, Validators, Mappings, Behaviours)
    Infrastructure/       ← EF Core AppDbContext, Identity, External Services integration
  WebApps/
    MK.FormEngine.API/    ← Main ASP.NET Core API entry point, Controllers/Endpoints
```

### VSA Rules

- Each slice owns its own request, handler, validator, and response types.
- Slices communicate via MediatR, not direct class references.
- Handlers MUST return standard `Result<T>` wrappers (or `Result<PagedResult<T>>`) to maintain consistent API responses and simplify global error handling.
- No shared DTOs across slices — duplicate if needed, refactor later.
- `Shared.Core` holds cross-cutting abstractions only (base classes, interfaces, pipeline behaviors).
- never using static magic string wrap it in static const
---

## Tech Stack

| Concern        | Technology                          |
|----------------|-------------------------------------|
| Framework      | ASP.NET Core (.NET 10)              |
| Mediator       | MediatR                             |
| Validation     | FluentValidation (DI extensions)    |
| Database       | SQL Server + EF Core                |
| Auth           | JWT Bearer                          |
| Authorization | Policy-based + custom `[RequireApiKey]` attribute |
| Caching        | Redis (StackExchange.Redis)         |
| API Docs       | Swagger (custom Shared.Swagger) |
| Logging        | Shared.Logs (configure Serilog here)|

---

## Development Conventions

### MediatR Pipeline

The MediatR pipeline behaviors are located in `modules/Application/Common/Behaviours` and execute in the following order:
1. `UnhandledExceptionBehaviour<,>` — Catches and logs unhandled exceptions.
2. `AuthorizationBehaviour<,>` — Evaluates security and roles before processing.
3. `ValidationBehaviour<,>` — Runs FluentValidation, throwing `ValidationException` on failure.
4. `PerformanceBehaviour<,>` — Logs warnings for slow requests (e.g. > 500ms).
5. `LoggingBehaviour<,>` — Logs request details.

### FluentValidation

- Every command/query that writes data **must** have a validator
- Register validators via `services.AddValidatorsFromAssembly(...)` in DI
- Use `.WithMessage(...)` for all rules — no default messages in production

### EF Core

- Data access is centralized in `IApplicationDbContext` implemented by `ApplicationDbContext` in `modules/Infrastructure`.
- Direct `DbContext` usage in handlers is preferred over the Repository pattern for simple slices (e.g., `_context.TodoItems.Add(entity)`).
- Connection string key: `"ConnectionStrings:DefaultConnection"`

### JWT Auth

- Validate: Issuer, Audience, Lifetime, SigningKey
- Store JWT settings under `"JwtSettings"` in appsettings
- Use `[Authorize]` on controllers/endpoints; never skip in non-public slices
- Claims mapping: prefer policy-based authorization over role strings

### Redis Caching

- Register via `IDistributedCache` or typed cache service in `Shared.Infrastructure`
- Cache keys: `"{entity}:{id}"` format
- Set explicit TTLs — no sliding expiration without justification
- Never cache user-specific data without including user ID in the key

### API Versioning

- Version prefix in route: `/api/v{version}/...`
- Current version: `v1`
- Add new versions rather than breaking existing ones

### Domain Entity Design (Light DDD)

- Avoid anemic domain models (bags of public setters with no behavior). Expose intent and behavior through domain methods.
- Keep state properties private or init-only to enforce encapsulation.
- Define a private parameterless constructor for EF Core, keeping the rest of the application from creating the entity in an invalid state.
- Use static factory methods (`Create`) as the only way to build entities, validating invariants and throwing custom `DomainException` for invalid states.
- Domain behavior and business rules (e.g., calculations, status updates) live inside the entity itself, not in separate domain/application services.

Example:
```csharp
using MovieManagement.Domain.Common;

namespace MovieManagement.Domain.Movies;

public sealed class Movie : Entity
{
    // EF Core needs a parameterless constructor. Keeping it private means the
    // rest of the application cannot create a Movie in an invalid state.
    private Movie()
    {
    }

    private Movie(string title, string director, DateOnly releaseDate, Genre genre, string synopsis)
    {
        Title = title;
        Director = director;
        ReleaseDate = releaseDate;
        Genre = genre;
        Synopsis = synopsis;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public string Title { get; private set; } = default!;
    public string Director { get; private set; } = default!;
    public DateOnly ReleaseDate { get; private set; }
    public Genre Genre { get; private set; }
    public string Synopsis { get; private set; } = default!;
    public double? AverageRating { get; private set; }
    public int RatingCount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    // A factory method is the only way to build a Movie. It enforces the rules
    // that must always be true, so an invalid Movie can never exist.
    public static Movie Create(string title, string director, DateOnly releaseDate, Genre genre, string synopsis)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("A movie must have a title.");
        }

        if (string.IsNullOrWhiteSpace(director))
        {
            throw new DomainException("A movie must have a director.");
        }

        return new Movie(title.Trim(), director.Trim(), releaseDate, genre, synopsis?.Trim() ?? string.Empty);
    }

    public void UpdateDetails(string title, string director, DateOnly releaseDate, Genre genre, string synopsis)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("A movie must have a title.");
        }

        Title = title.Trim();
        Director = director.Trim();
        ReleaseDate = releaseDate;
        Genre = genre;
        Synopsis = synopsis?.Trim() ?? string.Empty;
    }

    // Behavior lives on the entity, not in a service. The running average is a
    // business rule, so the Movie owns it.
    public void AddRating(int score)
    {
        if (score is < 1 or > 10)
        {
            throw new DomainException("A rating must be between 1 and 10.");
        }

        var runningTotal = (AverageRating ?? 0) * RatingCount + score;
        RatingCount++;
        AverageRating = Math.Round(runningTotal / RatingCount, 2);
    }
}
```

---


## Serilog Logging Strategy

### Folder Layout (per feature, per level)

```
logs/
│
├── featureName/
│   ├── Trace/          featureName-trace-yyyy-MM-dd.log
│   ├── Information/    featureName-info-yyyy-MM-dd.log
│   └── Error/          featureName-error-yyyy-MM-dd.log
│
└── _global/
    ├── Information/    global-info-yyyy-MM-dd.log
    └── Error/          global-error-20250531.log
```
See more at: Docs/How-Logs-Works.md can be updated if there are better implementations.

## Commands

```bash
# Build
dotnet build MK.FormEngine.slnx

# Run API
dotnet run --project src/WebApps/MK.FormEngine.API

# Add EF migration
dotnet ef migrations add <MigrationName> --project src/modules/Infrastructure --startup-project src/WebApps/MK.FormEngine.API

# Update database
dotnet ef database update --project src/modules/Infrastructure --startup-project src/WebApps/MK.FormEngine.API

```

---
## dotnet-claude-kit Integration

- Use `/scaffold` to add new features (auto-generates slice structure)
- Use `/health-check` to assess project health
- Use `/migrate` for safe EF Core migration workflow
- Use `/security-scan` before any release
- Use `/verify` to run the full 7-phase verification pipeline

## Claude Code Toolkit (Local — No Plugin Required)

All tooling is vendored in this repo. No external `dotnet-claude-kit` plugin needed.

| Location | Contents |
|----------|----------|
| `.claude/skills/` | 45+ workflow skills (`/scaffold`, `/verify`, `/health-check`, etc.) |
| `.claude/agents/` | 10 specialist agents + project-specific agents |
| `.claude/rules/` | Always-loaded and path-scoped coding rules |
| `.claude/hooks/` | Post-edit format, bash guard, scaffold restore |
| `.claude/knowledge/` | ADRs, anti-patterns, package recommendations |
| `tools/CWM.RoslynNavigator/` | Roslyn MCP server (15 semantic navigation tools) |
| `AGENTS.md` | Agent routing and orchestration |

### MCP Setup (one-time)

**Roslyn Navigator**

```bash
dotnet build tools/CWM.RoslynNavigator/CWM.RoslynNavigator.slnx
```

The Roslyn MCP server is configured in `.mcp.json` and targets `MK.FormEngine.slnx`.

**Postman**

Postman MCP is configured in `.cursor/mcp.json`. Copy `.cursor/mcp.local.env.example` to `.cursor/mcp.local.env` and set your `POSTMAN_API_KEY`. Target collection: `MK.FormEngine`. After creating endpoints, ask before running the `postman-sync` skill / Postman MCP.

### Key Slash Commands

- `/scaffold` — add new VSA features
- `/scaffold-slice` — project-specific slice scaffolding
- `/health-check` — assess project health
- `/migrate` — safe EF Core migration workflow
- `/security-scan` — pre-release security audit
- `/verify` — full 7-phase verification pipeline

See `AGENTS.md` for agent routing. Prefer Roslyn MCP tools (`find_symbol`, `get_project_graph`) over reading entire files.

---

## What NOT to Do

- Do not add logic to `Program.cs` — use extension methods DependencyInjection.cs in the related project
- Do not reference one feature slice from another — use MediatR instead
- Do not use `var` for non-obvious types
- Do not commit connection strings or JWT secrets — use user secrets or env vars
- Do not use `EnsureCreated()` in production code
- Do not return raw values from Handlers. Always wrap responses in `Result<T>.Success()`.
- Do not using static magic string wrap it in static const
- Do not using repository for handlers as new dotnet doesn't recommend it as ef do so
- Do not generate ef core migrations will do it manually just mention notify we need to add a new one 


---

## Workflow Rules

1. **Check KI Summaries:** Before starting any new task, check if there's a KI summary (Knowledge Item) relevant to your task and read the corresponding artifacts to adhere to established patterns.
2. **Follow VSA Pattern:** When building a new feature, group its Commands, Queries, and Validators together in `modules/Application/{FeatureName}/`.
3. **Use the Result Pattern:** All API endpoints and MediatR Handlers must use the `Result<T>` wrapper class in `Shared.Core.Common.Result`. Use `Result<T>.Success(...)` and `Result<T>.Fail(...)`.
4. **Prefer Pure Domain Entities:** Enforce invariant logic inside Domain Entities (in `modules/Domain`) and use `Guard.Against` to validate required state in Handlers.
5. **Keep Controllers Thin:** API Endpoints should only dispatch MediatR requests and return the HTTP response directly without custom logic.
6. **Postman MCP Synchronization:** After creating any new API endpoint, ask the user before using the Postman MCP server. Sync to `MK.FormEngine` only after they confirm (or when they explicitly request a Postman update).
7. **Regenerate Angular API Client:** After updating the API (endpoints/DTOs) and client work is needed, start `MK.FormEngine.API` and run `npm run api:generate` from `src/WebApps/MK.FormEngine.Web` before implementing Angular changes. Do not hand-edit `api-client.generated.ts`.

---

## Frontend (Angular) Rules

1. **Localization:** All labels, messages, and other strings must be localized (English and Arabic). Do not use hardcoded display strings in templates or components.
2. **Popups over Navigation:** For Add/Edit actions related to a list page, use a popup/modal instead of a separate page with navigation. This reduces back-and-forth navigation for the user.
3. **Comprehensive Tables:** For tables and lists, use the most comprehensive table component available in the project (review its related skill or shared component if it exists). Ensure it implements server-side pagination and server-side main filtering.
4. **API Loading UX (required):** Whenever the client app calls an API, show a loading state (spinner, skeleton, or table/list `[loading]`). Clear it in `finalize`/`finally` so it stops on both success and error.
5. **Action / Submit Busy State (required):** For any submit or action button that triggers an API call (save, delete, publish, clone, etc.), show a loader on that control and disable it for the request duration (PrimeNG: `[loading]` + `[disabled]`). Disable related cancel/secondary actions while the primary action is busy when appropriate.
6. **NSwag Client Regen (required):** After API contract changes that the web app must consume, run `npm run api:generate` in `src/WebApps/MK.FormEngine.Web` (with the API running) before writing client code against the new/changed types.
7. **PrimeNG Overlays in Dialogs:** When adding a PrimeNG dropdown (e.g., `<p-select>`, `<p-multiselect>`, `<p-autoComplete>`, `<p-datepicker>`) inside a modal (`<p-dialog>`), you MUST add `appendTo="body"`. When adding a paginated `<p-table>` inside a modal, you MUST add `paginatorDropdownAppendTo="body"` — the table does not use `appendTo`, which is why the rows-per-page overlay keeps closing when the dialog body scrolls. See skill `primeng-overlays`.
