# 08 — Shared Libraries

> **Sequence:** Cross-cutting need → identify Shared project → extend or reuse  
> **Previous:** [07 — Authentication & Authorization](07-authentication-authorization.md)  
> **Next:** [09 — Adding a New Feature](09-adding-a-new-feature.md)

---

## Why Shared Projects Exist

Shared libraries hold code used by **multiple layers** without violating the dependency rule. Feature-specific logic never belongs here.

```
Shared.Core     ← referenced by Domain, Application, Infrastructure
Shared.Logs     ← referenced by NWC.API, Infrastructure
Shared.Swagger  ← referenced by NWC.API
```

---

## Shared.Core

**Path:** `src/Shared/Shared.Core/`  
**References:** None (base library)

### What Lives Here

| Category | Examples | Purpose |
|----------|----------|---------|
| **Result pattern** | `Result<T>`, `ErrorInfo`, `PagedResult<T>` | Standard API responses |
| **Base entities** | `BaseEntity`, `BaseAuditableEntity`, `AuditEntry` | Domain event support, audit fields |
| **Exceptions** | `ApiException`, `UserFriendlyException`, `BusinessException` | Typed error handling |
| **Filters** | `GlobalExceptionHandlingFilter` | Converts exceptions to `Result<T>` JSON |
| **Security** | `AuthorizeAttribute` (Core variant) | Shared auth attributes |
| **Helpers** | `Guard`, extensions | Utility methods |
| **Enums** | `RoleEnum`, `PriorityTypeEnum` | Cross-cutting classifications |
| **Dapper** | `DapperContext` | Raw SQL access when needed |
| **Options** | `HangfireJobsOptions` | Configuration binding classes |

### Result<T> — Most Important Type

```csharp
// src/Shared/Shared.Core/Common/Result.cs
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Data { get; }
    public List<ErrorInfo> Errors { get; }
    public string? CorrelationId { get; }

    public static Result<T> Success(T? data);
    public static Result<T> Fail(string message, string? code = null, ...);
}
```

Every handler and API response flows through this type.

### When to Add to Shared.Core

| Add here | Don't add here |
|----------|----------------|
| Base class used by 2+ layers | Feature-specific DTOs |
| Cross-cutting exception types | Business rules |
| Generic helpers (pagination) | EF configurations |
| Configuration option classes | MediatR handlers |

---

## Shared.Logs

**Path:** `src/Shared/Shared.Logs/`  
**References:** Shared.Core

### What Lives Here

| Component | File | Purpose |
|-----------|------|---------|
| Serilog setup | `Configuration/SerilogConfiguration.cs` | Sinks, enrichers, Map routing |
| DI extensions | `DependencyInjection.cs` | `AddSerilogLogging`, `AddAuditLoggingServices` |
| Correlation | `Middleware/CorrelationMiddleware.cs` | X-Correlation-ID |
| Tenant | `Middleware/TenantLoggingMiddleware.cs` | TenantId enrichment |
| Audit abstractions | `Audit/IAuditService.cs`, `AuditLogAttribute.cs` | Request audit capture |
| Hangfire filter | `Audit/HangfireDashboardAuthorizationFilter.cs` | Secure dashboard |

### Extension Methods Used in Program.cs

```csharp
builder.AddSerilogLogging();           // Register Serilog
builder.AddAuditLoggingServices();     // Hangfire + audit DI
app.UseTenantLogging();                // Tenant middleware
app.UseAuditLogging();                 // Correlation + Hangfire dashboard
```

### When to Modify Shared.Logs

- Adding a new log sink (e.g. Application Insights)
- Changing log file structure
- Adding new enrichment properties (e.g. `ClientId`)
- Configuring audit/Hangfire behavior

**Do not** add Serilog references to Application project.

---

## Shared.Swagger

**Path:** `src/Shared/Shared.Swagger/`  
**References:** ASP.NET Core, Swashbuckle

### What Lives Here

| Component | Purpose |
|-----------|---------|
| `SwaggerSetting` | Bound from config — title, versions, enabled flag |
| `AddSwaggerDocs()` | Registers SwaggerGen with API key security |
| `UseSwaggerDocsMiddleware()` | Serves Swagger UI at root in Development |

### API Key Security Scheme

Documents `X-Api-Key` header for external integrations. All endpoints show the lock icon in Swagger UI.

### Configuration

Swagger settings bind from `Swagger` section in appsettings. Can disable Swagger in production via `Enabled: false`.

---

## Project Reference Graph

```
                    ┌─────────────┐
                    │  NWC.API    │
                    └──────┬──────┘
           ┌───────────────┼───────────────┐
           ▼               ▼               ▼
    ┌────────────┐  ┌────────────┐  ┌────────────────┐
    │Shared.Swagger│ │Shared.Logs │  │ Infrastructure │
    └────────────┘  └──────┬─────┘  └───────┬────────┘
                           │                │
                           ▼                ▼
                    ┌────────────┐   ┌────────────┐
                    │Shared.Core │◄──│ Application│
                    └──────▲─────┘   └──────┬─────┘
                           │                │
                           └────────────────┘
                                    ▲
                                    │
                              ┌─────┴─────┐
                              │  Domain   │
                              └───────────┘
```

---

## Decision Guide: Where Does This Code Go?

```
Is it a use case (command/query/handler)?
  └── Application/{Feature}/

Is it a business entity or rule?
  └── Domain/

Is it EF Core, Identity, or external API?
  └── Infrastructure/

Is it an HTTP controller or startup wiring?
  └── NWC.API/

Is it used by 2+ projects and not feature-specific?
  ├── Response wrapper, base entity, exception → Shared.Core
  ├── Logging, correlation, audit → Shared.Logs
  └── Swagger/OpenAPI → Shared.Swagger
```

---

## Package References Note

Shared.Core includes packages that might seem infrastructure-specific (Dapper, Oracle) for legacy integration paths. New features should prefer EF Core unless there's an explicit requirement for raw SQL/Oracle.

---

## File Quick Reference

| Project | Key Entry Points |
|---------|------------------|
| Shared.Core | `Common/Result.cs`, `Entities/BaseEntity.cs`, `Filters/GlobalExceptionHandlingFilter.cs` |
| Shared.Logs | `DependencyInjection.cs`, `Configuration/SerilogConfiguration.cs` |
| Shared.Swagger | `DependencyInjection.cs`, `SwaggerSetting.cs` |
