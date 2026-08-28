using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Lookups;
using Shared.Core.Common;

namespace KH.Application.Fsms.Lookups.ReturnReasons;

[Authorize(Policy = FsmsPolicies.ManageLookups)]
public record CreateReturnReasonCommand : IRequest<Result<long>>
{
    public string Code { get; init; } = default!;
    public string NameEn { get; init; } = default!;
    public string NameAr { get; init; } = default!;
    public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class CreateReturnReasonCommandValidator : AbstractValidator<CreateReturnReasonCommand>
{
    public CreateReturnReasonCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Return reason code is required.")
            .MaximumLength(50).WithMessage("Return reason code must not exceed 50 characters.");

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage("English name is required.")
            .MaximumLength(250).WithMessage("English name must not exceed 250 characters.");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("Arabic name is required.")
            .MaximumLength(250).WithMessage("Arabic name must not exceed 250 characters.");

        RuleFor(x => x.SortOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Sort order must be zero or positive.");
    }
}

public sealed class CreateReturnReasonCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateReturnReasonCommand, Result<long>>
{
    public async Task<Result<long>> Handle(CreateReturnReasonCommand request, CancellationToken cancellationToken)
    {
        var codeTaken = await context.FsmsReturnReasons
            .AnyAsync(x => x.Code == request.Code, cancellationToken);

        if (codeTaken)
        {
            return Result<long>.Fail(
                $"A return reason with code '{request.Code}' already exists.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        var entity = new FsmsReturnReason
        {
            Code = request.Code,
            NameEn = request.NameEn,
            NameAr = request.NameAr,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };

        context.FsmsReturnReasons.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result<long>.Success(entity.Id);
    }
}
