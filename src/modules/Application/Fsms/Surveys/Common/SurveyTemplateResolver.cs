using KH.Application.Common.Interfaces;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Surveys.Common;

/// <summary>
/// Picks the template a new survey is raised against. The originating system knows the kind of
/// asset it raised work for, not which form the back office models it with, so the FA type is the
/// selector: exactly one published template answers for each type. An explicit template id or code
/// still wins when a caller sends one, which is what keeps the older inbound contract working.
/// </summary>
internal static class SurveyTemplateResolver
{
    /// <summary>The template a survey will be pinned to, and the version to pin it at.</summary>
    internal sealed record ResolvedTemplate(
        long TemplateId,
        int? CurrentVersionNo,
        int TeamFillSlaHours,
        int CompletionSlaHours);

    public static async Task<Result<ResolvedTemplate>> ResolveAsync(
        IApplicationDbContext context,
        long? templateId,
        string? templateCode,
        string? faTypeCode,
        CancellationToken cancellationToken)
    {
        var code = string.IsNullOrWhiteSpace(templateCode) ? null : templateCode.Trim();

        if (templateId is > 0 || code is not null)
        {
            return await ResolveByIdentityAsync(context, templateId, code, cancellationToken);
        }

        var faType = string.IsNullOrWhiteSpace(faTypeCode) ? null : faTypeCode.Trim();

        if (faType is null)
        {
            return Result<ResolvedTemplate>.Fail(
                "An FA type code is required to resolve the template.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400);
        }

        // Two matches is a configuration mistake, not a survey the system can place: picking either
        // one would silently send the crew a form the back office did not choose. Reading two rows
        // is what tells them apart, so the take is deliberate.
        var matches = await context.SurveyTemplates
            .AsNoTracking()
            .Where(x => x.FaTypeCode == faType && x.Status == TemplateStatuses.Published)
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.CurrentVersionNo, x.TeamFillSlaHours, x.CompletionSlaHours })
            .Take(2)
            .ToListAsync(cancellationToken);

        if (matches.Count == 0)
        {
            return Result<ResolvedTemplate>.Fail(
                $"No published template is configured for FA type '{faType}'.",
                ApiErrorCodes.NotFound,
                httpStatusCode: 404);
        }

        if (matches.Count > 1)
        {
            return Result<ResolvedTemplate>.Fail(
                $"More than one published template is configured for FA type '{faType}'.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        return Result<ResolvedTemplate>.Success(new ResolvedTemplate(
            matches[0].Id,
            matches[0].CurrentVersionNo,
            matches[0].TeamFillSlaHours,
            matches[0].CompletionSlaHours));
    }

    /// <summary>The version snapshot to pin the survey to, or null when the template has none.</summary>
    public static Task<long?> ResolveVersionIdAsync(
        IApplicationDbContext context,
        ResolvedTemplate template,
        CancellationToken cancellationToken) =>
        context.TemplateVersions
            .AsNoTracking()
            .Where(v => v.TemplateId == template.TemplateId
                && v.VersionNo == template.CurrentVersionNo
                && v.TargetClient == TargetClients.Formly)
            .Select(v => (long?)v.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static async Task<Result<ResolvedTemplate>> ResolveByIdentityAsync(
        IApplicationDbContext context,
        long? templateId,
        string? templateCode,
        CancellationToken cancellationToken)
    {
        var template = await context.SurveyTemplates
            .AsNoTracking()
            .Where(x => templateId != null && x.Id == templateId
                || templateCode != null && x.TemplateCode == templateCode)
            .Select(x => new { x.Id, x.Status, x.CurrentVersionNo, x.TeamFillSlaHours, x.CompletionSlaHours })
            .FirstOrDefaultAsync(cancellationToken);

        if (template is null)
        {
            return Result<ResolvedTemplate>.Fail("Template not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        // A survey must be answerable the moment it lands; only a published template has a frozen
        // version to pin it to.
        if (template.Status != TemplateStatuses.Published)
        {
            return Result<ResolvedTemplate>.Fail(
                "Only a published template can receive surveys.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400);
        }

        return Result<ResolvedTemplate>.Success(new ResolvedTemplate(
            template.Id,
            template.CurrentVersionNo,
            template.TeamFillSlaHours,
            template.CompletionSlaHours));
    }
}
