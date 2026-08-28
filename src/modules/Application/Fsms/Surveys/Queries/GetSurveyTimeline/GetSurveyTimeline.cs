using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Surveys.Models;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Surveys.Queries.GetSurveyTimeline;

/// <summary>The append-only status trail of one survey, oldest first.</summary>
[Authorize(Policy = FsmsPolicies.ViewSurveys)]
public record GetSurveyTimelineQuery : IRequest<Result<IReadOnlyList<SurveyTimelineEntryDto>>>
{
    public long SurveyId { get; init; }
}

public sealed class GetSurveyTimelineQueryValidator : AbstractValidator<GetSurveyTimelineQuery>
{
    public GetSurveyTimelineQueryValidator()
    {
        RuleFor(x => x.SurveyId)
            .GreaterThan(0).WithMessage("Survey id is required.");
    }
}

public sealed class GetSurveyTimelineQueryHandler(IApplicationDbContext context, IIdentityService identityService)
    : IRequestHandler<GetSurveyTimelineQuery, Result<IReadOnlyList<SurveyTimelineEntryDto>>>
{
    public async Task<Result<IReadOnlyList<SurveyTimelineEntryDto>>> Handle(
        GetSurveyTimelineQuery request,
        CancellationToken cancellationToken)
    {
        var surveyExists = await context.Surveys
            .AnyAsync(x => x.Id == request.SurveyId, cancellationToken);

        if (!surveyExists)
        {
            return Result<IReadOnlyList<SurveyTimelineEntryDto>>.Fail(
                "Survey not found.",
                ApiErrorCodes.NotFound,
                httpStatusCode: 404);
        }

        var rows = await context.SurveyStatusHistory
            .AsNoTracking()
            .Where(x => x.SurveyId == request.SurveyId)
            // Id breaks the tie: two transitions inside one request share a timestamp.
            .OrderBy(x => x.ChangedDate)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.SurveyId,
                x.FromStatus,
                x.ToStatus,
                x.ChangedBy,
                x.ChangedDate,
                x.Note,
            })
            .ToListAsync(cancellationToken);

        // The trail is read as "who moved this, and when", so the ids are resolved to names in one
        // query rather than left for the client to show raw.
        var userNames = await identityService.GetUserNamesAsync(
            rows.Select(row => row.ChangedBy),
            cancellationToken);

        var entries = rows
            .Select(row => new SurveyTimelineEntryDto
            {
                EntryId = row.Id,
                SurveyId = row.SurveyId,
                FromStatus = row.FromStatus,
                ToStatus = row.ToStatus,
                ChangedBy = row.ChangedBy,
                ChangedByName = row.ChangedBy is null ? null : userNames.GetValueOrDefault(row.ChangedBy),
                ChangedDate = row.ChangedDate,
                Note = row.Note,
            })
            .ToList();

        return Result<IReadOnlyList<SurveyTimelineEntryDto>>.Success(entries);
    }
}
