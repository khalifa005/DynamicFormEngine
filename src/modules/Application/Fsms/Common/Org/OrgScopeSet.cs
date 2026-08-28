using KH.Domain.Constants.Fsms;

namespace KH.Application.Fsms.Common.Org;

/// <summary>
/// One row a caller wants to assign — a place, a kind of work, or both. A null
/// <see cref="Level"/>/<see cref="Code"/> means every territory; a null
/// <see cref="DepartmentId"/> means every department.
/// </summary>
public sealed record OrgScopeAssignment
{
    public string? Level { get; init; }
    public string? Code { get; init; }
    public int? DepartmentId { get; init; }
}

/// <summary>
/// One department's worth of an owner's coverage, with every territory code its rows reach once
/// each has been expanded downwards. Held per department rather than as one flat set because the
/// two axes are not independent: an owner working water in Riyadh and waste-water in Jeddah must
/// not thereby be given water in Jeddah.
/// </summary>
internal sealed record OrgScopeGroup(int? DepartmentId)
{
    /// <summary>Set by a department-only row: that department, in every territory.</summary>
    public bool CoversAllTerritory { get; set; }

    public HashSet<string> ClusterCodes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> CbuCodes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> BranchCodes { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> OperationAreaCodes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether this group's department admits work classified as <paramref name="departmentId"/>.</summary>
    public bool AcceptsDepartment(int? departmentId) =>
        // A group with no department takes any work; a survey with no department is taken by any
        // group, because the department is optional on a survey and hiding unclassified work would
        // lose it entirely.
        DepartmentId is null || departmentId is null || DepartmentId == departmentId;

    public bool AcceptsLocation(string? cbuCode, string? branchCode, string? operationAreaCode) =>
        CoversAllTerritory
        || (cbuCode is not null && CbuCodes.Contains(cbuCode))
        || (branchCode is not null && BranchCodes.Contains(branchCode))
        || (operationAreaCode is not null && OperationAreaCodes.Contains(operationAreaCode));

    public bool OverlapsLocation(OrgScopeGroup other) =>
        CoversAllTerritory
        || other.CoversAllTerritory
        || CbuCodes.Overlaps(other.CbuCodes)
        || BranchCodes.Overlaps(other.BranchCodes)
        || OperationAreaCodes.Overlaps(other.OperationAreaCodes)
        || ClusterCodes.Overlaps(other.ClusterCodes);
}

/// <summary>
/// One department's coverage flattened for callers that must filter in the database rather than in
/// memory. <see cref="OrgScopeSet.Covers"/> answers the same question for a row already loaded; this
/// hands the code lists to a query so the rows never have to be.
/// </summary>
public sealed record OrgScopeTerritory(
    int? DepartmentId,
    bool CoversAllTerritory,
    IReadOnlyList<string> CbuCodes,
    IReadOnlyList<string> BranchCodes,
    IReadOnlyList<string> OperationAreaCodes);

/// <summary>
/// The expanded coverage an owner (team, user or template) holds — its scope rows grouped by
/// department, each carrying every cluster, CBU, branch and operation area code its rows reach.
///
/// An owner with no scope rows at all is <see cref="IsUnrestricted"/> rather than "covers nothing":
/// coverage can then be rolled out gradually, and nobody's worklist goes blank the moment
/// enforcement goes live.
/// </summary>
public sealed class OrgScopeSet
{
    private readonly List<OrgScopeGroup> _groups;

    private OrgScopeSet(bool isUnrestricted, List<OrgScopeGroup> groups)
    {
        IsUnrestricted = isUnrestricted;
        _groups = groups;
    }

    public bool IsUnrestricted { get; }

    internal IReadOnlyList<OrgScopeGroup> Groups => _groups;

    public static OrgScopeSet Unrestricted() => new(true, []);

    public static OrgScopeSet Empty() => new(false, []);

