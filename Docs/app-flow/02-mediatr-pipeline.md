# 02 — MediatR Pipeline

> **Sequence:** Request enters pipeline → behaviours run outer-to-inner → handler → behaviours unwind  
> **Previous:** [01 — Request Lifecycle](01-request-lifecycle.md)  
> **Next:** [03 — Vertical Slice Structure](03-vertical-slice-structure.md)

---

## What Is the Pipeline?

MediatR pipeline behaviours are **cross-cutting concerns** that wrap every command and query. They run automatically — you do not call them from handlers.

Registration (`src/modules/Application/DependencyInjection.cs`):

```csharp
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehaviour<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(PerformanceBehaviour<,>));
});
```

---

## Execution Order

Behaviours registered **first** are **outermost** (they wrap everything else).

```
┌─────────────────────────────────────────────────────────┐
│  1. LoggingBehaviour          ← outermost (runs first)  │
│  ┌───────────────────────────────────────────────────┐  │
│  │  2. UnhandledExceptionBehaviour                   │  │
│  │  ┌─────────────────────────────────────────────┐  │  │
│  │  │  3. AuthorizationBehaviour                  │  │  │
│  │  │  ┌───────────────────────────────────────┐  │  │  │
│  │  │  │  4. ValidationBehaviour               │  │  │  │
│  │  │  │  ┌─────────────────────────────────┐  │  │  │  │
│  │  │  │  │  5. PerformanceBehaviour        │  │  │  │  │
│  │  │  │  │  ┌───────────────────────────┐  │  │  │  │  │
│  │  │  │  │  │      HANDLER              │  │  │  │  │  │
│  │  │  │  │  └───────────────────────────┘  │  │  │  │  │
│  │  │  │  └─────────────────────────────────┘  │  │  │  │
│  │  │  └───────────────────────────────────────┘  │  │  │
│  │  └─────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

---

## Behaviour 1: LoggingBehaviour

**File:** `src/modules/Application/Common/Behaviours/LoggingBehaviour.cs`

| What it does | Details |
|--------------|---------|
| Extracts feature name | Strips `Command`/`Query` suffix from request type name |
| Sets log scope | `BeginScope({ "Feature": "CreateTodoItem" })` |
| Logs request | User ID, user name, full request object |

**Why it matters for juniors:** This is how logs automatically route to `logs/CreateTodoItem/Information/`. You never configure Serilog per feature — just name your command/query correctly.

Example: `CreateTodoItemCommand` → feature folder `CreateTodoItem`.

---

## Behaviour 2: UnhandledExceptionBehaviour

**File:** `src/modules/Application/Common/Behaviours/UnhandledExceptionBehaviour.cs`

| What it does | Details |
|--------------|---------|
| Wraps handler in try/catch | Catches any unhandled exception |
| Logs error | Includes request name and serialized request |
| Re-throws | Exception propagates to ASP.NET exception filters |

This ensures every unhandled exception is logged **with the request context** before the global exception handler converts it to a `Result<T>` response.

---

## Behaviour 3: AuthorizationBehaviour

**File:** `src/modules/Application/Common/Behaviours/AuthorizationBehaviour.cs`

| What it does | Details |
|--------------|---------|
| Reads `[Authorize]` on the request class | Custom attribute in `Application/Common/Security/` |
| Checks authentication | `_user.Id` must exist |
| Role check | `[Authorize(Roles = "Administrator")]` |
| Policy check | `[Authorize(Policy = "CanPurge")]` |

**Important:** Authorization is on the **MediatR request**, not only on the controller. You can put `[Authorize]` on the command/query record itself for defense in depth.

Throws:
- `UnauthorizedAccessException` → 401
- `ForbiddenAccessException` → 403

See [07-authentication-authorization.md](07-authentication-authorization.md).

---

## Behaviour 4: ValidationBehaviour

**File:** `src/modules/Application/Common/Behaviours/ValidationBehaviour.cs`

| What it does | Details |
|--------------|---------|
| Finds all `IValidator<TRequest>` | Registered via FluentValidation DI |
| Runs all validators | Parallel via `Task.WhenAll` |
| On failure | Throws `ValidationException` with error list |

**Rule for juniors:** Every command that writes data **must** have a validator class. Example:

```csharp
public class CreateTodoItemCommandValidator : AbstractValidator<CreateTodoItemCommand>
{
    public CreateTodoItemCommandValidator()
    {
        RuleFor(v => v.Title)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Title is required and must not exceed 200 characters.");
    }
}
```

Validators are auto-discovered by `AddValidatorsFromAssembly(...)`.

---

## Behaviour 5: PerformanceBehaviour

**File:** `src/modules/Application/Common/Behaviours/PerformanceBehaviour.cs`

| What it does | Details |
|--------------|---------|
| Starts stopwatch | Before handler runs |
| Stops after handler | Measures elapsed time |
| Warns if slow | Logs warning when request takes **> 500ms** |

Use these warnings to identify N+1 queries, missing indexes, or external API slowness.

---

## How to Add a New Pipeline Behaviour

Only add a new behaviour if the concern applies to **all or most** requests. Feature-specific logic belongs in the handler.

1. Create class implementing `IPipelineBehavior<TRequest, TResponse>` in `Application/Common/Behaviours/`
2. Register in `DependencyInjection.cs` — order matters:
   - **Outer** (runs first): logging, exception handling
   - **Inner** (runs last before handler): performance, caching
3. Build and test with an existing command

---

## Common Mistakes

| Mistake | Fix |
|---------|-----|
| Validation not running | Add validator class; ensure it inherits `AbstractValidator<TRequest>` |
| Auth not enforced | Add `[Authorize]` on command/query, not just controller |
| Logs go to `_global` | Ensure request class name ends with `Command` or `Query` |
| Exception swallowed in handler | Let it bubble — `UnhandledExceptionBehaviour` will log it |

---

## File Reference

| Behaviour | File |
|-----------|------|
| Logging | `Application/Common/Behaviours/LoggingBehaviour.cs` |
| Unhandled Exception | `Application/Common/Behaviours/UnhandledExceptionBehaviour.cs` |
| Authorization | `Application/Common/Behaviours/AuthorizationBehaviour.cs` |
| Validation | `Application/Common/Behaviours/ValidationBehaviour.cs` |
| Performance | `Application/Common/Behaviours/PerformanceBehaviour.cs` |
| Registration | `Application/DependencyInjection.cs` |
