using System.Globalization;
using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Org;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;
using Shared.Core.Exceptions;

namespace KH.Application.Fsms.Surveys.Commands.BulkAllocateSurveys;

/// <summary>One survey's share of a bulk allocation.</summary>
public record BulkAllocateSurveyItem
{
    public long SurveyId { get; init; }
    public long FieldTeamId { get; init; }

    /// <summary>Overrides the survey's fill deadline for this allocation when supplied.</summary>
    public DateTimeOffset? DueDate { get; init; }

    /// <summary>Overrides the survey's completion (approve) deadline when supplied.</summary>
    public DateTimeOffset? CompletionDueDate { get; init; }

    public string? Note { get; init; }
}

/// <summary>
/// Allocates many surveys in one call, each to the crew the dispatcher chose for its location group.
///
/// Partial success is the point: a run of forty surveys must not be lost because one of them was
/// filled a minute earlier. Every item is checked and reported on its own, and the ones that pass
/// are saved together.
/// </summary>
[Authorize(Policy = FsmsPolicies.ManageAssignments)]
public record BulkAllocateSurveysCommand : IRequest<Result<BulkAllocationResultDto>>
{
    public IReadOnlyList<BulkAllocateSurveyItem> Items { get; init; } = [];
}

public sealed class BulkAllocateSurveysCommandValidator : AbstractValidator<BulkAllocateSurveysCommand>
{
    /// <summary>Caps one call at the same size the suggestion query will prepare.</summary>
    public const int MaxItems = 500;

    public BulkAllocateSurveysCommandValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one allocation is required.")
            .Must(items => items.Count <= MaxItems)
            .WithMessage($"No more than {MaxItems} surveys can be allocated at once.")
            .Must(items => items.Select(i => i.SurveyId).Distinct().Count() == items.Count)
            .WithMessage("A survey can only appear once in a bulk allocation.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.SurveyId)
                .GreaterThan(0).WithMessage("Survey id is required.");

            item.RuleFor(x => x.FieldTeamId)
                .GreaterThan(0).WithMessage("Field team id is required.");

            item.RuleFor(x => x.Note)
                .MaximumLength(1000).WithMessage("Note must not exceed 1000 characters.");
        });
    }
}

/// <summary>What became of one survey in the run.</summary>
public sealed class BulkAllocationItemResultDto
{
    public long SurveyId { get; init; }
    public string? SurveyCode { get; init; }
    public long FieldTeamId { get; init; }
    public bool Success { get; init; }

    /// <summary>Why it failed. Null on success.</summary>
    public string? Message { get; init; }
}

public sealed class BulkAllocationResultDto
{
    public int SucceededCount { get; init; }
    public int FailedCount { get; init; }
    public IReadOnlyList<BulkAllocationItemResultDto> Results { get; init; } = [];
}

public sealed class BulkAllocateSurveysCommandHandler(
    IApplicationDbContext context,
    IOrgScopeService orgScopeService,
    IUser user,
    TimeProvider timeProvider)
    : IRequestHandler<BulkAllocateSurveysCommand, Result<BulkAllocationResultDto>>
{
    public async Task<Result<BulkAllocationResultDto>> Handle(
        BulkAllocateSurveysCommand request,
        CancellationToken cancellationToken)
    {
        var surveyIds = request.Items.Select(x => x.SurveyId).Distinct().ToList();
        var teamIds = request.Items.Select(x => x.FieldTeamId).Distinct().ToList();

        // Tracked: the successful items are mutated and saved together at the end.
        var surveys = await context.Surveys
            .Include(x => x.Assignments)
            .Where(x => surveyIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var activeTeamIds = await context.FieldTeams
            .AsNoTracking()
            .Where(x => teamIds.Contains(x.Id) && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var activeTeams = activeTeamIds.ToHashSet();

        // One batch read for every crew named in the run rather than one per item.
        var teamScopes = await orgScopeService.GetScopesAsync(
            OrgScopeOwnerTypes.Team,
            teamIds.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToList(),
            cancellationToken);

        var callerScope = await orgScopeService.GetCurrentUserScopeAsync(cancellationToken);
        var assignedBy = user.Id;
        var assignedAt = timeProvider.GetUtcNow();

        var results = new List<BulkAllocationItemResultDto>(request.Items.Count);
        var succeeded = 0;

        foreach (var item in request.Items)
        {
            if (!surveys.TryGetValue(item.SurveyId, out var survey))
            {
                results.Add(Failure(item, null, "Survey not found."));
                continue;
            }

            // The dispatcher may only hand out work they can see; the same rule the worklist reads by.
            if (!callerScope.Covers(survey.CbuCode, survey.BranchCode, survey.OperationAreaCode, survey.DepartmentId))
            {
                results.Add(Failure(item, survey.SurveyCode, "This survey is outside your territory."));
                continue;
            }

            if (!activeTeams.Contains(item.FieldTeamId))
            {
                results.Add(Failure(item, survey.SurveyCode, "Field team not found or inactive."));
                continue;
            }

            var teamScope = teamScopes.GetValueOrDefault(
                item.FieldTeamId.ToString(CultureInfo.InvariantCulture),
                OrgScopeSet.Unrestricted());

            if (!teamScope.Covers(survey.CbuCode, survey.BranchCode, survey.OperationAreaCode, survey.DepartmentId))
            {
                results.Add(Failure(
                    item, survey.SurveyCode, "This field team's territory does not cover the survey's location."));
                continue;
            }

            if (survey.SubmissionCount > 0)
            {
                results.Add(Failure(
                    item, survey.SurveyCode, "A survey that has already been filled cannot be re-allocated."));
                continue;
            }

            if (survey.Assignments.Any(a => a.IsActive && a.FieldTeamId == item.FieldTeamId))
            {
                results.Add(Failure(item, survey.SurveyCode, "The survey is already allocated to this field team."));
                continue;
            }

            // The domain refuses a status that cannot carry an allocation. Catching it keeps one bad
            // survey from taking the rest of the run down with it.
            try
            {
                survey.Assign(
                    item.FieldTeamId,
                    assignedBy,
                    assignedAt,
                    item.DueDate,
                    item.Note,
                    item.CompletionDueDate);
            }
            catch (DomainException exception)
            {
                results.Add(Failure(item, survey.SurveyCode, exception.Message));
                continue;
            }

            succeeded++;
            results.Add(new BulkAllocationItemResultDto
            {
                SurveyId = item.SurveyId,
                SurveyCode = survey.SurveyCode,
                FieldTeamId = item.FieldTeamId,
                Success = true
            });
        }

        if (succeeded > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return Result<BulkAllocationResultDto>.Success(new BulkAllocationResultDto
        {
            SucceededCount = succeeded,
            FailedCount = results.Count - succeeded,
            Results = results
        });
    }

    private static BulkAllocationItemResultDto Failure(BulkAllocateSurveyItem item, string? surveyCode, string message) => new()
    {
        SurveyId = item.SurveyId,
        SurveyCode = surveyCode,
        FieldTeamId = item.FieldTeamId,
        Success = false,
        Message = message
    };
}
