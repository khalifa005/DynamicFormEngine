using KH.Application.Fsms.Common.Org;

namespace KH.Application.Fsms.Common.Models;

/// <summary>
/// Shared field-team shape returned by <see cref="Common.Interfaces.ITeamDirectory"/> regardless of
/// whether the source is the local mirror or the live WFM API. Reused by later assignment slices.
/// </summary>
public sealed class TeamDto
{
    public long TeamId { get; init; }
    public string UserCode { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string? Mobile { get; init; }
    public string? TeamType { get; init; }

    public int? ContractorId { get; init; }

    public string? ContractorPoNumber { get; init; }

    public string? ContractorNameEn { get; init; }

    public string? ContractorNameAr { get; init; }

    /// <summary>Retired comma-separated department ids, kept for one release.</summary>
    public IReadOnlyList<string> Departments { get; init; } = [];

    /// <summary>
    /// Read-only roll-up of the departments named across <see cref="Scopes"/>, so the admin grid can
    /// show them in a column without flattening the scope rows itself. Not something a caller sets:
    /// departments are assigned per scope row, because which department a crew works depends on
    /// where — that is the whole reason the two are paired.
    /// </summary>
    public IReadOnlyList<int> DepartmentIds { get; init; } = [];

    /// <summary>
    /// What this crew covers, from <c>ORG_SCOPES</c> — each row a territory, a department, or both.
    /// </summary>
    public IReadOnlyList<OrgScopeAssignment> Scopes { get; init; } = [];

    public bool IsActive { get; init; }

    public string? DeviceName { get; init; }

    public string? DeviceUuid { get; init; }

    public string? AppVersion { get; init; }

    public string? DeviceOs { get; init; }

    public double? LastActiveLatitude { get; init; }

    public double? LastActiveLongitude { get; init; }

    public DateTimeOffset? LastActiveAt { get; init; }
}
