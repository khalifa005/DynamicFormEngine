using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Lookups;
using Shared.Core.Common;

namespace KH.Application.Fsms.Lookups.Branches;

[Authorize(Policy = FsmsPolicies.ManageLookups)]
public record CreateBranchCommand : IRequest<Result<long>>
{
    public string Code { get; init; } = default!;
    public string? CbuCode { get; init; }
    public string? TaskZone { get; init; }
    public string? BranchCode { get; init; }
    public string NameEn { get; init; } = default!;
    public string NameAr { get; init; } = default!;
    public bool IsActive { get; init; } = true;
}

public sealed class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Branch code is required.")
            .MaximumLength(50).WithMessage("Branch code must not exceed 50 characters.");

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage("English name is required.")
            .MaximumLength(250).WithMessage("English name must not exceed 250 characters.");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("Arabic name is required.")
            .MaximumLength(250).WithMessage("Arabic name must not exceed 250 characters.");
    }
}

public sealed class CreateBranchCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateBranchCommand, Result<long>>
{
    public async Task<Result<long>> Handle(CreateBranchCommand request, CancellationToken cancellationToken)
    {
        var codeTaken = await context.FsmsBranches
            .AnyAsync(x => x.Code == request.Code, cancellationToken);

        if (codeTaken)
        {
            return Result<long>.Fail(
                $"A branch with code '{request.Code}' already exists.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        var entity = new FsmsBranch
        {
            Code = request.Code,
            CbuCode = request.CbuCode,
            TaskZone = request.TaskZone,
            BranchCode = request.BranchCode,
            NameEn = request.NameEn,
            NameAr = request.NameAr,
            IsActive = request.IsActive
        };

        context.FsmsBranches.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result<long>.Success(entity.Id);
    }
}
