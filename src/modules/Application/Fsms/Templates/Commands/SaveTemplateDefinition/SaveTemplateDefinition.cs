using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Common.Definition;
using KH.Application.Fsms.Templates.Common;
using KH.Application.Fsms.Templates.Models;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;
using System.Text.Json.Serialization;

namespace KH.Application.Fsms.Templates.Commands.SaveTemplateDefinition;

[Authorize(Policy = FsmsPolicies.ManageTemplates)]
public record SaveTemplateDefinitionCommand : IRequest<Result<TemplateDetailDto>>
{
    [JsonIgnore]
    public long TemplateId { get; init; }

    /// <summary>The form-builder definition document (name_en/name_ar/elements).</summary>
    public string DefinitionJson { get; init; } = default!;
}

public sealed class SaveTemplateDefinitionCommandValidator : AbstractValidator<SaveTemplateDefinitionCommand>
{
    public SaveTemplateDefinitionCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .GreaterThan(0).WithMessage("Template id is required.");

        RuleFor(x => x.DefinitionJson)
            .NotEmpty().WithMessage("A template definition is required.")
            .Must(SurveyDefinitionParser.IsValidJson).WithMessage("The template definition must be valid JSON.")
            .Must(SurveyDefinitionParser.HasElements).WithMessage("The template definition must contain at least one field.");
    }
}

public sealed class SaveTemplateDefinitionCommandHandler(IApplicationDbContext context)
    : IRequestHandler<SaveTemplateDefinitionCommand, Result<TemplateDetailDto>>
{
    public async Task<Result<TemplateDetailDto>> Handle(SaveTemplateDefinitionCommand request, CancellationToken cancellationToken)
    {
        var template = await context.SurveyTemplates
            .FirstOrDefaultAsync(x => x.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            return Result<TemplateDetailDto>.Fail("Template not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        var definition = SurveyDefinitionParser.Parse(request.DefinitionJson);

        template.SetDefinition(request.DefinitionJson, definition.NameEn, definition.NameAr);

        await context.SaveChangesAsync(cancellationToken);

        return Result<TemplateDetailDto>.Success(template.ToDetailDto());
    }
}
