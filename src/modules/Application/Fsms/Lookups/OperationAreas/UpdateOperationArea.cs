using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Lookups.OperationAreas;

[Authorize(Policy = FsmsPolicies.ManageLookups)]
public record UpdateOperationAreaCommand : IRequest<Result<long>>
{
    public long Id { get; init; }
    public string Code { get; init; } = default!;
    public string CbuCode { get; init; } = default!;
    public string? MainAreaCode { get; init; }
    public string NameEn { get; init; } = default!;
    public string NameAr { get; init; } = default!;
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateOperationAreaCommandValidator : AbstractValidator<UpdateOperationAreaCommand>
{
    public UpdateOperationAreaCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0L).WithMessage("Operation area id is required.");

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

public sealed class UpdateOperationAreaCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateOperationAreaCommand, Result<long>>
{
    public async Task<Result<long>> Handle(UpdateOperationAreaCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.FsmsOperationAreas.FindAsync(new object[] { request.Id }, cancellationToken);

        if (entity is null)
            return Result<long>.Fail("Operation area not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);

        if (!string.Equals(entity.Code, request.Code, StringComparison.OrdinalIgnoreCase))
        {
            var codeTaken = await context.FsmsOperationAreas
                .AnyAsync(x => x.Code == request.Code && x.Id != request.Id, cancellationToken);

            if (codeTaken)
            {
                return Result<long>.Fail(
                    $"An operation area with code '{request.Code}' already exists.",
                    ApiErrorCodes.ValidationError,
                    httpStatusCode: 409);
            }
        }

        entity.Code = request.Code;
        entity.CbuCode = request.CbuCode;
        entity.MainAreaCode = request.MainAreaCode;
        entity.NameEn = request.NameEn;
        entity.NameAr = request.NameAr;
        entity.IsActive = request.IsActive;

        await context.SaveChangesAsync(cancellationToken);

        return Result<long>.Success(entity.Id);
    }
}
