using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Permissions.Models;
using KH.Domain.Constants;
using Shared.Core.Common;

namespace KH.Application.Permissions.Queries.GetRoles;

/// <summary>
/// Every role with the permissions it currently holds — the whole matrix in one call, so the admin
/// screen does not have to fan out one request per role to draw a grid.
/// </summary>
[Authorize(Policy = Policies.CanManageRolePermissions)]
public record GetRolesQuery : IRequest<Result<IReadOnlyList<RoleDto>>>;

public sealed class GetRolesQueryHandler(IApplicationDbContext context, IRoleLookup roleLookup)
    : IRequestHandler<GetRolesQuery, Result<IReadOnlyList<RoleDto>>>
{
    public async Task<Result<IReadOnlyList<RoleDto>>> Handle(
        GetRolesQuery request,
        CancellationToken cancellationToken)
    {
        var roles = await roleLookup.GetRolesAsync(cancellationToken);
        var roleIds = roles.Select(r => r.Id).ToList();

        // One pass over the grants for every role at once. RoleId carries no foreign key, so the
        // roles themselves come from Identity and are stitched to their permissions here.
        var grants = await context.RolePermissions
            .AsNoTracking()
            .Where(rp => roleIds.Contains(rp.RoleId) && rp.Permission.IsActive)
            .Select(rp => new { rp.RoleId, rp.Permission.Code })
            .ToListAsync(cancellationToken);

        var grantsByRole = grants
            .GroupBy(g => g.RoleId, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(x => x.Code).OrderBy(c => c, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);

        var items = roles
            .Select(role =>
            {
                var (nameEn, nameAr) = RoleDisplayNames.For(role.Name);

                return new RoleDto
                {
                    Name = role.Name,
                    NameEn = nameEn,
                    NameAr = nameAr,
                    IsSystem = role.Name == Roles.Administrator,
                    AssignedPermissionCodes = grantsByRole.GetValueOrDefault(role.Id, [])
                };
            })
            // Presented in the order the business thinks about them — super admin first, field crew
            // last — rather than alphabetically.
            .OrderBy(r => RoleDisplayNames.SortOrder(r.Name))
            .ThenBy(r => r.Name, StringComparer.Ordinal)
            .ToList();

        return Result<IReadOnlyList<RoleDto>>.Success(items);
    }
}