    public static OrgScopeSet FromAssignments(
        IEnumerable<OrgScopeAssignment> assignments,
        OrgHierarchyMaps hierarchy)
    {
        var rows = assignments.ToList();

        if (rows.Count == 0)
        {
            return Unrestricted();
        }

        var groups = new Dictionary<int, OrgScopeGroup>();

        // -1 stands in for "no department" so one dictionary can key both cases; real department
        // ids are positive.
        const int NoDepartment = -1;

        foreach (var row in rows)
        {
            var key = row.DepartmentId ?? NoDepartment;

            if (!groups.TryGetValue(key, out var group))
            {
                group = new OrgScopeGroup(row.DepartmentId);
                groups[key] = group;
            }

            if (row.Level is null || row.Code is null)
            {
                // A department-only row: that kind of work, wherever it is.
                group.CoversAllTerritory = true;
                continue;
            }

            Expand(group, row.Level, row.Code, hierarchy);
        }

        return new OrgScopeSet(false, [.. groups.Values]);
    }

    /// <summary>
    /// True when this coverage reaches the given work. Both axes must be satisfied by the
    /// <b>same</b> row group — that is the whole point of grouping by department. On the location
    /// axis a survey only needs to be reachable through one of CBU, branch or operation area, not all
    /// three, since a survey is not obliged to carry every level.
    /// </summary>
    public bool Covers(string? cbuCode, string? branchCode, string? operationAreaCode, int? departmentId)
    {
        if (IsUnrestricted)
        {
            return true;
        }

        return _groups.Any(group =>
            group.AcceptsDepartment(departmentId)
            && group.AcceptsLocation(cbuCode, branchCode, operationAreaCode));
    }

    /// <summary>
    /// True when this coverage and <paramref name="other"/> share any work at all — used to decide
    /// whether a caller may see an owner (e.g. a team) rather than a single survey. Either side
    /// being unrestricted, or holding no rows, counts as overlapping, matching <see cref="Covers"/>.
    /// </summary>
    public bool Overlaps(OrgScopeSet other)
    {
        if (IsUnrestricted || other.IsUnrestricted)
        {
            return true;
        }

        return _groups.Any(group => other._groups.Any(otherGroup =>
            group.AcceptsDepartment(otherGroup.DepartmentId) && group.OverlapsLocation(otherGroup)));
    }

    /// <summary>
    /// The same coverage, expressed as plain code lists one per department group — what a query
    /// needs to narrow a worklist to this scope without materializing every candidate row first.
    /// Cluster codes are deliberately left out: a survey is stamped with CBU, branch and operation
    /// area only, and a cluster scope has already been expanded into the CBUs beneath it.
    /// </summary>
    public IReadOnlyList<OrgScopeTerritory> ToTerritories() =>
        [.. _groups.Select(group => new OrgScopeTerritory(
            group.DepartmentId,
            group.CoversAllTerritory,
            [.. group.CbuCodes],
            [.. group.BranchCodes],
            [.. group.OperationAreaCodes]))];

    /// <summary>Adds a code and everything beneath it to the group.</summary>
    private static void Expand(OrgScopeGroup group, string level, string code, OrgHierarchyMaps hierarchy)
    {
        switch (level)
        {
            case OrgScopeLevels.Cluster:
                group.ClusterCodes.Add(code);
                foreach (var cbu in hierarchy.CbusUnderCluster(code))
                {
                    group.CbuCodes.Add(cbu);
                    ExpandCbu(group, cbu, hierarchy);
                }

                break;

            case OrgScopeLevels.Cbu:
                group.CbuCodes.Add(code);
                ExpandCbu(group, code, hierarchy);
                break;

            // Nothing hangs below a branch. Operation areas are its siblings under the CBU, not its
            // children, so a branch scope reaches branch-stamped work and no further.
            case OrgScopeLevels.Branch:
                group.BranchCodes.Add(code);
                break;

            case OrgScopeLevels.OperationArea:
                group.OperationAreaCodes.Add(code);
                break;
        }
    }

    /// <summary>Adds both leaf levels beneath a CBU — they are parallel, not nested.</summary>
    private static void ExpandCbu(OrgScopeGroup group, string cbuCode, OrgHierarchyMaps hierarchy)
    {
        foreach (var branch in hierarchy.BranchesUnderCbu(cbuCode))
        {
            group.BranchCodes.Add(branch);
        }

        foreach (var area in hierarchy.AreasUnderCbu(cbuCode))
        {
            group.OperationAreaCodes.Add(area);
        }
    }
}
