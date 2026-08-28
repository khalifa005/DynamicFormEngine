---
paths:
  - "src/modules/Domain/**/*.cs"
description: Light DDD entity design rules for NWC Domain layer.
---

# Domain Entity Rules

- Avoid anemic models — expose behavior through domain methods, not public setters.
- Keep state properties `private` or `init`-only.
- Provide a private parameterless constructor for EF Core.
- Use static `Create` factory methods as the only way to construct entities.
- Throw `DomainException` for invalid states — never allow invalid entities to exist.
- Domain events belong on the entity; raise via `AddDomainEvent(...)`.

## Example Pattern

```csharp
public sealed class Movie : Entity
{
    private Movie() { }

    public static Movie Create(string title, ...) { /* validate invariants */ }
    public void UpdateDetails(...) { /* validate + mutate */ }
}
```
