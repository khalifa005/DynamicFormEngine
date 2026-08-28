using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Lookups;
using Shared.Core.Common;

namespace KH.Application.Fsms.Lookups.Departments;

[Authorize(Policy = FsmsPolicies.ManageLookups)]
public record CreateDepartmentCommand : IRequest<Result<int>>
{
    public string NameEn { get; init; } = default!;
    public string NameAr { get; init; } = default!;
    public bool IsActive { get; init; } = true;
}

public sealed class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator()
    {
        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage("English name is required.")
            .MaximumLength(250).WithMessage("English name must not exceed 250 characters.");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("Arabic name is required.")
            .MaximumLength(250).WithMessage("Arabic name must not exceed 250 characters.");
    }
}

public sealed class CreateDepartmentCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateDepartmentCommand, Result<int>>
{
    /// <summary>First id handed out when the table is empty; the seeded WFM ids start above it.</summary>
    private const int FirstDepartmentId = 1;

    public async Task<Result<int>> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        // LKP_DEPARTMENT mirrors WFM, so its PK is not database-generated (see
        // FsmsDepartmentConfiguration). Without an id of our own EF would insert 0, which succeeds
        // once and then collides with itself forever, so the next id is allocated here. Two
        // simultaneous creates could pick the same number; the table is a small admin-managed
        // lookup, and the PK rejects the loser rather than letting a duplicate through.
        var highestId = await context.FsmsDepartments
            .MaxAsync(x => (int?)x.Id, cancellationToken);

        var entity = new FsmsDepartment
        {
            Id = (highestId ?? FirstDepartmentId - 1) + 1,
            NameEn = request.NameEn,
            NameAr = request.NameAr,
            IsActive = request.IsActive
        };

        context.FsmsDepartments.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(entity.Id);
    }
}
