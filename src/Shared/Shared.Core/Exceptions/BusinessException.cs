namespace Shared.Core.Exceptions;

public class BusinessException : Exception
{
    public string? Code { get; }

    public BusinessException(string message, string? code = null) : base(message)
    {
        Code = code;
    }
}
