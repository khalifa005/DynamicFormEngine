using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Lookups;
using Shared.Core.Common;

namespace KH.Application.Fsms.Lookups.CustomerTypes;

[Authorize(Policy = FsmsPolicies.ManageLookups)]
public record CreateCustomerTypeCommand : IRequest<Result<long>>
{
    public string Code { get; init; } = default!;
    public string NameEn { get; init; } = default!;
    public string NameAr { get; init; } = default!;
    public bool IsActive { get; init; } = true;
}

public sealed class CreateCustomerTypeCommandValidator : AbstractValidator<CreateCustomerTypeCommand>
{
    public CreateCustomerTypeCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Customer type code is required.")
            .MaximumLength(50).WithMessage("Customer type code must not exceed 50 characters.");

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage("English name is required.")
            .MaximumLength(250).WithMessage("English name must not exceed 250 characters.");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("Arabic name is required.")
            .MaximumLength(250).WithMessage("Arabic name must not exceed 250 characters.");
    }
}

public sealed class CreateCustomerTypeCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateCustomerTypeCommand, Result<long>>
{
    public async Task<Result<long>> Handle(CreateCustomerTypeCommand request, CancellationToken cancellationToken)
    {
        var codeTaken = await context.FsmsCustomerTypes
            .AnyAsync(x => x.Code == request.Code, cancellationToken);

        if (codeTaken)
        {
            return Result<long>.Fail(
                $"A customer type with code '{request.Code}' already exists.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        var entity = new FsmsCustomerType
        {
            Code = request.Code,
            NameEn = request.NameEn,
            NameAr = request.NameAr,
            IsActive = request.IsActive
        };

        context.FsmsCustomerTypes.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result<long>.Success(entity.Id);
    }
}
