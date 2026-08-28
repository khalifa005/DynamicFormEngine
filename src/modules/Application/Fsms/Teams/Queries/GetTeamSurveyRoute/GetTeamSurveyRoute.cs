using System.Globalization;
using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Lookups;
using KH.Application.Fsms.Common.Org;
using KH.Domain.Constants.Fsms;
using Microsoft.Extensions.Options;
using Shared.Core.Caching;
using Shared.Core.Common;
using Shared.Core.Options;

namespace KH.Application.Fsms.Teams.Queries.GetTeamSurveyRoute;

/// <summary>
/// One crew's working day laid out in order — the surveys they submitted on a given date, earliest
/// first, each carrying the coordinate of the asset it was raised against.
///
/// This is a reconstruction, not a movement trace: nothing in the system records where a surveyor
/// physically stood or how they travelled between jobs. What it does show is the sequence and timing
/// of the work, which is what a supervisor asking "where was this team on Tuesday" actually wants.
/// Distances are straight-line between consecutive stops and are labelled as such by the client.
/// </summary>
[Authorize(Policy = FsmsPolicies.ViewSurveys)]
public record GetTeamSurveyRouteQuery : IRequest<Result<TeamSurveyRouteDto>>
{
    /// <summary>
    /// Saudi civil time. <see cref="Date"/> is a calendar day with no timezone of its own, while
    /// submissions are stamped as instants, so the two only line up once an offset is chosen. A day
    /// measured in UTC would push an evening job onto the following date, which is exactly the
    /// boundary a supervisor would notice.
    /// </summary>
    public const int DefaultUtcOffsetMinutes = 180;

    /// <summary>Furthest either side of UTC any real branch sits (UTC-12 to UTC+14).</summary>
    public const int MinUtcOffsetMinutes = -720;
    public const int MaxUtcOffsetMinutes = 840;

    public long FieldTeamId { get; init; }

    /// <summary>The calendar day to read, in the offset given by <see cref="UtcOffsetMinutes"/>.</summary>
    public DateOnly Date { get; init; }

    /// <summary>
    /// Minutes east of UTC that <see cref="Date"/> is read in. Left unset it is
    /// <see cref="DefaultUtcOffsetMinutes"/>, so a client that does not care still gets the Saudi day.
    /// </summary>
    public int? UtcOffsetMinutes { get; init; }
}

public sealed class GetTeamSurveyRouteQueryValidator : AbstractValidator<GetTeamSurveyRouteQuery>
{
    public GetTeamSurveyRouteQueryValidator()
    {
        RuleFor(x => x.FieldTeamId)
            .GreaterThan(0).WithMessage("Field team id is required.");

        RuleFor(x => x.Date)
            .NotEqual(default(DateOnly)).WithMessage("Date is required.");

        RuleFor(x => x.UtcOffsetMinutes)
            .InclusiveBetween(
                GetTeamSurveyRouteQuery.MinUtcOffsetMinutes,
                GetTeamSurveyRouteQuery.MaxUtcOffsetMinutes)
            .WithMessage(
                $"UTC offset must be between {GetTeamSurveyRouteQuery.MinUtcOffsetMinutes} and {GetTeamSurveyRouteQuery.MaxUtcOffsetMinutes} minutes.")
            .When(x => x.UtcOffsetMinutes.HasValue);
    }
}

/// <summary>The crew's day: who, when, and the ordered stops that made it up.</summary>
public sealed class TeamSurveyRouteDto
{
    public long FieldTeamId { get; init; }
    public string FieldTeamName { get; init; } = default!;
    public string FieldTeamUserCode { get; init; } = default!;

    public DateOnly Date { get; init; }

    /// <summary>The offset the day was read in — echoed so the client can prove which day it got.</summary>
    public int UtcOffsetMinutes { get; init; }

    public DateTimeOffset? FirstStopAt { get; init; }
    public DateTimeOffset? LastStopAt { get; init; }

    /// <summary>Every survey submitted that day, whether or not it can be drawn.</summary>
    public int TotalStopCount { get; init; }

