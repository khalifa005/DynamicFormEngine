using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Lookups;
using Shared.Core.Common;

namespace KH.Application.Fsms.Lookups.OperationAreas;

[Authorize(Policy = FsmsPolicies.ManageLookups)]
public record CreateOperationAreaCommand : IRequest<Result<long>>
{
    public string Code { get; init; } = default!;
    public string CbuCode { get; init; } = default!;
    public string? MainAreaCode { get; init; }
    public string NameEn { get; init; } = default!;
    public string NameAr { get; init; } = default!;
    public bool IsActive { get; init; } = true;
}

public sealed class CreateOperationAreaCommandValidator : AbstractValidator<CreateOperationAreaCommand>
{
    public CreateOperationAreaCommandValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Operation area code is required.")
            .MaximumLength(50).WithMessage("Operation area code must not exceed 50 characters.");

        RuleFor(x => x.CbuCode)
            .NotEmpty().WithMessage("CBU code is required.")
            .MaximumLength(50).WithMessage("CBU code must not exceed 50 characters.");

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage("English name is required.")
            .MaximumLength(250).WithMessage("English name must not exceed 250 characters.");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("Arabic name is required.")
            .MaximumLength(250).WithMessage("Arabic name must not exceed 250 characters.");
    }
}

public sealed class CreateOperationAreaCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateOperationAreaCommand, Result<long>>
{
    public async Task<Result<long>> Handle(CreateOperationAreaCommand request, CancellationToken cancellationToken)
    {
        var codeTaken = await context.FsmsOperationAreas
            .AnyAsync(x => x.Code == request.Code, cancellationToken);

        if (codeTaken)
        {
            return Result<long>.Fail(
                $"An operation area with code '{request.Code}' already exists.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        var entity = new FsmsOperationArea
        {
            Code = request.Code,
            CbuCode = request.CbuCode,
            MainAreaCode = request.MainAreaCode,
            NameEn = request.NameEn,
            NameAr = request.NameAr,
            IsActive = request.IsActive
        };

        context.FsmsOperationAreas.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result<long>.Success(entity.Id);
    }
}
