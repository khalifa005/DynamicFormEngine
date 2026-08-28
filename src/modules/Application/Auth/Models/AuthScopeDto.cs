namespace KH.Application.Auth.Models;

/// <summary>
/// One <c>ORG_SCOPES</c> row the signed-in caller works under, returned with the token so a mobile
/// client knows its territory without a second call. A field-team login carries the rows belonging
/// to its team; a back-office login carries its own.
/// </summary>
public sealed class AuthScopeDto
{
    /// <summary>The <c>ORG_SCOPES</c> row id.</summary>
    public long ScopeId { get; init; }

    /// <summary>See <c>OrgScopeLevels</c>. Null together with <see cref="Code"/> means every territory.</summary>
    public string? Level { get; init; }

    /// <summary>Code of the org unit at <see cref="Level"/> — a cluster, CBU, branch or operation area.</summary>
    public string? Code { get; init; }

    /// <summary>The territory's own name, resolved from <see cref="Code"/> at <see cref="Level"/>.</summary>
    public string? CodeNameEn { get; init; }

    public string? CodeNameAr { get; init; }

    /// <summary>The department this row is limited to; null means every department.</summary>
    public int? DepartmentId { get; init; }

    public string? DepartmentNameEn { get; init; }
    public string? DepartmentNameAr { get; init; }
}
