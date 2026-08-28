using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Submissions.Interfaces;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Submissions.Queries.GetSurveyLatestFill;

/// <summary>
/// The answers a survey now holds, ready to seed a repeat fill. A survey is filled more than once —
/// the field team on site, the back office afterwards, and either side again to correct what they
/// entered — so a repeat fill opens on the answers already given rather than on a blank form.
/// </summary>
/// <remarks>
/// The answers are accumulated across every fill rather than taken from the last one. Each fill
/// writes only what it answered, so the newest row alone would open the back office's form on its
/// own previous four answers and lose the crew's eighteen — which, now that every fill has to carry
/// the whole required set, would leave that form impossible to submit.
/// </remarks>
[Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
public record GetSurveyLatestFillQuery : IRequest<Result<SurveyLatestFillDto>>
{
    public long SurveyId { get; init; }
}

public sealed class GetSurveyLatestFillQueryValidator : AbstractValidator<GetSurveyLatestFillQuery>
{
    public GetSurveyLatestFillQueryValidator()
    {
        RuleFor(x => x.SurveyId)
            .GreaterThan(0).WithMessage("Survey id is required.");
    }
}

/// <summary>
/// The previous fill, ready to seed a form. <see cref="HasFill"/> tells the client whether it is
/// looking at real answers or at an untouched survey, which an empty <see cref="Answers"/> alone
/// could not — a fill where every answer was left blank looks identical.
/// </summary>
public sealed class SurveyLatestFillDto
{
    public bool HasFill { get; init; }
    public long SubmissionId { get; init; }
    public long TemplateId { get; init; }
    public int? VersionNo { get; init; }

    /// <summary>See <see cref="FilledByRoles"/>.</summary>
    public string? FilledByRole { get; init; }

    public string? SubmittedBy { get; init; }

    /// <summary>
    /// The display name behind <see cref="SubmittedBy"/>, so a reader shows who filled the survey
    /// without a second call. Null when the account no longer exists — the id still stands in.
    /// </summary>
    public string? SubmittedByName { get; init; }

    public DateTimeOffset? SubmittedDate { get; init; }

    /// <summary>
    /// Answers keyed by field <c>data_name</c>, exactly as stored — accumulated across every fill,
    /// newest value winning. The metadata above describes the latest fill alone.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Answers { get; init; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);
}

public sealed class GetSurveyLatestFillQueryHandler(
    IApplicationDbContext context,
    ISurveySubmissionStore submissionStore,
    IIdentityService identityService)
    : IRequestHandler<GetSurveyLatestFillQuery, Result<SurveyLatestFillDto>>
{
    public async Task<Result<SurveyLatestFillDto>> Handle(GetSurveyLatestFillQuery request, CancellationToken cancellationToken)
    {
        var survey = await context.Surveys
            .AsNoTracking()
            .Where(x => x.Id == request.SurveyId)
            .Select(x => new { x.TemplateId })
            .FirstOrDefaultAsync(cancellationToken);

        if (survey is null)
        {
            return Result<SurveyLatestFillDto>.Fail("Survey not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        var rows = await submissionStore.ListBySurveyAsync(survey.TemplateId, request.SurveyId, cancellationToken);

        // A survey nobody has filled yet is a valid answer, not a 404 — the form simply opens blank.
        if (rows.Count == 0)
        {
            return Result<SurveyLatestFillDto>.Success(new SurveyLatestFillDto { TemplateId = survey.TemplateId });
        }

        var latest = rows[^1];
        var submittedBy = latest.GetValueOrDefault(SubmissionColumns.SubmittedBy) as string;

        return Result<SurveyLatestFillDto>.Success(new SurveyLatestFillDto
        {
            HasFill = true,
            SubmissionId = Read<long>(latest, SubmissionColumns.Id),
            TemplateId = survey.TemplateId,
            VersionNo = ReadNullable<int>(latest, SubmissionColumns.VersionNo),
            FilledByRole = latest.GetValueOrDefault(SubmissionColumns.FilledByRole) as string,
            SubmittedBy = submittedBy,
            SubmittedByName = string.IsNullOrWhiteSpace(submittedBy)
                ? null
                : await identityService.GetUserNameAsync(submittedBy),
            SubmittedDate = ReadNullable<DateTimeOffset>(latest, SubmissionColumns.SubmittedDate),
            Answers = MergeAnswers(rows),
        });
    }

    /// <summary>
    /// The survey's answers as they now stand: every fill laid over the one before it, newest value
    /// winning, with a column a fill left NULL leaving the earlier answer in place.
    /// </summary>
    /// <remarks>
    /// No single row holds the survey's answers. Each fill writes its own, and the columns it did not
    /// answer stay NULL — so the field team's row carries the on-site answers and the back office's
    /// carries the verification, and neither is the whole form.
    /// <para>
    /// The metadata above deliberately still describes the <em>latest</em> fill: the answers are the
    /// survey's, but "who filled it and when" is a property of one submission.
    /// </para>
    /// </remarks>
    private static IReadOnlyDictionary<string, object?> MergeAnswers(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        var merged = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            foreach (var (column, value) in row)
            {
                // Everything that is not one of the fixed columns is a template field.
                if (SubmissionColumns.IsBase(column) || value is null)
                {
                    continue;
                }

                merged[column] = value;
            }
        }

        return merged;
    }

    private static T Read<T>(IReadOnlyDictionary<string, object?> row, string column)
        where T : struct =>
        ReadNullable<T>(row, column) ?? default;

    private static T? ReadNullable<T>(IReadOnlyDictionary<string, object?> row, string column)
        where T : struct =>
        row.GetValueOrDefault(column) is T value ? value : null;
}
