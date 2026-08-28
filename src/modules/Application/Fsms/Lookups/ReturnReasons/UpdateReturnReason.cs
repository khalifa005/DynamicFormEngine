using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Lookups.ReturnReasons;

[Authorize(Policy = FsmsPolicies.ManageLookups)]
public record UpdateReturnReasonCommand : IRequest<Result<long>>
{
    public long Id { get; init; }
    public string Code { get; init; } = default!;
    public string NameEn { get; init; } = default!;
    public string NameAr { get; init; } = default!;
    public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateReturnReasonCommandValidator : AbstractValidator<UpdateReturnReasonCommand>
{
    public UpdateReturnReasonCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0L).WithMessage("Return reason id is required.");

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

public sealed class UpdateReturnReasonCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateReturnReasonCommand, Result<long>>
{
    public async Task<Result<long>> Handle(UpdateReturnReasonCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.FsmsReturnReasons.FindAsync(new object[] { request.Id }, cancellationToken);

        if (entity is null)
            return Result<long>.Fail("Return reason not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);

        if (!string.Equals(entity.Code, request.Code, StringComparison.OrdinalIgnoreCase))
        {
            var codeTaken = await context.FsmsReturnReasons
                .AnyAsync(x => x.Code == request.Code && x.Id != request.Id, cancellationToken);

            if (codeTaken)
            {
                return Result<long>.Fail(
                    $"A return reason with code '{request.Code}' already exists.",
                    ApiErrorCodes.ValidationError,
                    httpStatusCode: 409);
            }
        }

        entity.Code = request.Code;
        entity.NameEn = request.NameEn;
        entity.NameAr = request.NameAr;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;

        await context.SaveChangesAsync(cancellationToken);

        return Result<long>.Success(entity.Id);
    }
}
