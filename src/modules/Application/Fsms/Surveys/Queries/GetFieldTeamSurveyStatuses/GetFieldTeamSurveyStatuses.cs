using System.Linq.Expressions;
using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Lookups;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Surveys;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Options;

namespace KH.Application.Fsms.Surveys.Queries.GetFieldTeamSurveyStatuses;

/// <summary>
/// Answers "where do these stand?" for a batch of surveys the mobile app already holds — the cheap
/// reconciliation read an offline client needs after it drains its queue, or after a submit whose
/// response never made it back over a bad connection.
///
/// The alternative was a detail read per survey or the whole sync bundle; this pulls status alone,
/// for as many surveys as the app asks about, in one call. Nothing here is cached: a status the app
/// is checking precisely because it may have moved is the last thing worth serving stale.
///
/// A survey can be named by its server id or by the code the device generated for it — a create
/// whose response was lost leaves the app holding only the code. Either key answers, and the reply
/// carries both so the app can fill in whichever it is missing.
///
/// Scoped to the caller's own team like every other field-team read: a key belonging to another crew
/// is reported as not found rather than answered, so the endpoint never confirms that another team's
/// work exists. Work this crew still holds after it closed — <c>APPROVED</c> or <c>EXPIRED</c> —
/// is answered; only a re-allocation away from the team is treated as not found.
/// </summary>
[Authorize(Policy = FsmsPolicies.SubmitOrReviewSurveys)]
public record GetFieldTeamSurveyStatusesQuery : IRequest<Result<FieldTeamSurveyStatusBatchDto>>
{
    /// <summary>Server survey ids the app holds.</summary>
    public IReadOnlyList<long> SurveyIds { get; init; } = [];

    /// <summary>Device-generated survey codes, for work whose server id the app never received.</summary>
    public IReadOnlyList<string> SurveyCodes { get; init; } = [];
}

public sealed class GetFieldTeamSurveyStatusesQueryValidator : AbstractValidator<GetFieldTeamSurveyStatusesQuery>
{
    /// <summary>
    /// The cap is on keys of either kind together, since both sides cost the same round trip. Matched
    /// to the bulk-allocation read rather than to bulk submit: this call writes nothing and returns a
    /// handful of columns per survey, so it carries a far larger batch comfortably.
    /// </summary>
    public const int MaxKeys = 500;

    /// <summary>Mirrors the SurveyCode column length in the EF configuration.</summary>
    public const int MaxSurveyCodeLength = 60;

    public GetFieldTeamSurveyStatusesQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => KeyCount(x) > 0)
            .WithMessage("At least one survey id or survey code is required.")
            .Must(x => KeyCount(x) <= MaxKeys)
            .WithMessage($"No more than {MaxKeys} surveys can be checked at once.");

        RuleForEach(x => x.SurveyIds)
            .GreaterThan(0).WithMessage("Survey id must be greater than 0.");

        RuleForEach(x => x.SurveyCodes)
            .NotEmpty().WithMessage("Survey code cannot be empty.")
            .MaximumLength(MaxSurveyCodeLength)
            .WithMessage($"Survey code cannot exceed {MaxSurveyCodeLength} characters.");
    }

    /// <summary>
    /// Null-tolerant on purpose: a client that sends <c>"surveyIds": null</c> rather than omitting it
    /// binds a null collection, and that must fail validation rather than throw inside the rule.
    /// </summary>
    private static int KeyCount(GetFieldTeamSurveyStatusesQuery query) =>
        (query.SurveyIds?.Count ?? 0) + (query.SurveyCodes?.Count ?? 0);
}

/// <summary>
/// The current state of one survey, and just enough around it for the app to act on that state
/// without a second call: clear its queue row, show the crew why work came back, or decide the
/// local copy is stale. Answers, definition and fill history stay with the detail read.
/// </summary>
public sealed class FieldTeamSurveyStatusDto
{
    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = default!;

    /// <summary>See <see cref="SurveyStatuses"/>.</summary>
    public string Status { get; init; } = default!;

    public DateTimeOffset? SubmittedDate { get; init; }

