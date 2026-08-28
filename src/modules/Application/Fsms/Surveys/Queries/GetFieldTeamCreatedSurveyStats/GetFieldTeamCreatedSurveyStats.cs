using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Surveys.Queries.GetFieldTeamCreatedSurveyStats;

/// <summary>
/// Daily created vs currently-approved counts for field-raised surveys this crew raised itself.
/// Approvals are attributed back to the creation calendar day, even when the reviewer acted later —
/// that is the figure a profile chart and a pay run both need.
///
/// The team is never taken from the request: the only crew this query answers for is the one on the
/// token. Credit stays with the creating crew; a later re-allocation does not move the counts.
/// </summary>
[Authorize(Policy = FsmsPolicies.CreateOrManageAssignments)]
public record GetFieldTeamCreatedSurveyStatsQuery : IRequest<Result<FieldTeamCreatedSurveyStatsDto>>
{
    /// <summary>
    /// Saudi civil time. <see cref="FromDate"/> / <see cref="ToDate"/> are calendar days with no
    /// timezone of their own, while creation is stamped as an instant, so the two only line up once
    /// an offset is chosen. A day measured in UTC would push an evening job onto the following date.
    /// </summary>
    public const int DefaultUtcOffsetMinutes = 180;

    /// <summary>Furthest either side of UTC any real branch sits (UTC-12 to UTC+14).</summary>
    public const int MinUtcOffsetMinutes = -720;
    public const int MaxUtcOffsetMinutes = 840;

    /// <summary>Inclusive calendar span this read will cover, in the chosen offset.</summary>
    public const int MaxRangeDays = 366;

    /// <summary>Inclusive start of the calendar range, in the offset given by <see cref="UtcOffsetMinutes"/>.</summary>
    public DateOnly FromDate { get; init; }

    /// <summary>Inclusive end of the calendar range, in the offset given by <see cref="UtcOffsetMinutes"/>.</summary>
    public DateOnly ToDate { get; init; }

    /// <summary>
    /// Minutes east of UTC that <see cref="FromDate"/> and <see cref="ToDate"/> are read in. Left
    /// unset it is <see cref="DefaultUtcOffsetMinutes"/>, so a client that does not care still gets
    /// the Saudi day.
    /// </summary>
    public int? UtcOffsetMinutes { get; init; }
}

public sealed class GetFieldTeamCreatedSurveyStatsQueryValidator : AbstractValidator<GetFieldTeamCreatedSurveyStatsQuery>
{
    public GetFieldTeamCreatedSurveyStatsQueryValidator()
    {
        RuleFor(x => x.FromDate)
            .NotEqual(default(DateOnly)).WithMessage("From date is required.");

        RuleFor(x => x.ToDate)
            .NotEqual(default(DateOnly)).WithMessage("To date is required.")
            .GreaterThanOrEqualTo(x => x.FromDate)
            .WithMessage("The end of the date range must not precede its start.")
            .When(x => x.FromDate != default && x.ToDate != default);

        RuleFor(x => x)
            .Must(x => x.ToDate.DayNumber - x.FromDate.DayNumber + 1 <= GetFieldTeamCreatedSurveyStatsQuery.MaxRangeDays)
            .WithMessage($"The date range cannot exceed {GetFieldTeamCreatedSurveyStatsQuery.MaxRangeDays} days.")
            .When(x => x.FromDate != default && x.ToDate != default && x.ToDate >= x.FromDate);

        RuleFor(x => x.UtcOffsetMinutes)
            .InclusiveBetween(
                GetFieldTeamCreatedSurveyStatsQuery.MinUtcOffsetMinutes,
                GetFieldTeamCreatedSurveyStatsQuery.MaxUtcOffsetMinutes)
            .WithMessage(
                $"UTC offset must be between {GetFieldTeamCreatedSurveyStatsQuery.MinUtcOffsetMinutes} and {GetFieldTeamCreatedSurveyStatsQuery.MaxUtcOffsetMinutes} minutes.")
            .When(x => x.UtcOffsetMinutes.HasValue);
    }
}

/// <summary>
/// Created vs currently-approved counts for the caller's own field-raised work, bucketed by the
/// calendar day each survey was created. <see cref="TotalApproved"/> is the pay figure for the range.
/// </summary>
public sealed class FieldTeamCreatedSurveyStatsDto
{
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }

    // Hidden for now — still applied when grouping; default is Saudi UTC+3.
    // public int UtcOffsetMinutes { get; init; }

    public int TotalCreated { get; init; }

    /// <summary>Surveys created in the range whose current status is <c>APPROVED</c>.</summary>
    public int TotalApproved { get; init; }

    public IReadOnlyList<FieldTeamCreatedSurveyStatsDayDto> Days { get; init; } = [];
}

