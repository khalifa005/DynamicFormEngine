using System.Globalization;
using KH.Application.Fsms.Common.Org;
using KH.Domain.Constants.Fsms;

namespace KH.Application.Fsms.Templates.Common;

/// <summary>
/// Whether the caller's territory reaches a single template. The list read filters in bulk; this is
/// the same rule for the by-id reads, which would otherwise stay open to anyone who knows an id.
/// </summary>
public static class TemplateScopeGuard
{
    public const string OutsideTerritoryMessage = "This template is outside your territory.";

    public static async Task<bool> CanSeeAsync(
        IOrgScopeService orgScopeService,
        long templateId,
        CancellationToken cancellationToken)
    {
        var callerScope = await orgScopeService.GetCurrentUserScopeAsync(cancellationToken);

        return await CanSeeAsync(orgScopeService, callerScope, templateId, cancellationToken);
    }

    /// <summary>
    /// Visibility is the "do we share any ground" question, so this is <c>Overlaps</c> rather than
    /// <c>Covers</c>: a template scoped to a whole CBU is still worth showing to someone who works
    /// one operation area inside it. A template with no scope rows is unrestricted, and
    /// <see cref="IOrgScopeService.GetScopeAsync"/> already returns that for an owner with none.
    ///
    /// Takes the caller's coverage rather than resolving it, for handlers that have already read it.
    /// </summary>
    public static async Task<bool> CanSeeAsync(
        IOrgScopeService orgScopeService,
        OrgScopeSet callerScope,
        long templateId,
        CancellationToken cancellationToken)
    {
        if (callerScope.IsUnrestricted)
        {
            return true;
        }

        var templateScope = await orgScopeService.GetScopeAsync(
            OrgScopeOwnerTypes.Template,
            templateId.ToString(CultureInfo.InvariantCulture),
            cancellationToken);

        return callerScope.Overlaps(templateScope);
    }
}
