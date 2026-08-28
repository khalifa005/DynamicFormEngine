using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.Templates.Common;
using KH.Application.Fsms.Templates.Models;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;
using System.Text.Json.Serialization;

namespace KH.Application.Fsms.Templates.Commands.UpdateTemplate;

[Authorize(Policy = FsmsPolicies.ManageTemplates)]
public record UpdateTemplateCommand : IRequest<Result<TemplateDetailDto>>
{
    [JsonIgnore]
    public long TemplateId { get; init; }
    public string TemplateNameEn { get; init; } = default!;
    public string TemplateNameAr { get; init; } = default!;
    public string Category { get; init; } = default!;
    public int? DepartmentId { get; init; }
    public string? BranchScope { get; init; }

    /// <summary>
    /// The FA type this template surveys. An inbound survey names the FA type it was raised for,
    /// and the published template carrying that code is the one it is answered against.
    /// </summary>
    public string? FaTypeCode { get; init; }

    /// <summary>Calendar hours from allocation until the field team must submit.</summary>
    public int TeamFillSlaHours { get; init; }

    /// <summary>Calendar hours after the fill window for back-office completion.</summary>
    public int CompletionSlaHours { get; init; }

    /// <summary>
    /// When true, publishing a new version of this template queues a job that re-pins its own
    /// surveys — the ones not filled in yet — to that version instead of leaving them behind.
    /// </summary>
    public bool AutoMigrateSurveysOnPublish { get; init; }

    /// <summary>The territory this template may be used in — superseded <see cref="BranchScope"/>.</summary>
    public IReadOnlyList<OrgScopeAssignment> Scopes { get; init; } = [];
}

public sealed class UpdateTemplateCommandValidator : AbstractValidator<UpdateTemplateCommand>
{
    public UpdateTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .GreaterThan(0).WithMessage("Template id is required.");

        RuleFor(x => x.TemplateNameEn)
            .NotEmpty().WithMessage("English template name is required.")
            .MaximumLength(250).WithMessage("English template name must not exceed 250 characters.");

        RuleFor(x => x.TemplateNameAr)
            .NotEmpty().WithMessage("Arabic template name is required.")
            .MaximumLength(250).WithMessage("Arabic template name must not exceed 250 characters.");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required.")
            .Must(SurveyCategories.IsDefined).WithMessage("Category is not a recognized survey category.");

        RuleFor(x => x.FaTypeCode)
            .MaximumLength(50).WithMessage("FA type code must not exceed 50 characters.");

        RuleFor(x => x.TeamFillSlaHours)
            .GreaterThan(0).WithMessage("Team fill SLA hours must be greater than zero.");

        RuleFor(x => x.CompletionSlaHours)
            .GreaterThan(0).WithMessage("Completion SLA hours must be greater than zero.");

        // A scope row may name a territory, a department, or both, so neither half of the
        // territory is required here. That a row carries at least one of them, and that its
        // codes exist, is checked by IOrgScopeService.ValidateAsync, where the reference data is.
        RuleForEach(x => x.Scopes).ChildRules(scope =>
        {
            scope.RuleFor(x => x.Level)
                .Must(OrgScopeLevels.IsDefined).WithMessage("Scope level is not recognized.")
                .When(x => !string.IsNullOrWhiteSpace(x.Level));
        });
    }
}

public sealed class UpdateTemplateCommandHandler(IApplicationDbContext context, IOrgScopeService orgScopeService)
    : IRequestHandler<UpdateTemplateCommand, Result<TemplateDetailDto>>
{
    public async Task<Result<TemplateDetailDto>> Handle(UpdateTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await context.SurveyTemplates
            .IncludeFullDefinition()
            .FirstOrDefaultAsync(x => x.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            return Result<TemplateDetailDto>.Fail("Template not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        // Main-info, scopes, and the auto-migrate flag never change Status. Skip the write when
        // none of the fields UpdateDetails owns have actually changed.
        var detailsChanged =
            template.TemplateNameEn != request.TemplateNameEn ||
            template.TemplateNameAr != request.TemplateNameAr ||
            template.Category != request.Category ||
            template.DepartmentId != request.DepartmentId ||
            template.BranchScope != request.BranchScope ||
            template.FaTypeCode != request.FaTypeCode ||
            template.TeamFillSlaHours != request.TeamFillSlaHours ||
            template.CompletionSlaHours != request.CompletionSlaHours;

        if (detailsChanged)
        {
            template.UpdateDetails(
                request.TemplateNameEn,
                request.TemplateNameAr,
                request.Category,
                request.DepartmentId,
                request.BranchScope,
                request.TeamFillSlaHours,
                request.CompletionSlaHours,
                request.FaTypeCode);
        }

        var autoMigrateChanged = template.AutoMigrateSurveysOnPublish != request.AutoMigrateSurveysOnPublish;

        if (autoMigrateChanged)
        {
            template.SetAutoMigrateSurveysOnPublish(request.AutoMigrateSurveysOnPublish);
        }

        if (detailsChanged || autoMigrateChanged)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        var scopeResult = await orgScopeService.ReplaceScopesAsync(
            OrgScopeOwnerTypes.Template, template.Id.ToString(), request.Scopes, cancellationToken);

        if (!scopeResult.IsSuccess)
        {
            return Result<TemplateDetailDto>.Fail(scopeResult.Errors);
        }

        return Result<TemplateDetailDto>.Success(template.ToDetailDto(request.Scopes));
    }
}
