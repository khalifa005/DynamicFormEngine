using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Lookups.Departments;

[Authorize(Policy = FsmsPolicies.ManageLookups)]
public record UpdateDepartmentCommand : IRequest<Result<int>>
{
    public int Id { get; init; }
    public string NameEn { get; init; } = default!;
    public string NameAr { get; init; } = default!;
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Department id is required.");

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage("English name is required.")
            .MaximumLength(250).WithMessage("English name must not exceed 250 characters.");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("Arabic name is required.")
            .MaximumLength(250).WithMessage("Arabic name must not exceed 250 characters.");
    }
}

public sealed class UpdateDepartmentCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateDepartmentCommand, Result<int>>
{
    public async Task<Result<int>> Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.FsmsDepartments.FindAsync(new object[] { request.Id }, cancellationToken);

        if (entity is null)
            return Result<int>.Fail("Department not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);

        entity.NameEn = request.NameEn;
        entity.NameAr = request.NameAr;
        entity.IsActive = request.IsActive;

        await context.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(entity.Id);
    }
}
