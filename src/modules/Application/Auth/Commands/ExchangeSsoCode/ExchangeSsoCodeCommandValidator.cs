namespace KH.Application.Auth.Commands.ExchangeSsoCode;

public sealed class ExchangeSsoCodeCommandValidator : AbstractValidator<ExchangeSsoCodeCommand>
{
    /// <summary>
    /// The code is a base64-encoded 64-byte random value, so it lands at 88 characters. The cap is
    /// generous but bounded, to keep an oversized body from reaching the database lookup.
    /// </summary>
    private const int MaxCodeLength = 256;

    public ExchangeSsoCodeCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(MaxCodeLength).WithMessage($"Code must not exceed {MaxCodeLength} characters.");
    }
}
