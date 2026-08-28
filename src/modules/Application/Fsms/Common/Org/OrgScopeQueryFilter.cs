using System.Linq.Expressions;
using KH.Application.Common.Extensions;
using KH.Domain.Entities.Fsms.Surveys;

namespace KH.Application.Fsms.Common.Org;

/// <summary>
/// Turns an <see cref="OrgScopeSet"/> into a database predicate. Lives beside the scope model rather
/// than inside one read: every query that lists surveys has to answer "may this caller see this row"
/// the same way, and two copies of that rule are two chances for one of them to be the loose one.
/// </summary>
public static class OrgScopeQueryFilter
{
    /// <summary>
    /// The caller's coverage as one <c>WHERE</c> clause: an <c>OR</c> across their scope groups, each
    /// pairing a department with the territory codes it reaches.
    ///
    /// The pairing is why this cannot be two independent filters. ANDing "my departments" against
    /// "my territories" would hand someone who covers water in Riyadh and waste-water in Jeddah the
    /// other two combinations as well — the cross product, not their actual coverage.
    ///
    /// Only meaningful for a restricted scope; check <see cref="OrgScopeSet.IsUnrestricted"/> first,
    /// since an unrestricted set has no groups and would produce a predicate matching nothing.
    /// </summary>
    public static Expression<Func<Survey, bool>> ForSurveys(OrgScopeSet scope)
    {
        var predicate = PredicateBuilder.False<Survey>();

        foreach (var group in scope.Groups)
        {
            var cbuCodes = group.CbuCodes.ToList();
            var branchCodes = group.BranchCodes.ToList();
            var areaCodes = group.OperationAreaCodes.ToList();
            var coversAllTerritory = group.CoversAllTerritory;
            var departmentId = group.DepartmentId;

            // A survey carrying no department is admitted by every group: the field is optional on
            // a survey, and hiding unclassified work would lose it rather than protect it.
            Expression<Func<Survey, bool>> matchesDepartment = departmentId is null
                ? _ => true
                : x => x.DepartmentId == null || x.DepartmentId == departmentId;

            Expression<Func<Survey, bool>> matchesLocation = coversAllTerritory
                ? _ => true
                : x =>
                    (x.CbuCode != null && cbuCodes.Contains(x.CbuCode)) ||
                    (x.BranchCode != null && branchCodes.Contains(x.BranchCode)) ||
                    (x.OperationAreaCode != null && areaCodes.Contains(x.OperationAreaCode));

            predicate = predicate.Or(matchesDepartment.And(matchesLocation));
        }

        return predicate;
    }
}
