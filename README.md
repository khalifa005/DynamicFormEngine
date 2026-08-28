# MK.FormEngine

A **dynamic form and field survey engine**: design templates in a visual builder, assign work to field crews, collect submissions (including photos, GPS, and barcodes), then review, approve, and export results.

The back office is a bilingual (English / Arabic, RTL) Angular admin. The API is ASP.NET Core (.NET 10). A mobile app talks to a frozen field-team contract under `/api/v1/fsms/field-team`.

---

## Goal

Organizations that send people into the field still end up with rigid paper forms, one-off spreadsheets, or a custom app per survey type. MK.FormEngine exists so you can:

1. **Define forms once** in a catalog-backed builder (shared field names, types, and rules).
2. **Publish templates** and allocate surveys to teams, contractors, and org units (cluster / business unit / branch / operation area).
3. **Collect structured data** from crews — text, choices, numbers, dates, photos, GPS, barcodes — with validation and a full submission lifecycle (draft → submitted → review → approved / returned).
4. **Operate the work** from an admin dashboard: KPIs, late surveys, approvals, PDF/Excel export, media, and historical data import.
5. **Plug into the rest of the estate** when you need it: JWT for the SPA, optional SAML2 SSO, optional Active Directory for team login, Hangfire jobs, and API keys for machine-to-machine callers.

It is a **demo / open template**, not a branded corporate product. Seed data is fictional. Secrets are not committed.

---

## Benefits

| Benefit | What you get |
| --- | --- |
| Change forms without shipping a new app | Templates and a shared field catalog drive the UI and validation. |
| One contract for mobile crews | Field-team login, assigned surveys, submit, and uploads stay on a stable `/api/v1/fsms/field-team` surface. |
| Consistent APIs | Every handler returns `Result<T>` (or `Result<PagedResult<T>>`) so errors, validation, and success look the same. |
| Fast feature work | Vertical slices keep a use case (command, validator, handler, DTOs) in one folder instead of scattering it across layers. |
| Safer domain | Entities use factories, private setters, and domain methods instead of anemic public bags of data. |
| Bilingual operations | English and Arabic (RTL) in the admin; labels live in Transloco, not hardcoded templates. |
| Optional enterprise auth | Local JWT out of the box; SAML2 SSO and AD for field teams are config flags, not a rewrite. |
| API keys for inbound feeds | `[RequireApiKey]` consumers can call `POST /surveys/inbound` without a user token. |
| Generated SPA client | Angular HTTP clients and DTOs come from OpenAPI via NSwag — do not hand-edit `api-client.generated.ts`. |

---

## Architecture patterns

MK.FormEngine is **Vertical Slice Architecture with Clean Architecture layering** and **light DDD** in the domain. Slices talk through MediatR, not by referencing each other.

```text
Request  →  Thin controller  →  MediatR pipeline  →  Handler  →  EF Core / domain
                 │                    │
                 │                    ├─ UnhandledExceptionBehaviour
                 │                    ├─ AuthorizationBehaviour
                 │                    ├─ ValidationBehaviour (FluentValidation)
                 │                    ├─ PerformanceBehaviour
                 │                    └─ LoggingBehaviour
                 ▼
            Result<T> HTTP envelope
```

### Vertical Slice Architecture (VSA)

Each feature lives under `src/modules/Application/{Feature}/` as its own **command or query, handler, validator, and response types**.

- Slices communicate via **MediatR**, not direct class references.
- No shared DTOs across slices (duplicate if needed; refactor later).
- Controllers stay thin: dispatch a request and return the `Result<T>` response.

### Clean Architecture layering

| Layer | Project | Role |
| --- | --- | --- |
| Domain | `src/modules/Domain` | Entities, enums, exceptions, domain events. No EF, no HTTP. |
| Application | `src/modules/Application` | Slices, pipeline behaviours, mappings. Depends on Domain + `Shared.Core`. |
| Infrastructure | `src/modules/Infrastructure` | EF Core `ApplicationDbContext`, Identity, seed data, external HTTP/Oracle. |
| Host | `src/WebApps/MK.FormEngine.API` | Controllers, DI wiring, auth, Hangfire, Swagger. |
| Shared | `Shared.Core`, `Shared.Logs`, `Shared.Swagger` | Result type, cache keys, logging, OpenAPI — cross-cutting only. |

