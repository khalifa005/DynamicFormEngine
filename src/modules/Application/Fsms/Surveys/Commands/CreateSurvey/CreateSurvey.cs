using System.Text.Json;
using System.Text.Json.Serialization;
using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.Surveys.Common;
using KH.Application.Fsms.Surveys.Models;
using KH.Application.Fsms.Templates.Common;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Surveys;
using Shared.Core.Common;

namespace KH.Application.Fsms.Surveys.Commands.CreateSurvey;

/// <summary>
/// Operator-raised survey — same landing state as the inbound feed, different source. The create
/// dialog can pick the form by FA type exactly as the feed does, or name the template outright.
/// </summary>
[Authorize(Policy = FsmsPolicies.CreateOrManageAssignments)]
public record CreateSurveyCommand : IRequest<Result<SurveyDetailDto>>
{
    /// <summary>Names the template directly. Left unset, <see cref="FaTypeCode"/> selects it.</summary>
    public long? TemplateId { get; init; }

    /// <summary>Generated when the operator leaves it blank.</summary>
    public string? SurveyCode { get; init; }

    public string? FaId { get; init; }
    public string? TaskCode { get; init; }
    public string? FaTypeCode { get; init; }
    public long? TaskTypeId { get; init; }
    public string? CustomerName { get; init; }

    /// <summary>The number the crew calls before arriving.</summary>
    public string? CustomerPhone { get; init; }

    public long? CustomerTypeId { get; init; }
    public string? MeterNumber { get; init; }
    public string? Hcn { get; init; }
    public string? CbuCode { get; init; }
    public string? BranchCode { get; init; }
    public string? OperationAreaCode { get; init; }
    public int? DepartmentId { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public DateTimeOffset? CompletionDueDate { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public Dictionary<string, object?>? AdditionalData { get; init; }

    /// <summary>
    /// Which door the survey came in through — see <see cref="SurveySources"/>. Set by the endpoint,
    /// never by the caller: the back-office dialog raises it as <see cref="SurveySources.Manual"/>,
    /// the field-team app as <see cref="SurveySources.Team"/>.
    /// </summary>
    [JsonIgnore]
    public string Source { get; init; } = SurveySources.Manual;
}

public sealed class CreateSurveyCommandValidator : AbstractValidator<CreateSurveyCommand>
{
    public CreateSurveyCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.TemplateId is > 0 || !string.IsNullOrWhiteSpace(x.FaTypeCode))
            .WithMessage("Either a template id or an FA type code is required.");

        RuleFor(x => x.Source)
            .Must(SurveySources.IsDefined).WithMessage("Source is not a recognized survey source.");

        RuleFor(x => x.SurveyCode)
            .MaximumLength(60).WithMessage("Survey code must not exceed 60 characters.");

        RuleFor(x => x.FaId)
            .MaximumLength(100).WithMessage("FA id must not exceed 100 characters.");

        RuleFor(x => x.TaskCode)
            .MaximumLength(100).WithMessage("Task code must not exceed 100 characters.");

        RuleFor(x => x.FaTypeCode)
            .MaximumLength(50).WithMessage("FA type code must not exceed 50 characters.");

        RuleFor(x => x.TaskTypeId)
            .GreaterThan(0L).WithMessage("Task type id must be a positive number.")
            .When(x => x.TaskTypeId.HasValue);

        RuleFor(x => x.CustomerName)
            .MaximumLength(SurveyFieldLimits.CustomerName)
            .WithMessage($"Customer name must not exceed {SurveyFieldLimits.CustomerName} characters.");

        RuleFor(x => x.CustomerPhone)
            .MaximumLength(SurveyFieldLimits.CustomerPhone)
            .WithMessage($"Customer phone must not exceed {SurveyFieldLimits.CustomerPhone} characters.");

        RuleFor(x => x.CustomerTypeId)
            .GreaterThan(0L).WithMessage("Customer type id must be a positive number.")
            .When(x => x.CustomerTypeId.HasValue);

        RuleFor(x => x.MeterNumber)
            .MaximumLength(SurveyFieldLimits.MeterNumber)
            .WithMessage($"Meter number must not exceed {SurveyFieldLimits.MeterNumber} characters.");

        RuleFor(x => x.Hcn)
            .MaximumLength(SurveyFieldLimits.Hcn)
            .WithMessage($"HCN must not exceed {SurveyFieldLimits.Hcn} characters.");

        RuleFor(x => x.CbuCode)
            .MaximumLength(50).WithMessage("CBU code must not exceed 50 characters.");

        RuleFor(x => x.BranchCode)
            .MaximumLength(50).WithMessage("Branch code must not exceed 50 characters.");

        RuleFor(x => x.OperationAreaCode)
            .MaximumLength(50).WithMessage("Operation area code must not exceed 50 characters.");

        RuleFor(x => x.Latitude)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Latitude is required.")
            .InclusiveBetween(-90, 90).WithMessage("Latitude must be between -90 and 90.");

        RuleFor(x => x.Longitude)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Longitude is required.")
            .InclusiveBetween(-180, 180).WithMessage("Longitude must be between -180 and 180.");
    }
}

