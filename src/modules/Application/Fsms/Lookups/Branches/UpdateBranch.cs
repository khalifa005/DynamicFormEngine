using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Lookups.Branches;

[Authorize(Policy = FsmsPolicies.ManageLookups)]
public record UpdateBranchCommand : IRequest<Result<long>>
{
    public long Id { get; init; }
    public string Code { get; init; } = default!;
    public string? CbuCode { get; init; }
    public string? TaskZone { get; init; }
    public string? BranchCode { get; init; }
    public string NameEn { get; init; } = default!;
    public string NameAr { get; init; } = default!;
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0L).WithMessage("Branch id is required.");

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

public sealed class UpdateBranchCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateBranchCommand, Result<long>>
{
    public async Task<Result<long>> Handle(UpdateBranchCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.FsmsBranches.FindAsync(new object[] { request.Id }, cancellationToken);

        if (entity is null)
            return Result<long>.Fail("Branch not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);

        if (!string.Equals(entity.Code, request.Code, StringComparison.OrdinalIgnoreCase))
        {
            var codeTaken = await context.FsmsBranches
                .AnyAsync(x => x.Code == request.Code && x.Id != request.Id, cancellationToken);

            if (codeTaken)
            {
                return Result<long>.Fail(
                    $"A branch with code '{request.Code}' already exists.",
                    ApiErrorCodes.ValidationError,
                    httpStatusCode: 409);
            }
        }

        entity.Code = request.Code;
        entity.CbuCode = request.CbuCode;
        entity.TaskZone = request.TaskZone;
        entity.BranchCode = request.BranchCode;
        entity.NameEn = request.NameEn;
        entity.NameAr = request.NameAr;
        entity.IsActive = request.IsActive;

        await context.SaveChangesAsync(cancellationToken);

        return Result<long>.Success(entity.Id);
    }
}