    /// <summary>How many of those carry a coordinate. Below <see cref="TotalStopCount"/> the drawn
    /// route is incomplete, and the client says so rather than presenting a partial line as the day.</summary>
    public int MappedStopCount { get; init; }

    /// <summary>
    /// Straight-line metres between consecutive mapped stops, summed. Not road distance — nothing
    /// here knows what route was driven, and the figure is only ever a floor on the real one.
    /// </summary>
    public double? TotalDistanceMeters { get; init; }

    public IReadOnlyList<TeamSurveyRouteStopDto> Stops { get; init; } = [];
}

/// <summary>
/// One stop on the day. Numbered over every stop rather than only the mapped ones, so "stop 4" means
/// the same thing in the list as it does on the map even when stop 3 had no coordinate to draw.
/// </summary>
public sealed class TeamSurveyRouteStopDto
{
    /// <summary>1-based position in the day.</summary>
    public int Sequence { get; init; }

    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = default!;
    public long TemplateId { get; init; }
    public string? TemplateNameEn { get; init; }
    public string? TemplateNameAr { get; init; }
    public string Status { get; init; } = default!;
    public string? FaId { get; init; }
    public string? TaskCode { get; init; }
    public string? FaTypeCode { get; init; }

    /// <summary>
    /// The asset's recorded location, null on a survey that never got one. Where the work was, not
    /// where the surveyor was — see the remarks on <see cref="GetTeamSurveyRouteQuery"/>.
    /// </summary>
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }

    public DateTimeOffset SubmittedDate { get; init; }

    /// <summary>Minutes since the previous stop of the day. Null on the first.</summary>
    public int? MinutesFromPreviousStop { get; init; }

    /// <summary>
    /// Straight-line metres from the previous <em>mapped</em> stop. Null on the first mapped stop and
    /// on every unmapped one.
    /// </summary>
    public double? DistanceFromPreviousMeters { get; init; }

    public string? CbuCode { get; init; }
    public string? BranchCode { get; init; }
    public string? BranchNameEn { get; init; }
    public string? BranchNameAr { get; init; }
    public string? OperationAreaCode { get; init; }
    public string? OperationAreaNameEn { get; init; }
    public string? OperationAreaNameAr { get; init; }
    public int? DepartmentId { get; init; }
    public string? DepartmentNameEn { get; init; }
    public string? DepartmentNameAr { get; init; }
}