/// <summary>One calendar day of the chart: how many the crew raised, and how many of those are approved now.</summary>
public sealed class FieldTeamCreatedSurveyStatsDayDto
{
    public DateOnly Date { get; init; }
    public int CreatedCount { get; init; }
    public int ApprovedCount { get; init; }
}

public sealed class GetFieldTeamCreatedSurveyStatsQueryHandler(
    IApplicationDbContext context,
    IUser user)
    : IRequestHandler<GetFieldTeamCreatedSurveyStatsQuery, Result<FieldTeamCreatedSurveyStatsDto>>
{
    private const string OnlyFieldTeamMessage = "This endpoint is only available to a field-team login.";

    public async Task<Result<FieldTeamCreatedSurveyStatsDto>> Handle(
        GetFieldTeamCreatedSurveyStatsQuery request,
        CancellationToken cancellationToken)
    {
        if (user.FieldTeamId is not long fieldTeamId)
        {
            return Result<FieldTeamCreatedSurveyStatsDto>.Fail(
                OnlyFieldTeamMessage,
                ApiErrorCodes.UnauthorizedAccess,
                httpStatusCode: 403);
        }

        var offsetMinutes = request.UtcOffsetMinutes ?? GetFieldTeamCreatedSurveyStatsQuery.DefaultUtcOffsetMinutes;
        var offset = TimeSpan.FromMinutes(offsetMinutes);
        var rangeStart = new DateTimeOffset(request.FromDate.ToDateTime(TimeOnly.MinValue), offset);
        var rangeEnd = new DateTimeOffset(request.ToDate.ToDateTime(TimeOnly.MinValue), offset).AddDays(1);

        // Half-open on purpose, and compared against the column untouched: wrapping the instant in a
        // date conversion would give the same rows and lose the index. Device capture is the field
        // working day; server Created is the fallback when a row was never stamped from a device.
        var rows = await context.Surveys
            .AsNoTracking()
            .Where(x => x.Source == SurveySources.Team)
            .Where(x => x.Assignments
                .OrderBy(a => a.AssignedDate)
                .ThenBy(a => a.Id)
                .Select(a => a.FieldTeamId)
                .FirstOrDefault() == fieldTeamId)
            .Where(x => (x.DeviceCreatedDate ?? x.Created) >= rangeStart
                && (x.DeviceCreatedDate ?? x.Created) < rangeEnd)
            .Select(x => new FieldTeamCreatedSurveyStatsRow
            {
                CreatedAt = x.DeviceCreatedDate ?? x.Created,
                IsApproved = x.Status == SurveyStatuses.Approved,
            })
            .ToListAsync(cancellationToken);

        var countsByDay = rows
            .GroupBy(x => DateOnly.FromDateTime(x.CreatedAt.ToOffset(offset).DateTime))
            .ToDictionary(
                group => group.Key,
                group => (Created: group.Count(), Approved: group.Count(x => x.IsApproved)));

        var days = new List<FieldTeamCreatedSurveyStatsDayDto>();
        for (var date = request.FromDate; date <= request.ToDate; date = date.AddDays(1))
        {
            countsByDay.TryGetValue(date, out var counts);
            days.Add(new FieldTeamCreatedSurveyStatsDayDto
            {
                Date = date,
                CreatedCount = counts.Created,
                ApprovedCount = counts.Approved,
            });
        }

        return Result<FieldTeamCreatedSurveyStatsDto>.Success(new FieldTeamCreatedSurveyStatsDto
        {
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            // UtcOffsetMinutes = offsetMinutes,
            TotalCreated = days.Sum(x => x.CreatedCount),
            TotalApproved = days.Sum(x => x.ApprovedCount),
            Days = days,
        });
    }
}

/// <summary>
/// The two columns the chart needs, before they are bucketed by civil day. Internal rather than
/// nested and private: EF materializes a projection through generated accessors, and a private
/// nested target compiles cleanly and then fails at runtime.
/// </summary>
internal sealed class FieldTeamCreatedSurveyStatsRow
{
    public DateTimeOffset CreatedAt { get; init; }
    public bool IsApproved { get; init; }
}
