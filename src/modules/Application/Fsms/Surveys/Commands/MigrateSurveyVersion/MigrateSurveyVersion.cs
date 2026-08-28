using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Surveys.Common;
using KH.Application.Fsms.Surveys.Models;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Surveys.Commands.MigrateSurveyVersion;

/// <summary>
/// Moves an in-flight survey onto its template's newest published version. A survey is pinned to
/// the version it was raised against so a republish never changes the form under a crew mid-job;
/// this is the back office deciding that a particular survey should pick the newer form up anyway.
/// </summary>
[Authorize(Policy = FsmsPolicies.ManageAssignments)]
public record MigrateSurveyVersionCommand : IRequest<Result<SurveyDetailDto>>
{
    public long SurveyId { get; init; }

    public string? Note { get; init; }
}

public sealed class MigrateSurveyVersionCommandValidator : AbstractValidator<MigrateSurveyVersionCommand>
{
    public MigrateSurveyVersionCommandValidator()
    {
        RuleFor(x => x.SurveyId)
            .GreaterThan(0).WithMessage("Survey id is required.");

        RuleFor(x => x.Note)
            .MaximumLength(1000).WithMessage("Note must not exceed 1000 characters.");
    }
}

public sealed class MigrateSurveyVersionCommandHandler(
    IApplicationDbContext context,
    IUser user,
    TimeProvider timeProvider)
    : IRequestHandler<MigrateSurveyVersionCommand, Result<SurveyDetailDto>>
{
    public async Task<Result<SurveyDetailDto>> Handle(MigrateSurveyVersionCommand request, CancellationToken cancellationToken)
    {
        var survey = await context.Surveys
            .IncludeDetail()
            .FirstOrDefaultAsync(x => x.Id == request.SurveyId, cancellationToken);

        if (survey is null)
        {
            return Result<SurveyDetailDto>.Fail("Survey not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        // The newest snapshot of the survey's own template — a survey never crosses to another one.
        var latestVersion = await context.TemplateVersions
            .AsNoTracking()
            .Where(v => v.TemplateId == survey.TemplateId && v.TargetClient == TargetClients.Formly)
            .OrderByDescending(v => v.VersionNo)
            .Select(v => new { v.Id, v.VersionNo })
            .FirstOrDefaultAsync(cancellationToken);

        if (latestVersion is null)
        {
            return Result<SurveyDetailDto>.Fail(
                "The template has no published version to move to.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400);
        }

        if (survey.TemplateVersionNo is int currentVersionNo && latestVersion.VersionNo <= currentVersionNo)
        {
            return Result<SurveyDetailDto>.Fail(
                $"The survey is already on the newest template version ({currentVersionNo}).",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 400);
        }

        survey.MigrateToTemplateVersion(
            latestVersion.Id,
            latestVersion.VersionNo,
            user.Id,
            timeProvider.GetUtcNow(),
            request.Note);

        await context.SaveChangesAsync(cancellationToken);

        return Result<SurveyDetailDto>.Success(await survey.ToDetailDtoAsync(context, cancellationToken));
    }
}
