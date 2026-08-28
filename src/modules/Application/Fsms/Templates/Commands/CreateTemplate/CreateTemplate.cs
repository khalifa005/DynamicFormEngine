using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Org;
using KH.Application.Fsms.Templates.Common;
using KH.Application.Fsms.Templates.Models;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Templates;
using Shared.Core.Common;

namespace KH.Application.Fsms.Templates.Commands.CreateTemplate;

[Authorize(Policy = FsmsPolicies.ManageTemplates)]
public record CreateTemplateCommand : IRequest<Result<TemplateDetailDto>>
{
    public string TemplateCode { get; init; } = default!;
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

public sealed class CreateTemplateCommandValidator : AbstractValidator<CreateTemplateCommand>
{
    public CreateTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateCode)
            .NotEmpty().WithMessage("Template code is required.")
            .MaximumLength(50).WithMessage("Template code must not exceed 50 characters.");

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

public sealed class CreateTemplateCommandHandler(IApplicationDbContext context, IOrgScopeService orgScopeService)
    : IRequestHandler<CreateTemplateCommand, Result<TemplateDetailDto>>
{
    public async Task<Result<TemplateDetailDto>> Handle(CreateTemplateCommand request, CancellationToken cancellationToken)
    {
        var code = request.TemplateCode.Trim();

        var codeExists = await context.SurveyTemplates
            .AnyAsync(x => x.TemplateCode == code, cancellationToken);

        if (codeExists)
        {
            return Result<TemplateDetailDto>.Fail(
                $"A template with code '{code}' already exists.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        var template = SurveyTemplate.Create(
            code,
            request.TemplateNameEn,
            request.TemplateNameAr,
            request.Category,
            request.TeamFillSlaHours,
            request.CompletionSlaHours,
            request.DepartmentId,
            request.BranchScope,
            request.FaTypeCode,
            request.AutoMigrateSurveysOnPublish);

        context.SurveyTemplates.Add(template);
        await context.SaveChangesAsync(cancellationToken);

        var scopeResult = await orgScopeService.ReplaceScopesAsync(
            OrgScopeOwnerTypes.Template, template.Id.ToString(), request.Scopes, cancellationToken);

        if (!scopeResult.IsSuccess)
        {
            return Result<TemplateDetailDto>.Fail(scopeResult.Errors);
        }

        return Result<TemplateDetailDto>.Success(template.ToDetailDto(request.Scopes));
    }
}