public sealed class CreateSurveyCommandHandler(
    IApplicationDbContext context,
    IUser user,
    IOrgScopeService orgScopeService,
    TimeProvider timeProvider)
    : IRequestHandler<CreateSurveyCommand, Result<SurveyDetailDto>>
{
    private const string OutsideTerritoryMessage = "You cannot create a survey outside your territory.";

    public async Task<Result<SurveyDetailDto>> Handle(CreateSurveyCommand request, CancellationToken cancellationToken)
    {
        // Containment, not overlap: this survey is being placed somewhere, so the caller's coverage
        // has to reach that exact spot — the same test AllocateSurvey applies before handing work to
        // a crew. A caller who names no territory at all fails it, which is deliberate: the survey
        // list matches on CBU, branch or area, so a placeless survey would be invisible to the very
        // person who raised it.
        var callerScope = await orgScopeService.GetCurrentUserScopeAsync(cancellationToken);

        if (!callerScope.Covers(request.CbuCode, request.BranchCode, request.OperationAreaCode, request.DepartmentId))
        {
            return Result<SurveyDetailDto>.Fail(
                OutsideTerritoryMessage, ApiErrorCodes.UnauthorizedAccess, httpStatusCode: 403);
        }

        var resolution = await SurveyTemplateResolver.ResolveAsync(
            context,
            request.TemplateId,
            templateCode: null,
            request.FaTypeCode,
            cancellationToken);

        if (!resolution.IsSuccess || resolution.Data is null)
        {
            return Result<SurveyDetailDto>.Fail(resolution.Errors);
        }

        var template = resolution.Data;

        // Checked after resolution rather than on the request, because the FA type path picks the
        // template for the caller — they never name the id being checked here.
        if (!await TemplateScopeGuard.CanSeeAsync(orgScopeService, callerScope, template.TemplateId, cancellationToken))
        {
            return Result<SurveyDetailDto>.Fail(
                TemplateScopeGuard.OutsideTerritoryMessage, ApiErrorCodes.UnauthorizedAccess, httpStatusCode: 403);
        }

        var now = timeProvider.GetUtcNow();
        var surveyCode = string.IsNullOrWhiteSpace(request.SurveyCode)
            ? SurveyCodeFactory.Next(now)
            : request.SurveyCode.Trim();

        var codeExists = await context.Surveys.AnyAsync(x => x.SurveyCode == surveyCode, cancellationToken);
        if (codeExists)
        {
            return Result<SurveyDetailDto>.Fail(
                $"A survey with code '{surveyCode}' already exists.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        var versionId = await SurveyTemplateResolver.ResolveVersionIdAsync(context, template, cancellationToken);

        var survey = Survey.Create(
            surveyCode,
            template.TemplateId,
            versionId,
            template.CurrentVersionNo,
            request.Source,
            user.Id,
            now,
            request.FaId,
            request.TaskCode,
            request.FaTypeCode,
            request.CbuCode,
            request.BranchCode,
            request.OperationAreaCode,
            request.DepartmentId,
            request.DueDate,
            request.AdditionalData is { Count: > 0 } ? JsonSerializer.Serialize(request.AdditionalData) : null,
            request.Latitude,
            request.Longitude,
            request.TaskTypeId,
            request.CustomerName,
            request.CustomerTypeId,
            request.MeterNumber,
            request.Hcn,
            request.CustomerPhone);

        survey.ApplySlaDefaults(template.TeamFillSlaHours, template.CompletionSlaHours);
        survey.SetCompletionDueDate(request.CompletionDueDate);

        context.Surveys.Add(survey);
        await context.SaveChangesAsync(cancellationToken);

        return Result<SurveyDetailDto>.Success(await survey.ToDetailDtoAsync(context, cancellationToken));
    }
}