    /// <summary>How many fills are on record — 0 means nothing the crew sent has landed.</summary>
    public int SubmissionCount { get; init; }

    /// <summary>Structured cause of the current return. Null unless the survey stands returned.</summary>
    public string? ReturnReasonCode { get; init; }

    /// <summary>English label of that code from the lookup. Null unless the survey stands returned.</summary>
    public string? ReturnReasonNameEn { get; init; }

    /// <summary>Arabic label of that code from the lookup. Null unless the survey stands returned.</summary>
    public string? ReturnReasonNameAr { get; init; }

    /// <summary>What the reviewer wrote when returning the survey, for the crew to read.</summary>
    public string? ReturnReason { get; init; }

    public int ReturnCount { get; init; }
    public DateTimeOffset? DueDate { get; init; }

    /// <summary>Last server-side change. The app compares this against its local copy.</summary>
    public DateTimeOffset LastModified { get; init; }
}

public sealed class FieldTeamSurveyStatusBatchDto
{
    public IReadOnlyList<FieldTeamSurveyStatusDto> Items { get; init; } = [];

    /// <summary>
    /// Ids that answered nothing — unknown, or re-allocated away from the caller's team. Closed work
    /// this crew still holds (<c>APPROVED</c> / <c>EXPIRED</c>) is in <see cref="Items"/>, not here.
    /// The app drops these from its local store; it has no further business with them either way.
    /// </summary>
    public IReadOnlyList<long> NotFoundSurveyIds { get; init; } = [];

    /// <summary>Codes that answered nothing — see <see cref="NotFoundSurveyIds"/>.</summary>
    public IReadOnlyList<string> NotFoundSurveyCodes { get; init; } = [];

    /// <summary>
    /// The server's clock at the moment of the read, as the sync bundle reports it. Gives the app one
    /// reference to date its local copy by, rather than trusting a device clock that may have drifted.
    /// </summary>
    public DateTimeOffset ServerTime { get; init; }
}

