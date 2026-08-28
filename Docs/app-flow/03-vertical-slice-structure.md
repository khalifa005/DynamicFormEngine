# 03 — Vertical Slice Structure

> **Sequence:** Feature concept → folder layout → command/query → handler → validator → DTOs  
> **Previous:** [02 — MediatR Pipeline](02-mediatr-pipeline.md)  
> **Next:** [04 — Domain Layer](04-domain-layer.md)

---

## What Is a Vertical Slice?

A **vertical slice** is one complete use case — from API input to database output — grouped in a single folder. Unlike traditional layered architecture where all controllers live together and all services live together, VSA colocates everything a feature needs.

```
Traditional (Horizontal Layers)          NWC (Vertical Slices)
─────────────────────────────           ─────────────────────────
Controllers/                            TodoItems/
  TodoController.cs                       Commands/CreateTodoItem/
Services/                                 Queries/GetTodoItemsWithPagination/
  TodoService.cs                          EventHandlers/
Repositories/                           TodoLists/
  TodoRepository.cs                       Commands/CreateTodoList/
Validators/                               Queries/GetTodos/
  CreateTodoValidator.cs
```

**Benefit for teams:** Change one feature without hunting across five folders. Delete a feature by removing one directory.

---

## Standard Folder Layout

```
src/modules/Application/{FeatureName}/
├── Commands/
│   ├── Create{Feature}/
│   │   ├── Create{Feature}.cs                    ← record + handler (same file)
│   │   └── Create{Feature}CommandValidator.cs
│   ├── Update{Feature}/
│   └── Delete{Feature}/
├── Queries/
│   ├── Get{Feature}s/
│   │   ├── Get{Feature}s.cs                      ← record + handler
│   │   ├── Get{Feature}sQueryValidator.cs        ← optional for queries
│   │   └── {Feature}Dto.cs                       ← response DTO (slice-local)
│   └── Get{Feature}ById/
└── EventHandlers/                                 ← optional
    └── {Feature}CreatedEventHandler.cs
```

---

## Command Pattern

```csharp
// Command record — implements IRequest<Result<T>>
public record CreateTodoItemCommand : IRequest<Result<int>>
{
    public int ListId { get; init; }
    public string? Title { get; init; }
}

// Handler — same file as command (NWC convention)
public class CreateTodoItemCommandHandler : IRequestHandler<CreateTodoItemCommand, Result<int>>
{
    private readonly IApplicationDbContext _context;

    public CreateTodoItemCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<int>> Handle(CreateTodoItemCommand request, CancellationToken cancellationToken)
    {
        var entity = new TodoItem { ListId = request.ListId, Title = request.Title };
        entity.AddDomainEvent(new TodoItemCreatedEvent(entity));

        _context.TodoItems.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(entity.Id);
    }
}
```

### Command Rules

| Rule | Reason |
|------|--------|
| Return `Result<T>` | Consistent API error handling |
| Inject `IApplicationDbContext` | No repository wrapper for simple CRUD |
| Use `record` for commands/queries | Immutability, concise syntax |
| Co-locate handler in same file | Slice stays self-contained |
| Add domain events before SaveChanges | Interceptor dispatches them |

---

## Query Pattern

```csharp
public record GetTodosQuery : IRequest<Result<TodosVm>>;

public class GetTodosQueryHandler : IRequestHandler<GetTodosQuery, Result<TodosVm>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public async Task<Result<TodosVm>> Handle(GetTodosQuery request, CancellationToken cancellationToken)
    {
        var lists = await _context.TodoLists
            .AsNoTracking()
            .ProjectTo<TodoListDto>(_mapper.ConfigurationProvider)
            .ToListAsync(cancellationToken);

        return Result<TodosVm>.Success(new TodosVm { Lists = lists });
    }
}
```

### Query Rules

| Rule | Reason |
|------|--------|
| Use `AsNoTracking()` | Read-only queries should not track entities |
| Use AutoMapper `ProjectTo` | Efficient SQL projection |
| DTOs live in the slice folder | No shared DTOs across slices |
| Paginated queries return `Result<PagedResult<T>>` | Standard pagination wrapper |

---

## Result Pattern

All handlers return `Result<T>` from `Shared.Core`:

```csharp
// Success
return Result<int>.Success(entity.Id);

// Failure with message
return Result<int>.Fail("Todo list not found.", code: "E404001", httpStatusCode: 404);

// Failure with multiple errors
return Result<int>.Fail(new List<ErrorInfo> { ... });
```

Response JSON structure:

```json
{
  "isSuccess": false,
  "data": null,
  "errors": [
    { "code": "E404001", "message": "Todo list not found.", "httpStatusCode": 404 }
  ],
  "correlationId": "f47ac10b-58cc-4372-a567-0e02b2c3d479"
}
```

**Never** return raw `int`, `string`, or entity objects from handlers.

---

## Validator Pattern

```csharp
public class CreateTodoItemCommandValidator : AbstractValidator<CreateTodoItemCommand>
{
    public CreateTodoItemCommandValidator()
    {
        RuleFor(v => v.Title)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("Title is required and must not exceed 200 characters.");

        RuleFor(v => v.ListId)
            .GreaterThan(0)
            .WithMessage("A valid list ID is required.");
    }
}
```

| Rule | Details |
|------|---------|
| One validator per command/query | Named `{RequestName}Validator` |
| Always use `.WithMessage(...)` | No default FluentValidation messages in production |
| Validators auto-register | Via `AddValidatorsFromAssembly` — no manual DI needed |

---

## Slice Isolation Rules

| Allowed | Not Allowed |
|---------|-------------|
| Handler uses `IApplicationDbContext` | Handler references another slice's handler |
| Handler uses domain entities | Handler uses Infrastructure concrete classes |
| Slice has its own DTOs | Shared DTO project across features |
| Event handler reacts to domain events | Direct service call to another feature |

**Cross-feature communication:** Publish a domain event or send a new MediatR command — never inject another slice's handler.

---

## Naming Conventions

| Artifact | Pattern | Example |
|----------|---------|---------|
| Command | `{Verb}{Entity}Command` | `CreateTodoItemCommand` |
| Query | `{Verb}{Entity}Query` | `GetTodosQuery` |
| Handler | `{RequestName}Handler` | `CreateTodoItemCommandHandler` |
| Validator | `{RequestName}Validator` | `CreateTodoItemCommandValidator` |
| DTO | `{Entity}Dto` | `TodoItemBriefDto` |
| View model | `{Feature}Vm` | `TodosVm` |

---

## Real Examples in the Codebase

| Feature | Commands | Queries |
|---------|----------|---------|
| TodoItems | Create, Update, UpdateDetail, Delete | GetTodoItemsWithPagination |
| TodoLists | Create, Update, Delete | GetTodos |

Browse: `src/modules/Application/TodoItems/` and `src/modules/Application/TodoLists/`.

---

## Next Steps

- Domain entities used by handlers: [04-domain-layer.md](04-domain-layer.md)
- Full feature tutorial: [09-adding-a-new-feature.md](09-adding-a-new-feature.md)
