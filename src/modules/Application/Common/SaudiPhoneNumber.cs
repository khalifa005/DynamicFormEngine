using System.Text;

namespace KH.Application.Common;

internal static class SaudiPhoneNumber
{
    public const string InvalidFormatMessage =
        "Phone number must be a valid Saudi mobile number (e.g. 05XXXXXXXX or +9665XXXXXXXX).";

    public static bool IsValid(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return false;
        }

        var digits = ExtractDigits(phone);
        return digits.Length switch
        {
            10 when digits.StartsWith("05", StringComparison.Ordinal) => true,
            9 when digits.StartsWith('5') => true,
            12 when digits.StartsWith("9665", StringComparison.Ordinal) => true,
            _ => false
        };
    }

    public static string NormalizeToLocalFormat(string phone)
    {
        var digits = ExtractDigits(phone);

        return digits.Length switch
        {
            12 when digits.StartsWith("9665", StringComparison.Ordinal) => "0" + digits[3..],
            9 when digits.StartsWith('5') => "0" + digits,
            10 when digits.StartsWith("05", StringComparison.Ordinal) => digits,
            _ => phone.Trim()
        };
    }

    private static string ExtractDigits(string phone)
    {
        var builder = new StringBuilder(phone.Length);
        foreach (var character in phone)
        {
            if (char.IsDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
