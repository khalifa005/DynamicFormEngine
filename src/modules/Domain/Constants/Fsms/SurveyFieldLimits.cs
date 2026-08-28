namespace KH.Domain.Constants.Fsms;

/// <summary>Max lengths for first-class survey columns that are not already named elsewhere.</summary>
public abstract class SurveyFieldLimits
{
    public const int CustomerName = 250;

    /// <summary>
    /// Generous for a phone number, deliberately: the originating systems send it unformatted, with
    /// or without a country code, and occasionally more than one separated by a slash. Truncating a
    /// crew's only way of reaching the customer is worse than storing a messy string.
    /// </summary>
    public const int CustomerPhone = 50;

    public const int MeterNumber = 100;
    public const int Hcn = 50;

    /// <summary>Matches the <c>ReturnReason</c> ceiling — both are operator-length free text.</summary>
    public const int SourceComment = 1000;

    /// <summary>Same width as the org codes it is the raw, unresolved form of.</summary>
    public const int MaintenanceAreaCode = 50;
}
