# 10 — API Layer

> **Sequence:** Program.cs startup → middleware pipeline → controllers → HTTP response  
> **Previous:** [09 — Adding a New Feature](09-adding-a-new-feature.md)  
> **Back to:** [App Architecture Guide](../App-Architecture-Guide.md)

---

## Purpose

The API layer (`src/WebApps/NWC.API/`) is the **HTTP boundary**. It:

- Starts the ASP.NET Core host
- Registers all DI services from other projects
- Configures middleware pipeline
- Exposes REST endpoints via controllers
- Returns JSON `Result<T>` responses

**Golden rule:** Controllers are thin dispatchers. No business logic, no DbContext, no validation.

---

## Project Structure

```
NWC.API/
├── Program.cs                 ← Host entry point (minimal)
├── DependencyInjection.cs     ← Web-specific DI (IUser, CurrentUser)
├── Controllers/
│   ├── ApiControllerBase.cs   ← Base class with Mediator accessor
│   ├── TodoListsController.cs
│   ├── TodoItemsController.cs
│   └── *TestController.cs     ← Logging/audit/api-key test endpoints
├── Services/
│   └── CurrentUser.cs         ← IUser implementation
├── appsettings.json
├── appsettings.Development.json
└── Properties/
    └── launchSettings.json
```

---

## Program.cs — Startup Sequence

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Logging & audit infrastructure
builder.AddSerilogLogging();
builder.AddAuditLoggingServices();

// 2. Application layers
builder.AddApplicationServices();      // MediatR, validators, AutoMapper
builder.AddInfrastructureServices();   // EF Core, Identity
builder.AddWebServices();              // IUser

// 3. API services
builder.Services.AddControllers();
builder.Services.AddSwaggerDocs();

var app = builder.Build();

// 4. Middleware pipeline (order matters!)
app.UseTenantLogging();
app.UseAuditLogging();                 // Includes CorrelationMiddleware

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerDocsMiddleware();
    await app.InitialiseDatabaseAsync();  // Seed dev data
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### Middleware Order Explained

| Order | Middleware | Why This Position |
|-------|------------|-------------------|
| 1 | TenantLogging | Enriches all downstream logs |
| 2 | Correlation (in UseAuditLogging) | Available before controllers |
| 3 | Swagger (dev) | Before auth for UI access |
| 4 | HTTPS redirection | Security |
| 5 | Authorization | Before endpoint execution |
| 6 | Controllers | Handle requests |

---

## ApiControllerBase

```csharp
// Controllers/ApiControllerBase.cs
[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _mediator;
    protected ISender Mediator =>
        _mediator ??= HttpContext.RequestServices.GetRequiredService<ISender>();
}
```

All controllers inherit this to get lazy `Mediator` access without constructor injection.

**Route convention:** `ProductsController` → `/api/Products`

For API versioning (future): prefix routes with `/api/v{version}/...` per project conventions.

---

## Controller Pattern

```csharp
[Authorize]
public class TodoListsController : ApiControllerBase
{
    // GET /api/TodoLists
    [HttpGet]
    public async Task<ActionResult<Result<TodosVm>>> Get()
    {
        var result = await Mediator.Send(new GetTodosQuery());
        if (!result.IsSuccess)
            return BadRequest(result);
        return Ok(result);
    }

    // POST /api/TodoLists
    [HttpPost]
    public async Task<ActionResult<Result<int>>> Create(CreateTodoListCommand command)
    {
        var result = await Mediator.Send(command);
        if (!result.IsSuccess)
            return BadRequest(result);
        return Ok(result);
    }

    // PUT /api/TodoLists/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, UpdateTodoListCommand command)
    {
        if (id != command.Id)
            return BadRequest();

        var result = await Mediator.Send(command);
        if (!result.IsSuccess)
            return BadRequest(result);

        return NoContent();
    }

    // DELETE /api/TodoLists/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await Mediator.Send(new DeleteTodoListCommand(id));
        if (!result.IsSuccess)
            return BadRequest(result);

        return NoContent();
    }
}
```

### HTTP Status Mapping Convention

| Scenario | Status Code |
|----------|-------------|
| Query success | `200 OK` + `Result<T>` body |
| Command success (returns ID) | `200 OK` + `Result<int>` body |
| Update/delete success | `204 NoContent` |
| Business/validation failure | `400 BadRequest` + `Result<T>` with errors |
| Route/body ID mismatch | `400 BadRequest` (empty) |
| Unhandled exception | Filter maps to 4xx/5xx + `Result<T>` |

