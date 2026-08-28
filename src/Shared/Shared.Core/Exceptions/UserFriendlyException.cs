namespace Shared.Core.Exceptions;

public class UserFriendlyException : Exception
{
    public string? Code { get; }

    public UserFriendlyException(string message, string? code = null) : base(message)
    {
        Code = code;
    }
}
