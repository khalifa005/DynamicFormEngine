# 05 — Infrastructure & Data

> **Sequence:** Handler calls IApplicationDbContext → EF Core → interceptors → SQL Server  
> **Previous:** [04 — Domain Layer](04-domain-layer.md)  
> **Next:** [06 — Logging & Audit](06-logging-and-audit.md)

---

## Purpose

Infrastructure (`src/modules/Infrastructure/`) implements technical concerns:

- EF Core `ApplicationDbContext`
- Entity configurations (Fluent API)
- Database migrations
- ASP.NET Identity
- EF interceptors (audit fields, domain events)
- External service implementations (`IdentityService`, `AuditService`)

Application handlers depend on **`IApplicationDbContext`** (interface in Application). Infrastructure provides the concrete **`ApplicationDbContext`**.

---

## Dependency Injection

**File:** `src/modules/Infrastructure/DependencyInjection.cs`

```csharp
public static void AddInfrastructureServices(this IHostApplicationBuilder builder)
{
    // Connection string
    var connectionString = builder.Configuration.GetConnectionString("KHDb");

    // EF Interceptors
    builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
    builder.Services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventsInterceptor>();

    // DbContext
    builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
    {
        options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
        options.UseSqlServer(connectionString).AddAsyncSeeding(sp);
    });

    // Register interface → implementation
    builder.Services.AddScoped<IApplicationDbContext>(provider =>
        provider.GetRequiredService<ApplicationDbContext>());

    // Identity
    builder.Services.AddDefaultIdentity<ApplicationUser>()
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<ApplicationDbContext>();

    builder.Services.AddTransient<IIdentityService, IdentityService>();
}
```

---

## ApplicationDbContext

**File:** `src/modules/Infrastructure/Data/ApplicationDbContext.cs`

```csharp
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>, IApplicationDbContext
{
    public DbSet<TodoList> TodoLists => Set<TodoList>();
    public DbSet<TodoItem> TodoItems => Set<TodoItem>();
    public DbSet<AuditEntry> AuditLogs => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
```

### IApplicationDbContext Interface

**File:** `src/modules/Application/Common/Interfaces/IApplicationDbContext.cs`

Handlers only see DbSets defined on the interface. When adding a new entity:

1. Add `DbSet<NewEntity>` to **both** interface and context
2. Create EF configuration class
3. Add migration

---

## Entity Configuration (Fluent API)

Configurations live in `Infrastructure/Data/Configurations/`:

```
Configurations/
├── TodoItemConfiguration.cs
├── TodoListConfiguration.cs
└── AuditEntryConfiguration.cs
```

Example pattern:

```csharp
public class TodoItemConfiguration : IEntityTypeConfiguration<TodoItem>
{
    public void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        builder.Property(t => t.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasOne(t => t.List)
            .WithMany(l => l.Items)
            .HasForeignKey(t => t.ListId);
    }
}
```

**Rule:** Do not use data annotations on domain entities for EF mapping — keep configuration in Infrastructure.

---

## EF Core Interceptors

### AuditableEntityInterceptor

Automatically sets audit fields on entities inheriting `BaseAuditableEntity`:

| Field | When Set |
|-------|----------|
| `Created` / `CreatedBy` | Entity added |
| `LastModified` / `LastModifiedBy` | Entity modified |
| `IsActive` | Soft delete support |

Handlers do **not** set these manually.

### DispatchDomainEventsInterceptor

**File:** `src/modules/Infrastructure/Data/Interceptors/DispatchDomainEventsInterceptor.cs`

Before SQL is sent:

1. Finds all tracked `BaseEntity` instances with pending domain events
2. Copies events to a list
3. Clears events on entities
4. Publishes each event via `IMediator.Publish()`
5. Allows `SaveChanges` to proceed

This is why you call `entity.AddDomainEvent(...)` **before** `SaveChangesAsync()`.

---

## Migrations Workflow

```bash
# 1. Change entity or configuration
# 2. Create migration
dotnet ef migrations add AddOrderTable \
  --project src/modules/Infrastructure \
  --startup-project src/WebApps/NWC.API

# 3. Review generated files in Infrastructure/Data/Migrations/

# 4. Apply to database
dotnet ef database update \
  --project src/modules/Infrastructure \
  --startup-project src/WebApps/NWC.API
```

| Rule | Details |
|------|---------|
| Never `EnsureCreated()` in production | Always use migrations |
| Review migration SQL | Check for data loss on renames |
| Connection string key | `ConnectionStrings:KHDb` |
| Dev auto-seed | `InitialiseDatabaseAsync()` in Program.cs (Development only) |

---

## Identity

Infrastructure integrates ASP.NET Identity:

- `ApplicationUser` extends Identity user
- `IIdentityService` wraps user lookups, role checks, policy authorization
- Used by `AuthorizationBehaviour` and `LoggingBehaviour`

Roles defined in `Domain/Constants/Roles.cs`.  
Policies defined in `Domain/Constants/Policies.cs`.

---

## Audit Service

**File:** `src/modules/Infrastructure/Services/AuditService.cs`

Persists `AuditEntry` records to the database via Hangfire background job on the `"audit"` queue:

```csharp
[Queue("audit")]
public async Task SaveAsync(AuditEntry entry)
{
    _dbContext.AuditLogs.Add(entry);
    await _dbContext.SaveChangesAsync(CancellationToken.None);
}
```

See [06-logging-and-audit.md](06-logging-and-audit.md) for the full audit flow.

---

## Data Access Patterns in Handlers

### Preferred (Direct DbContext)

```csharp
_context.TodoItems.Add(entity);
await _context.SaveChangesAsync(cancellationToken);
```

### Read Queries

```csharp
var item = await _context.TodoItems
    .AsNoTracking()
    .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

if (item is null)
    return Result<TodoItemDto>.Fail("Not found.", httpStatusCode: 404);
```

### No Repository Pattern

For simple slices, **do not** create `ITodoItemRepository`. The DbContext is the unit of work. Add repositories only when query complexity justifies it.

---

## Folder Map

```
Infrastructure/
├── Data/
│   ├── ApplicationDbContext.cs
│   ├── ApplicationDbContextInitialiser.cs
│   ├── Configurations/
│   ├── Interceptors/
│   └── Migrations/
├── Identity/
│   ├── ApplicationUser.cs
│   └── IdentityService.cs
├── Services/
│   └── AuditService.cs
└── DependencyInjection.cs
```

---

## Checklist: Adding a New Persisted Entity

1. Create entity in `Domain/Entities/`
2. Add `DbSet<>` to `IApplicationDbContext` and `ApplicationDbContext`
3. Create `IEntityTypeConfiguration<>` in `Data/Configurations/`
4. Run `dotnet ef migrations add ...`
5. Run `dotnet ef database update ...`
6. Build handlers that use the new DbSet
