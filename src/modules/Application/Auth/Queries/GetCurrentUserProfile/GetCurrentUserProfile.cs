using System.Globalization;
using KH.Application.Common.Interfaces;
using KH.Application.Fsms.Common.Lookups;
using KH.Application.Fsms.Common.Org;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Options;

namespace KH.Application.Auth.Queries.GetCurrentUserProfile;

/// <summary>
/// The caller's own identity, roles/permissions and resolved territory — the "who am I" a client
/// asks once after signing in, whether it is a back-office session or a field-team one.
/// </summary>
public record GetCurrentUserProfileQuery : IRequest<Result<CurrentUserProfileDto>>;

public sealed class CurrentUserProfileDto
{
    public string UserId { get; init; } = default!;
    public string? UserName { get; init; }
    public string? Email { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>The <c>TEAMS</c> row id behind this login, when it is a field-team account.</summary>
    public long? FieldTeamId { get; init; }

    public bool IsUnrestrictedScope { get; init; }

    /// <summary>
    /// Territory from <c>ORG_SCOPES</c>: team rows when <see cref="FieldTeamId"/> is set, otherwise
    /// the caller's own user rows.
    /// </summary>
    public IReadOnlyList<CurrentUserScopeDto> Scopes { get; init; } = [];
}

public sealed class CurrentUserScopeDto
{
    /// <summary>The <c>ORG_SCOPES</c> row id.</summary>
    public long ScopeId { get; init; }

    public string? Level { get; init; }
    public string? Code { get; init; }

    /// <summary>The territory's own name, resolved from <see cref="Code"/> at <see cref="Level"/>.</summary>
    public string? CodeNameEn { get; init; }
    public string? CodeNameAr { get; init; }

    public int? DepartmentId { get; init; }
    public string? DepartmentNameEn { get; init; }
    public string? DepartmentNameAr { get; init; }
}

public sealed class GetCurrentUserProfileQueryHandler(
    IUser user,
    IOrgScopeService orgScopeService,
    IApplicationDbContext context,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetCurrentUserProfileQuery, Result<CurrentUserProfileDto>>
{
    public async Task<Result<CurrentUserProfileDto>> Handle(
        GetCurrentUserProfileQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.Id))
        {
            return Result<CurrentUserProfileDto>.Fail(
                "Not authenticated.", ApiErrorCodes.UnauthorizedAccess, httpStatusCode: 401);
        }

        var scope = await orgScopeService.GetCurrentUserScopeAsync(cancellationToken);

        // Field-team accounts carry scope on the Team row; back-office accounts on the User row.
        string ownerType;
        string ownerId;
        if (user.FieldTeamId is long fieldTeamId)
        {
            ownerType = OrgScopeOwnerTypes.Team;
            ownerId = fieldTeamId.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            ownerType = OrgScopeOwnerTypes.User;
            ownerId = user.Id;
        }

        var scopeRows = await context.OrgScopes
            .AsNoTracking()
            .Where(x => x.OwnerType == ownerType && x.OwnerId == ownerId && x.IsActive)
            .Select(x => new { x.Id, x.Level, x.Code, x.DepartmentId })
            .ToListAsync(cancellationToken);

        var departmentNames = await LookupNameCache.GetDepartmentNamesAsync(
            context, cache, cacheSettings.Value, cancellationToken);
        var cbuNames = await LookupNameCache.GetCbuNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var branchNames = await LookupNameCache.GetBranchNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var operationAreaNames = await LookupNameCache.GetOperationAreaNamesAsync(
            context, cache, cacheSettings.Value, cancellationToken);

        var scopes = scopeRows
            .Select(x =>
            {
                var department = departmentNames.FindDepartment(x.DepartmentId);

                // The code's own name comes from whichever cache matches the row's level — a scope
                // row is only ever one level, so only one of these ever has a hit.
                var codeName = x.Level switch
                {
                    OrgScopeLevels.Cbu => cbuNames.FindCbu(x.Code),
                    OrgScopeLevels.Branch => branchNames.FindBranch(x.Code),
                    OrgScopeLevels.OperationArea => operationAreaNames.FindOperationArea(x.Code),
                    _ => null,
                };

                return new CurrentUserScopeDto
                {
                    ScopeId = x.Id,
                    Level = x.Level,
                    Code = x.Code,
                    CodeNameEn = codeName?.NameEn,
                    CodeNameAr = codeName?.NameAr,
                    DepartmentId = x.DepartmentId,
                    DepartmentNameEn = department?.NameEn,
                    DepartmentNameAr = department?.NameAr,
                };
            })
            .ToList();

        return Result<CurrentUserProfileDto>.Success(new CurrentUserProfileDto
        {
            UserId = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            Roles = user.Roles,
            Permissions = user.Permissions,
            FieldTeamId = user.FieldTeamId,
            IsUnrestrictedScope = scope.IsUnrestricted,
            Scopes = scopes,
        });
    }
}
