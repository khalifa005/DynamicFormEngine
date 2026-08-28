namespace Shared.Logs.Audit;

/// <summary>
/// Interface for resolving the identity and type of the client initiating the current request.
/// </summary>
public interface ICallerResolver
{
    /// <summary>
    /// Resolves the current caller's details: type, user ID, and application name.
    /// </summary>
    /// <returns>A tuple containing CallerType, UserId, and AppName.</returns>
    (string CallerType, string? UserId, string? AppName) ResolveCaller();
}