---

## Model Binding

ASP.NET Core binds JSON body to command records automatically:

```json
POST /api/TodoLists
{
  "title": "Shopping List"
}
```

Binds to `CreateTodoListCommand { Title = "Shopping List" }`.

Route parameters bind to command properties when using records with positional parameters:

```csharp
// DeleteTodoListCommand(int Id) — id from route
await Mediator.Send(new DeleteTodoListCommand(id));
```

---

## Swagger / OpenAPI

Configured via `Shared.Swagger`:

- Swagger UI served at root (`/`) in Development when enabled
- API key auth documented (`X-Api-Key` header)
- Multiple API versions supported via config

Test endpoints in Development:
- `LoggingTestController` — verify feature log routing
- `AuditTestController` — verify audit persistence
- `ApiKeyTestController` — verify API key auth

---

## CurrentUser Service

```csharp
// NWC.API/Services/CurrentUser.cs
public class CurrentUser : IUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public string? Id => _httpContextAccessor.HttpContext?.User
        ?.FindFirstValue(ClaimTypes.NameIdentifier);
}
```

Bridges HTTP context to Application layer without handlers referencing ASP.NET types.

---

## Configuration Files

### appsettings.json (base)

Connection strings, JWT settings, Swagger config, Hangfire options.

### appsettings.Development.json

```json
{
  "Serilog": { "MinimumLevel": { "Default": "Information" } },
  "ElasticConfiguration": { "Uri": "http://localhost:9200" }
}
```

**Secrets:** Use `dotnet user-secrets` for JWT signing keys and production connection strings.

```bash
dotnet user-secrets set "JwtSettings:SigningKey" "your-secret-key" \
  --project src/WebApps/NWC.API
```

---

## Global Exception Handling

Exceptions thrown from MediatR pipeline or handlers are caught by `GlobalExceptionHandlingFilter` in Shared.Core and converted to `Result<T>` JSON:

| Exception | Status |
|-----------|--------|
| FluentValidation `ValidationException` | 400 |
| `UnauthorizedAccessException` | 401 |
| `ForbiddenAccessException` | 403 |
| `EntityNotFoundException` | 404 |
| Unhandled | 500 (generic message in production) |

Controllers typically don't need try/catch — the filter handles it.

---

## Adding a New Controller — Checklist

- [ ] Create `{Feature}Controller.cs` in `Controllers/`
- [ ] Extend `ApiControllerBase`
- [ ] Add `[Authorize]` at class or action level
- [ ] One action per use case — calls `Mediator.Send`
- [ ] Map `Result<T>.IsSuccess` to appropriate HTTP status
- [ ] Add `[ProducesResponseType]` for OpenAPI clarity (optional but good)
- [ ] Test in Swagger

---

## What NOT to Put in NWC.API

| Don't | Do Instead |
|-------|------------|
| Business logic | Application handler |
| EF Core queries | Application handler via `IApplicationDbContext` |
| FluentValidation rules | Validator in Application slice |
| Serilog configuration | Shared.Logs |
| Entity definitions | Domain project |
| Large DI registration blocks | `DependencyInjection.cs` per project |

---

## Running Locally

```bash
dotnet run --project src/WebApps/NWC.API
```

Default URLs in `launchSettings.json` (typically `https://localhost:7xxx`).

In Development:
1. Swagger UI opens automatically
2. Database is seeded via `InitialiseDatabaseAsync()`
3. Logs write to `./logs/` folder
4. Hangfire dashboard at `/hangfire` (if enabled)

---

## End of the Flow Series

You now have the complete picture:

1. [Request Lifecycle](01-request-lifecycle.md) — HTTP to response
2. [MediatR Pipeline](02-mediatr-pipeline.md) — cross-cutting behaviours
3. [Vertical Slices](03-vertical-slice-structure.md) — feature organization
4. [Domain Layer](04-domain-layer.md) — entities and events
5. [Infrastructure](05-infrastructure-data.md) — EF Core and migrations
6. [Logging & Audit](06-logging-and-audit.md) — Serilog and Hangfire
7. [Auth](07-authentication-authorization.md) — JWT and policies
8. [Shared Libraries](08-shared-libraries.md) — cross-cutting projects
9. [Adding Features](09-adding-a-new-feature.md) — hands-on tutorial
10. **API Layer** (this document)

Return to the [App Architecture Guide](../App-Architecture-Guide.md) for the overview and quick reference.
