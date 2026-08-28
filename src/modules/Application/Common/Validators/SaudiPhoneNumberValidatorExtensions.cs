using FluentValidation;

namespace KH.Application.Common.Validators;

internal static class SaudiPhoneNumberValidatorExtensions
{
    public static IRuleBuilderOptions<T, string> SaudiMobileNumber<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .Must(SaudiPhoneNumber.IsValid)
            .WithMessage(SaudiPhoneNumber.InvalidFormatMessage);

    public static IRuleBuilderOptions<T, string?> SaudiMobileNumberWhenProvided<T>(
        this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(phone => phone is null || SaudiPhoneNumber.IsValid(phone))
            .WithMessage(SaudiPhoneNumber.InvalidFormatMessage);
}