Handlers use **`IApplicationDbContext` directly** (no repository layer). That matches current EF Core guidance for simple slices.

### CQRS via MediatR

Writes are **commands**; reads are **queries**. One handler per request. The pipeline is the cross-cutting place for auth, validation, timing, and logging — not the controller.

### Result pattern

Handlers always wrap output in `Result<T>.Success(...)` or `Result<T>.Fail(...)` (`Shared.Core.Common.Result`). Global exception handling maps failures to a stable API shape. Clients never consume a raw entity from a handler.

### Light DDD (domain model)

Entities are not anemic:

- Private / init setters; private parameterless constructor for EF Core.
- **`Create` factory methods** enforce invariants (`DomainException` on invalid state).
- Business behaviour (status changes, calculations) lives on the entity, not in a “domain service” bag.

### FluentValidation

Every command that writes data has a validator, registered from the Application assembly. Messages are explicit (`.WithMessage(...)`). The `ValidationBehaviour` runs before the handler.

### Options + configuration

Settings bind from `appsettings.json` / environment / user-secrets (`JwtSettings`, `CacheSettings`, `SwaggerSetting`, `Sso`, `Hangfire`, and so on). Magic strings in code are wrapped in `static const`.

### Caching

`IDistributedCache` (in-memory or Redis) with explicit TTLs. Keys follow `{prefix}:{entity}:{id}` via `CacheKeys` / `RedisKeysConst`. User-specific data includes the user id in the key.

### AuthN / AuthZ

- **JWT Bearer** for the admin SPA (issuer / audience / lifetime / signing key).
- **Policy-based authorization** plus Identity roles.
- **`[RequireApiKey]`** for machine-to-machine callers (`X-Api-Key`).
- Optional **SAML2 SSO** and **LDAP/AD** for field-team passwords — both off by default.

### Background work

**Hangfire** for background jobs (for example survey version migration and data import). Dashboard is config-gated; credentials must come from secrets, not source.

### API versioning

Routes use `/api/v{version}/...`. Current version is **v1**. Additive changes are preferred over breaking existing clients — especially the field-team mobile contract.

### Frontend patterns

- Angular **standalone** components and **signals**.
- PrimeNG tables with **server-side pagination and filtering**.
- Transloco for **i18n**; add/edit flows use **modals**, not extra routes, where the list page already exists.
- Runtime config from `public/config/app-config.json` (API base URL, language, feature flags) so you can retarget environments without a rebuild.

---

## Solution layout

```text
MK.FormEngine.slnx
src/
  modules/
    Domain/                 Entities, enums, domain events
    Application/            Vertical slices (Commands, Queries, Validators, Behaviours)
    Infrastructure/         EF Core, Identity, seed, integrations
  Shared/
    Shared.Core/            Result, constants, filters, cache keys
    Shared.Logs/            Serilog + optional Elasticsearch
    Shared.Swagger/         OpenAPI / Swagger UI
  WebApps/
    MK.FormEngine.API/      ASP.NET Core host
    MK.FormEngine.Web/      Angular 20 + PrimeNG admin
```

---

## Tech stack

| Concern | Technology |
| --- | --- |
| API | ASP.NET Core (.NET 10) |
| Mediator | MediatR |
| Validation | FluentValidation |
| Database | SQL Server + EF Core |
| Auth | JWT Bearer; optional SAML2 / AD |
| Jobs | Hangfire |
| Cache | In-memory or Redis |
| Logging | Serilog (optional Elasticsearch + Kibana) |
| Admin UI | Angular 20, PrimeNG 20, Tailwind, Transloco, Formly |
| API client | NSwag from OpenAPI |
| Docs | Swagger / Scalar (`SwaggerSetting` in appsettings) |

---

