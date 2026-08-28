using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Lookups.Cbus;

[Authorize(Policy = FsmsPolicies.ManageLookups)]
public record UpdateCbuCommand : IRequest<Result<int>>
{
    public int Id { get; init; }
    public string Code { get; init; } = default!;
    public string ClusterCode { get; init; } = default!;
    public string NameEn { get; init; } = default!;
    public string NameAr { get; init; } = default!;
    public long? OrgId { get; init; }
    public string? OrgCode { get; init; }
    public string? DefaultTaskZone { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateCbuCommandValidator : AbstractValidator<UpdateCbuCommand>
{
    public UpdateCbuCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("CBU id is required.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("CBU code is required.")
            .MaximumLength(50).WithMessage("CBU code must not exceed 50 characters.");

        RuleFor(x => x.ClusterCode)
            .NotEmpty().WithMessage("Cluster code is required.")
            .MaximumLength(50).WithMessage("Cluster code must not exceed 50 characters.");

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage("English name is required.")
            .MaximumLength(250).WithMessage("English name must not exceed 250 characters.");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("Arabic name is required.")
            .MaximumLength(250).WithMessage("Arabic name must not exceed 250 characters.");
    }
}

public sealed class UpdateCbuCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateCbuCommand, Result<int>>
{
    public async Task<Result<int>> Handle(UpdateCbuCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.FsmsCbus.FindAsync(new object[] { request.Id }, cancellationToken);

        if (entity is null)
            return Result<int>.Fail("CBU not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);

        // If the code changed, verify the new code is not already taken by another record.
        if (!string.Equals(entity.Code, request.Code, StringComparison.OrdinalIgnoreCase))
        {
            var codeTaken = await context.FsmsCbus
                .AnyAsync(x => x.Code == request.Code && x.Id != request.Id, cancellationToken);

            if (codeTaken)
            {
                return Result<int>.Fail(
                    $"A CBU with code '{request.Code}' already exists.",
                    ApiErrorCodes.ValidationError,
                    httpStatusCode: 409);
            }
        }

        entity.Code = request.Code;
        entity.ClusterCode = request.ClusterCode;
        entity.NameEn = request.NameEn;
        entity.NameAr = request.NameAr;
        entity.OrgId = request.OrgId;
        entity.OrgCode = request.OrgCode;
        entity.DefaultTaskZone = request.DefaultTaskZone;
        entity.IsActive = request.IsActive;

        await context.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(entity.Id);
    }
}
