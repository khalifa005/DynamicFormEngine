using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Templates.Common;
using KH.Application.Fsms.Templates.Models;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Templates.Commands.ArchiveTemplate;

[Authorize(Policy = FsmsPolicies.ManageTemplates)]
public record ArchiveTemplateCommand : IRequest<Result<TemplateDetailDto>>
{
    public long TemplateId { get; init; }
}

public sealed class ArchiveTemplateCommandValidator : AbstractValidator<ArchiveTemplateCommand>
{
    public ArchiveTemplateCommandValidator()
    {
        RuleFor(x => x.TemplateId)
            .GreaterThan(0).WithMessage("Template id is required.");
    }
}

public sealed class ArchiveTemplateCommandHandler(IApplicationDbContext context)
    : IRequestHandler<ArchiveTemplateCommand, Result<TemplateDetailDto>>
{
    public async Task<Result<TemplateDetailDto>> Handle(ArchiveTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await context.SurveyTemplates
            .IncludeFullDefinition()
            .FirstOrDefaultAsync(x => x.Id == request.TemplateId, cancellationToken);

        if (template is null)
        {
            return Result<TemplateDetailDto>.Fail("Template not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);
        }

        template.Archive();

        await context.SaveChangesAsync(cancellationToken);

        return Result<TemplateDetailDto>.Success(template.ToDetailDto());
    }
}
