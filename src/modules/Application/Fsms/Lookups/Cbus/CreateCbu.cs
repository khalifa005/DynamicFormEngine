using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Lookups;
using Shared.Core.Common;

namespace KH.Application.Fsms.Lookups.Cbus;

[Authorize(Policy = FsmsPolicies.ManageLookups)]
public record CreateCbuCommand : IRequest<Result<int>>
{
    public string Code { get; init; } = default!;
    public string ClusterCode { get; init; } = default!;
    public string NameEn { get; init; } = default!;
    public string NameAr { get; init; } = default!;
    public long? OrgId { get; init; }
    public string? OrgCode { get; init; }
    public string? DefaultTaskZone { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class CreateCbuCommandValidator : AbstractValidator<CreateCbuCommand>
{
    public CreateCbuCommandValidator()
    {
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

public sealed class CreateCbuCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateCbuCommand, Result<int>>
{
    public async Task<Result<int>> Handle(CreateCbuCommand request, CancellationToken cancellationToken)
    {
        var codeTaken = await context.FsmsCbus
            .AnyAsync(x => x.Code == request.Code, cancellationToken);

        if (codeTaken)
        {
            return Result<int>.Fail(
                $"A CBU with code '{request.Code}' already exists.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        var entity = new FsmsCbu
        {
            Code = request.Code,
            ClusterCode = request.ClusterCode,
            NameEn = request.NameEn,
            NameAr = request.NameAr,
            OrgId = request.OrgId,
            OrgCode = request.OrgCode,
            DefaultTaskZone = request.DefaultTaskZone,
            IsActive = request.IsActive
        };

        context.FsmsCbus.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(entity.Id);
    }
}
