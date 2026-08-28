using System.Text.Json;
using KH.Application.Common.Interfaces;
using KH.Application.Fsms.Surveys.Common;
using KH.Application.Fsms.Surveys.Models;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Surveys;
using Shared.Core.Common;

namespace KH.Application.Fsms.Surveys.Commands.CreateSurveyFromApi;

/// <summary>
/// The inbound (machine-to-machine) survey feed. Creates a survey in <c>CREATED</c> (PENDING)
/// pinned to the template's currently published version. Guarded by the API key on the endpoint,
/// so it carries no user-policy attribute of its own.
///
/// The caller names the FA type it raised work for and the feed finds the form: the originating
/// system knows its assets, not how the back office models them.
/// </summary>
public record CreateSurveyFromApiCommand : IRequest<Result<SurveyDetailDto>>
{
    /// <summary>
    /// Optional override, kept for callers still on the older contract. Left unset — the normal
    /// case — the template is resolved from <see cref="FaTypeCode"/>.
    /// </summary>
    public long? TemplateId { get; init; }

    /// <summary>Optional override, as <see cref="TemplateId"/>.</summary>
    public string? TemplateCode { get; init; }

    /// <summary>The originating system's code. Generated when the caller has none.</summary>
    public string? SurveyCode { get; init; }

    public string? FaId { get; init; }
    public string? TaskCode { get; init; }

    /// <summary>
    /// The kind of asset the work was raised for. This is what selects the template — the single
    /// published one configured for the type.
    /// </summary>
    public string? FaTypeCode { get; init; }

    public long? TaskTypeId { get; init; }
    public string? CustomerName { get; init; }

    /// <summary>The number the crew calls before arriving.</summary>
    public string? CustomerPhone { get; init; }

    public long? CustomerTypeId { get; init; }
    public string? MeterNumber { get; init; }
    public string? Hcn { get; init; }

    /// <summary>CBU code on the inbound payload.</summary>
    public string? CbuCode { get; init; }

    public string? BranchCode { get; init; }

    /// <summary>Operation-area code on the inbound payload.</summary>
    public string? OperationAreaCode { get; init; }

    public int? DepartmentId { get; init; }
    public DateTimeOffset? DueDate { get; init; }
    public DateTimeOffset? CompletionDueDate { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }

    /// <summary>Anything the originating system sends that the template does not model.</summary>
    public Dictionary<string, object?>? AdditionalData { get; init; }
}

public sealed class CreateSurveyFromApiCommandValidator : AbstractValidator<CreateSurveyFromApiCommand>
{
    public CreateSurveyFromApiCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.FaTypeCode)
                || x.TemplateId is > 0
                || !string.IsNullOrWhiteSpace(x.TemplateCode))
            .WithMessage("An FA type code is required (or a template id/code to override it).");

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

public sealed class CreateSurveyFromApiCommandHandler(
    IApplicationDbContext context,
    IUser user,
    TimeProvider timeProvider)
    : IRequestHandler<CreateSurveyFromApiCommand, Result<SurveyDetailDto>>
{
    public async Task<Result<SurveyDetailDto>> Handle(CreateSurveyFromApiCommand request, CancellationToken cancellationToken)
    {
        var resolution = await SurveyTemplateResolver.ResolveAsync(
            context,
            request.TemplateId,
            request.TemplateCode,
            request.FaTypeCode,
            cancellationToken);

        if (!resolution.IsSuccess || resolution.Data is null)
        {
            return Result<SurveyDetailDto>.Fail(resolution.Errors);
        }

        var template = resolution.Data;
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
            SurveySources.Api,
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
            SerializeAdditionalData(request.AdditionalData),
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

    private static string? SerializeAdditionalData(Dictionary<string, object?>? additionalData) =>
        additionalData is { Count: > 0 } ? JsonSerializer.Serialize(additionalData) : null;
}
