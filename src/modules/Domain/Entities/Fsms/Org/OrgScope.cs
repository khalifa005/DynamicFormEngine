using KH.Domain.Constants.Fsms;

namespace KH.Domain.Entities.Fsms.Org;

/// <summary>
/// One slice of coverage a team, user or template holds — a place, a kind of work, or both. Read as
/// a sentence: <c>(Cbu, "RCBU", 10)</c> is "water network, in RCBU". Table: <c>ORG_SCOPES</c>.
///
/// Each part is optional, and absent means all of it:
/// <list type="bullet">
/// <item>no <see cref="DepartmentId"/> — every department in that territory;</item>
/// <item>no <see cref="Level"/>/<see cref="Code"/> — that department everywhere;</item>
/// <item>neither — rejected, because an owner covering everything is expressed by holding no rows
/// at all, and a blank row would quietly read the same way while looking deliberate.</item>
/// </list>
///
/// The department lives on the row rather than in a list beside it because the two are not
/// independent: an owner working water in Riyadh and waste-water in Jeddah is not the same as one
/// working both departments in both cities, and separate lists can only say the latter.
///
/// A territory row still covers everything beneath it — <c>(Cluster, "CC")</c> reaches every CBU,
/// branch and operation area in the Central cluster — so the common case stays one row.
///
/// <see cref="OwnerId"/> is a string because it has to hold both a <c>FieldTeam.Id</c> (long) and
/// an Identity user id (GUID). One column that holds either beats two nullable ones that each hold
/// half the rows.
/// </summary>
public sealed class OrgScope : BaseAuditableEntity<long>
{
    private OrgScope()
    {
    }

    private OrgScope(string ownerType, string ownerId, string? level, string? code, int? departmentId)
    {
        OwnerType = ownerType;
        OwnerId = ownerId;
        Level = level;
        Code = code;
        DepartmentId = departmentId;
        IsActive = true;
    }

    /// <summary>See <see cref="OrgScopeOwnerTypes"/>.</summary>
    public string OwnerType { get; private set; } = default!;

    /// <summary>Team id, Identity user id or template id, depending on <see cref="OwnerType"/>.</summary>
    public string OwnerId { get; private set; } = default!;

    /// <summary>See <see cref="OrgScopeLevels"/>. Null together with <see cref="Code"/> means every territory.</summary>
    public string? Level { get; private set; }

    /// <summary>Code of the org unit at <see cref="Level"/> — a cluster, CBU, branch or operation area code.</summary>
    public string? Code { get; private set; }

    /// <summary>The department this row is limited to; null means every department.</summary>
    public int? DepartmentId { get; private set; }

    /// <summary>True when the row names a place. False means it applies everywhere.</summary>
    public bool HasTerritory => Level is not null && Code is not null;

    /// <summary>True when the row names only a kind of work, with no place attached.</summary>
    public bool IsDepartmentOnly => !HasTerritory && DepartmentId is not null;

    public static OrgScope Create(string ownerType, string ownerId, string? level, string? code, int? departmentId)
    {
        if (!OrgScopeOwnerTypes.IsDefined(ownerType))
        {
            throw new DomainException($"Unknown scope owner type '{ownerType}'.");
        }

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            throw new DomainException("A scope must belong to an owner.");
        }

        var normalizedLevel = string.IsNullOrWhiteSpace(level) ? null : level.Trim();
        var normalizedCode = string.IsNullOrWhiteSpace(code) ? null : code.Trim();

        // Half a territory is not a narrower scope, it is an unanswerable one: a level with no code
        // names no unit, and a code with no level cannot be looked up, since codes are only unique
        // within a level.
        if (normalizedLevel is null != normalizedCode is null)
        {
            throw new DomainException("A scope must name both a level and a code, or neither.");
        }

        if (normalizedLevel is not null && !OrgScopeLevels.IsDefined(normalizedLevel))
        {
            throw new DomainException($"Unknown scope level '{normalizedLevel}'.");
        }

        if (normalizedLevel is null && departmentId is null)
        {
            throw new DomainException(
                "A scope must name a territory, a department, or both. An owner that covers everything holds no scopes at all.");
        }

        return new OrgScope(ownerType, ownerId.Trim(), normalizedLevel, normalizedCode, departmentId);
    }
}