## How to run

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/) and npm
- SQL Server (local instance, LocalDB, or Docker) — TCP port **1433** if you use a hostname
- (Optional) [Docker Desktop](https://www.docker.com/) for Elasticsearch + Kibana

### 1. Clone

```bash
git clone https://github.com/khalifa005/MK.FormEngine.git
cd MK.FormEngine
```

### 2. Configure the API

`appsettings.Development.json` is **gitignored**. Copy the example, then put a real connection string in it (or in user-secrets).

**PowerShell (Windows):**

```powershell
Copy-Item src/WebApps/MK.FormEngine.API/appsettings.Development.json.example `
          src/WebApps/MK.FormEngine.API/appsettings.Development.json
```

**bash / macOS / Linux:**

```bash
cp src/WebApps/MK.FormEngine.API/appsettings.Development.json.example \
   src/WebApps/MK.FormEngine.API/appsettings.Development.json
```

Edit `ConnectionStrings:DefaultConnection` to your SQL Server. Example:

```text
Data Source=tcp:localhost,1433;Initial Catalog=MK_FormEngine_Dev;User ID=sa;Password=YOUR_PASSWORD;Encrypt=False;TrustServerCertificate=True;MultiSubnetFailover=True
```

Prefer user-secrets so the password never sits in a file you might commit:

```bash
cd src/WebApps/MK.FormEngine.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "YOUR_CONNECTION_STRING"
dotnet user-secrets set "JwtSettings:SigningKey" "a-dev-signing-key-at-least-32-characters!"
```

The example file already turns on `DatabaseStartup:ApplyMigrations`, `SeedData`, and `ApplySqlObjects` so the first run creates the schema and demo data.

### 3. Run the API

From the repo root:

```bash
dotnet run --project src/WebApps/MK.FormEngine.API
```

| URL | What |
| --- | --- |
| `https://localhost:7098` | HTTPS (launch profile `https`) |
| `http://localhost:5157` | HTTP |
| `https://localhost:7098/swagger` | Swagger UI (`SwaggerSetting`) |
| `https://localhost:7098/hangfire` | Hangfire dashboard (if enabled; set username/password via secrets) |

CORS already allows `http://localhost:4200` and `https://localhost:4200`.

### 4. Run the Angular admin

In a second terminal:

```bash
cd src/WebApps/MK.FormEngine.Web
npm install
npm start
```

Open [http://localhost:4200](http://localhost:4200).

Runtime API URL is `public/config/app-config.json` → `apiBaseUrl` (default `https://localhost:7098`). Change it if you only run HTTP on port 5157.

**Seeded demo admin (local only):** `administrator@localhost` / `Administrator1!`

Maps on the geolocation picker need a Google Maps JS API key in `src/index.html` (`YOUR_GOOGLE_MAPS_KEY`). Leave it if you do not use maps.

### 5. Optional — Elasticsearch + Kibana

```bash
docker compose up -d
```

Kibana: [http://localhost:5601](http://localhost:5601). Elasticsearch: `http://localhost:9200` (already the default `ElasticConfiguration:Uri`).

---

## Useful commands

```bash
# Build
dotnet build MK.FormEngine.slnx

# Add an EF Core migration (you still apply them; do not generate casually in PRs)
dotnet ef migrations add <Name> --project src/modules/Infrastructure --startup-project src/WebApps/MK.FormEngine.API

# Apply migrations
dotnet ef database update --project src/modules/Infrastructure --startup-project src/WebApps/MK.FormEngine.API
```

Regenerate the Angular client **after** the API is running and the OpenAPI contract changed:

```bash
cd src/WebApps/MK.FormEngine.Web
npm run api:generate
```

Never hand-edit `src/app/core/api/api-client.generated.ts`.

---

## Security notes

- Committed `appsettings.json` uses placeholders such as `SET_VIA_USER_SECRETS_OR_ENV`.
- `appsettings.Development.json` and CertData secrets (`*.pfx`, IdP metadata, password `.txt`) are gitignored.
- Rotate any credential that ever lived in an older copy of this tree before you push a public fork.

---

## License

Open-source demo / template. See [LICENSE](LICENSE) if present; otherwise treat as MIT unless stated otherwise.