public sealed class GetFieldTeamSurveyStatusesQueryHandler(
    IApplicationDbContext context,
    IUser user,
    TimeProvider timeProvider,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetFieldTeamSurveyStatusesQuery, Result<FieldTeamSurveyStatusBatchDto>>
{
    public async Task<Result<FieldTeamSurveyStatusBatchDto>> Handle(
        GetFieldTeamSurveyStatusesQuery request,
        CancellationToken cancellationToken)
    {
        if (user.FieldTeamId is not long fieldTeamId)
        {
            return Result<FieldTeamSurveyStatusBatchDto>.Fail(
                "This endpoint is only available to a field-team login.",
                ApiErrorCodes.UnauthorizedAccess,
                httpStatusCode: 403);
        }

        var ids = (request.SurveyIds ?? []).Distinct().ToList();

        var codes = (request.SurveyCodes ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rows = new List<FieldTeamSurveyStatusRow>(ids.Count + codes.Count);

        // Two seeks rather than one query with `ids.Contains(Id) || codes.Contains(SurveyCode)`: an OR
        // across two lists lets neither index drive, and the read degrades to a scan of SURVEYS. Split,
        // the id side seeks the primary key and the code side the unique IX_SURVEYS_SurveyCode. An
        // ids-only request — what the app sends nearly every time — is still a single round trip.
        //
        // EF.Parameter sends each list as one parameter, translating to a single OPENJSON seek. Left to
        // its default EF Core 10 expands it to IN (@id1, @id2, ...), whose SQL text — and therefore its
        // cached plan — changes with every distinct batch size. A mobile client checks whatever it
        // happens to be holding, so that would mean a plan per count; this way it is one plan for all.
        if (ids.Count > 0)
        {
            rows.AddRange(await ScopedToTeam(fieldTeamId)
                .Where(x => EF.Parameter(ids).Contains(x.Id))
                .Select(StatusProjection)
                .ToListAsync(cancellationToken));
        }

        if (codes.Count > 0)
        {
            rows.AddRange(await ScopedToTeam(fieldTeamId)
                .Where(x => EF.Parameter(codes).Contains(x.SurveyCode))
                .Select(StatusProjection)
                .ToListAsync(cancellationToken));
        }

        var found = new Dictionary<long, FieldTeamSurveyStatusRow>(rows.Count);
        foreach (var row in rows)
        {
            // The same survey can be named by both its id and its code in one request; it answers once.
            found.TryAdd(row.SurveyId, row);
        }

        // Case-insensitive to match the database collation the codes were matched under. Ordinal here
        // would let a differently-cased code answer in `Items` and be listed as not found in the same
        // reply — and the app acts on not-found by dropping the survey it was just told about.
        var foundCodes = found.Values
            .Select(x => x.SurveyCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var returnReasonNames = await LookupNameCache.GetReturnReasonNamesAsync(
            context, cache, cacheSettings.Value, cancellationToken);

        var batch = new FieldTeamSurveyStatusBatchDto
        {
            Items = found.Values
                .Select(row =>
                {
                    var returnReason = returnReasonNames.FindReturnReason(row.ReturnReasonCode);
                    return new FieldTeamSurveyStatusDto
                    {
                        SurveyId = row.SurveyId,
                        SurveyCode = row.SurveyCode,
                        Status = row.Status,
                        SubmittedDate = row.SubmittedDate,
                        SubmissionCount = row.SubmissionCount,
                        ReturnReasonCode = row.ReturnReasonCode,
                        ReturnReasonNameEn = returnReason?.NameEn,
                        ReturnReasonNameAr = returnReason?.NameAr,
                        ReturnReason = row.ReturnReason,
                        ReturnCount = row.ReturnCount,
                        DueDate = row.DueDate,
                        LastModified = row.LastModified,
                    };
                })
                .ToList(),
            NotFoundSurveyIds = ids.Where(id => !found.ContainsKey(id)).ToList(),
            NotFoundSurveyCodes = codes.Where(code => !foundCodes.Contains(code)).ToList(),
            ServerTime = timeProvider.GetUtcNow(),
        };

        return Result<FieldTeamSurveyStatusBatchDto>.Success(batch);
    }

    /// <summary>
    /// The crew only ever sees work allocated to the team on its own token — the team is never taken
    /// from the request. Closed allocations still belong to this crew; a <c>REASSIGNED</c> row does
    /// not. Driven by survey id, so the EXISTS rides IX_SURVEY_ASSIGNMENTS_SurveyId.
    /// </summary>
    private IQueryable<Survey> ScopedToTeam(long fieldTeamId) =>
        context.Surveys
            .AsNoTracking()
            .Where(x => x.Assignments.Any(a =>
                a.FieldTeamId == fieldTeamId && a.Status != AssignmentStatuses.Reassigned));

    /// <summary>
    /// Every column comes off SURVEYS alone — no template, lookup or assignment join — so the read
    /// stays one statement however many surveys are asked about.
    /// </summary>
    private static readonly Expression<Func<Survey, FieldTeamSurveyStatusRow>> StatusProjection =
        x => new FieldTeamSurveyStatusRow
        {
            SurveyId = x.Id,
            SurveyCode = x.SurveyCode,
            Status = x.Status,
            SubmittedDate = x.SubmittedDate,
            SubmissionCount = x.SubmissionCount,
            ReturnReasonCode = x.ReturnReasonCode,
            ReturnReason = x.ReturnReason,
            ReturnCount = x.ReturnCount,
            DueDate = x.DueDate,
            LastModified = x.LastModified,
        };
}

/// <summary>
/// The projection target. Internal rather than nested and private: EF materializes a projection
/// through generated accessors, and a private nested target compiles cleanly and then fails at
/// runtime.
/// </summary>
internal sealed class FieldTeamSurveyStatusRow
{
    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = default!;
    public string Status { get; init; } = default!;
    public DateTimeOffset? SubmittedDate { get; init; }
    public int SubmissionCount { get; init; }
    public string? ReturnReasonCode { get; init; }
    public string? ReturnReason { get; init; }
    public int ReturnCount { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public DateTimeOffset LastModified { get; init; }
}
