namespace Shared.Core.Exceptions;

/// <summary>
/// Thrown when a domain invariant or business rule is violated inside an entity or domain service.
/// Mapped to <see cref="Common.Result{T}"/> by <see cref="Filters.GlobalExceptionHandlingFilter"/>.
/// </summary>
public sealed class DomainException : Exception
{
    public string? Code { get; }

    public DomainException(string message, string? code = null) : base(message)
    {
        Code = code;
    }
}
