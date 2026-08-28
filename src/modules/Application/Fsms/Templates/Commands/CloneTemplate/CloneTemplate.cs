using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Templates.Common;
using KH.Application.Fsms.Templates.Models;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;
using System.Text.Json.Serialization;

namespace KH.Application.Fsms.Templates.Commands.CloneTemplate;

[Authorize(Policy = FsmsPolicies.ManageTemplates)]
public record CloneTemplateCommand : IRequest<Result<TemplateDetailDto>>
{
    [JsonIgnore]
    public long TemplateId { get; init; }
    public string NewTemplateCode { get; init; } = default!;
    public string NewTemplateNameEn { get; init; } = default!;
    public string NewTemplateNameAr { get; init; } = default!;
}

public sealed class CloneTemplateCommandValidator : AbstractValidator<CloneTemplateCommand>
{
    public CloneTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .GreaterThan(0).WithMessage("Template id is required.");

        RuleFor(x => x.NewTemplateCode)
            .NotEmpty().WithMessage("New template code is required.")
            .MaximumLength(50).WithMessage("Template code must not exceed 50 characters.");

        RuleFor(x => x.NewTemplateNameEn)
            .NotEmpty().WithMessage("English template name is required.")
            .MaximumLength(250).WithMessage("English template name must not exceed 250 characters.");

        RuleFor(x => x.NewTemplateNameAr)
            .NotEmpty().WithMessage("Arabic template name is required.")
            .MaximumLength(250).WithMessage("Arabic template name must not exceed 250 characters.");
    }
}

public sealed class CloneTemplateCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CloneTemplateCommand, Result<TemplateDetailDto>>
{
    public async Task<Result<TemplateDetailDto>> Handle(CloneTemplateCommand request, CancellationToken cancellationToken)
    {
        var source = await context.SurveyTemplates
            .IncludeFullDefinition()
            .FirstOrDefaultAsync(x => x.Id == request.TemplateId, cancellationToken);

        if (source is null)
        {
            return Result<TemplateDetailDto>.Fail("Source template not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        var newCode = request.NewTemplateCode.Trim();

        var codeExists = await context.SurveyTemplates
            .AnyAsync(x => x.TemplateCode == newCode, cancellationToken);

        if (codeExists)
        {
            return Result<TemplateDetailDto>.Fail(
                $"A template with code '{newCode}' already exists.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        var clone = source.Clone(newCode, request.NewTemplateNameEn, request.NewTemplateNameAr);

        context.SurveyTemplates.Add(clone);
        await context.SaveChangesAsync(cancellationToken);

        return Result<TemplateDetailDto>.Success(clone.ToDetailDto());
    }
}