/// <summary>
/// Reads the day in three passes rather than one clever query: the filtered survey read, the keyed
/// label lookups the page actually used, then a single walk over the ordered rows to number the stops
/// and measure the gaps between them. The same shape as the survey worklists, and for the same
/// reason — resolving labels inline costs an OUTER APPLY per lookup per row.
/// </summary>
public sealed class GetTeamSurveyRouteQueryHandler(
    IApplicationDbContext context,
    IOrgScopeService orgScopeService,
    ICacheService cache,
    IOptions<CacheSettings> cacheSettings)
    : IRequestHandler<GetTeamSurveyRouteQuery, Result<TeamSurveyRouteDto>>
{
    /// <summary>Mean Earth radius, the usual haversine constant.</summary>
    private const double EarthRadiusMeters = 6_371_000d;

    public async Task<Result<TeamSurveyRouteDto>> Handle(
        GetTeamSurveyRouteQuery request,
        CancellationToken cancellationToken)
    {
        var team = await context.FieldTeams
            .AsNoTracking()
            .Where(x => x.Id == request.FieldTeamId)
            .Select(x => new { x.Id, x.Name, x.UserCode })
            .FirstOrDefaultAsync(cancellationToken);

        if (team is null)
        {
            return Result<TeamSurveyRouteDto>.Fail(
                "Field team not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        var callerScope = await orgScopeService.GetCurrentUserScopeAsync(cancellationToken);

        if (!callerScope.IsUnrestricted)
        {
            // Refused rather than answered empty. A supervisor looking at another region's crew has
            // asked the wrong question, and a blank day reads as "they did nothing" instead.
            var teamScope = await orgScopeService.GetScopeAsync(
                OrgScopeOwnerTypes.Team,
                team.Id.ToString(CultureInfo.InvariantCulture),
                cancellationToken);

            if (!callerScope.Overlaps(teamScope))
            {
                return Result<TeamSurveyRouteDto>.Fail(
                    "This field team is outside your territory.",
                    ApiErrorCodes.UnauthorizedAccess,
                    httpStatusCode: 403);
            }
        }

        var offsetMinutes = request.UtcOffsetMinutes ?? GetTeamSurveyRouteQuery.DefaultUtcOffsetMinutes;
        var offset = TimeSpan.FromMinutes(offsetMinutes);
        var dayStart = new DateTimeOffset(request.Date.ToDateTime(TimeOnly.MinValue), offset);
        var dayEnd = dayStart.AddDays(1);

        // Half-open on purpose, and compared against the column untouched: wrapping SubmittedDate in
        // a date conversion would give the same rows and lose the index.
        var query = context.Surveys
            .AsNoTracking()
            .Where(x => x.Assignments.Any(a =>
                a.FieldTeamId == team.Id && a.Status != AssignmentStatuses.Reassigned))
            .Where(x => x.SubmittedDate >= dayStart && x.SubmittedDate < dayEnd);

        // The crew's own territory got them this far; this keeps an individual survey sitting outside
        // the *caller's* territory from riding along with it.
        if (!callerScope.IsUnrestricted)
        {
            query = query.Where(OrgScopeQueryFilter.ForSurveys(callerScope));
        }

        var rows = await query
            .OrderBy(x => x.SubmittedDate)
            .ThenBy(x => x.Id)
            .Select(x => new TeamRouteRow
            {
                SurveyId = x.Id,
                SurveyCode = x.SurveyCode,
                TemplateId = x.TemplateId,
                Status = x.Status,
                FaId = x.FaId,
                TaskCode = x.TaskCode,
                FaTypeCode = x.FaTypeCode,
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                SubmittedDate = x.SubmittedDate,
                CbuCode = x.CbuCode,
                BranchCode = x.BranchCode,
                OperationAreaCode = x.OperationAreaCode,
                DepartmentId = x.DepartmentId,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            // A quiet day is an answer, not a failure.
            return Result<TeamSurveyRouteDto>.Success(new TeamSurveyRouteDto
            {
                FieldTeamId = team.Id,
                FieldTeamName = team.Name,
                FieldTeamUserCode = team.UserCode,
                Date = request.Date,
                UtcOffsetMinutes = offsetMinutes,
                TotalStopCount = 0,
                MappedStopCount = 0,
                Stops = [],
            });
        }

        var branchNames = await LookupNameCache.GetBranchNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var operationAreaNames = await LookupNameCache.GetOperationAreaNamesAsync(context, cache, cacheSettings.Value, cancellationToken);
        var departmentNames = await LookupNameCache.GetDepartmentNamesAsync(context, cache, cacheSettings.Value, cancellationToken);

        var templateIds = rows.Select(x => x.TemplateId).Distinct().ToList();
        var templates = await context.SurveyTemplates
            .AsNoTracking()
            .Where(x => templateIds.Contains(x.Id))
            .Select(x => new { x.Id, x.TemplateNameEn, x.TemplateNameAr })
            .ToDictionaryAsync(x => x.Id, x => (x.TemplateNameEn, x.TemplateNameAr), cancellationToken);

        var stops = new List<TeamSurveyRouteStopDto>(rows.Count);
        var mappedStopCount = 0;
        double? totalDistanceMeters = null;
        DateTimeOffset? previousStopAt = null;
        (double Latitude, double Longitude)? previousMappedPoint = null;

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var submittedDate = row.SubmittedDate!.Value;

            var minutesFromPreviousStop = previousStopAt is DateTimeOffset previous
                ? (int)Math.Round((submittedDate - previous).TotalMinutes)
                : (int?)null;

            double? distanceFromPreviousMeters = null;

            if (row.Latitude is double latitude && row.Longitude is double longitude)
            {
                mappedStopCount++;

                if (previousMappedPoint is { } previousPoint)
                {
                    var leg = DistanceMeters(
                        previousPoint.Latitude, previousPoint.Longitude, latitude, longitude);

                    distanceFromPreviousMeters = leg;
                    totalDistanceMeters = (totalDistanceMeters ?? 0d) + leg;
                }

                previousMappedPoint = (latitude, longitude);
            }

            var branch = branchNames.FindBranch(row.BranchCode);
            var operationArea = operationAreaNames.FindOperationArea(row.OperationAreaCode);
            var department = departmentNames.FindDepartment(row.DepartmentId);
            var hasTemplate = templates.TryGetValue(row.TemplateId, out var template);

            stops.Add(new TeamSurveyRouteStopDto
            {
                Sequence = index + 1,
                SurveyId = row.SurveyId,
                SurveyCode = row.SurveyCode,
                TemplateId = row.TemplateId,
                TemplateNameEn = hasTemplate ? template.TemplateNameEn : null,
                TemplateNameAr = hasTemplate ? template.TemplateNameAr : null,
                Status = row.Status,
                FaId = row.FaId,
                TaskCode = row.TaskCode,
                FaTypeCode = row.FaTypeCode,
                Latitude = row.Latitude,
                Longitude = row.Longitude,
                SubmittedDate = submittedDate,
                MinutesFromPreviousStop = minutesFromPreviousStop,
                DistanceFromPreviousMeters = distanceFromPreviousMeters,
                CbuCode = row.CbuCode,
                BranchCode = row.BranchCode,
                BranchNameEn = branch?.NameEn,
                BranchNameAr = branch?.NameAr,
                OperationAreaCode = row.OperationAreaCode,
                OperationAreaNameEn = operationArea?.NameEn,
                OperationAreaNameAr = operationArea?.NameAr,
                DepartmentId = row.DepartmentId,
                DepartmentNameEn = department?.NameEn,
                DepartmentNameAr = department?.NameAr,
            });

            previousStopAt = submittedDate;
        }

        return Result<TeamSurveyRouteDto>.Success(new TeamSurveyRouteDto
        {
            FieldTeamId = team.Id,
            FieldTeamName = team.Name,
            FieldTeamUserCode = team.UserCode,
            Date = request.Date,
            UtcOffsetMinutes = offsetMinutes,
            FirstStopAt = stops[0].SubmittedDate,
            LastStopAt = stops[^1].SubmittedDate,
            TotalStopCount = stops.Count,
            MappedStopCount = mappedStopCount,
            TotalDistanceMeters = totalDistanceMeters,
            Stops = stops,
        });
    }

    /// <summary>
    /// Great-circle distance between two coordinates. Haversine rather than a projected approximation
    /// because a crew's day can cross a CBU, and the error of treating degrees as flat grows with it.
    /// </summary>
    private static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var lat1Radians = double.DegreesToRadians(lat1);
        var lat2Radians = double.DegreesToRadians(lat2);
        var deltaLatitude = double.DegreesToRadians(lat2 - lat1);
        var deltaLongitude = double.DegreesToRadians(lon2 - lon1);

        var a = Math.Pow(Math.Sin(deltaLatitude / 2), 2)
                + Math.Cos(lat1Radians) * Math.Cos(lat2Radians) * Math.Pow(Math.Sin(deltaLongitude / 2), 2);

        return EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}

/// <summary>
/// The survey columns the route needs, before its labels are resolved. Internal rather than nested
/// and private: EF materializes a projection through generated accessors, and a private nested target
/// compiles cleanly and then fails at runtime.
/// </summary>
internal sealed class TeamRouteRow
{
    public long SurveyId { get; init; }
    public string SurveyCode { get; init; } = default!;
    public long TemplateId { get; init; }
    public string Status { get; init; } = default!;
    public string? FaId { get; init; }
    public string? TaskCode { get; init; }
    public string? FaTypeCode { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }

    /// <summary>Never null in practice — the day filter is a range over this column — but the entity
    /// declares it nullable, so the projection has to carry it that way.</summary>
    public DateTimeOffset? SubmittedDate { get; init; }

    public string? CbuCode { get; init; }
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
    public int? DepartmentId { get; init; }
}
