using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Lookups.FaTypes;

[Authorize(Policy = FsmsPolicies.ManageLookups)]
public record UpdateFaTypeCommand : IRequest<Result<long>>
{
    public long Id { get; init; }
    public string FaTypeCode { get; init; } = default!;
    public long TaskTypeId { get; init; }
    public string NameEn { get; init; } = default!;
    public string NameAr { get; init; } = default!;
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateFaTypeCommandValidator : AbstractValidator<UpdateFaTypeCommand>
{
    public UpdateFaTypeCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0L).WithMessage("FA type id is required.");

        RuleFor(x => x.FaTypeCode)
            .NotEmpty().WithMessage("FA type code is required.")
            .MaximumLength(50).WithMessage("FA type code must not exceed 50 characters.");

        RuleFor(x => x.TaskTypeId)
            .GreaterThan(0L).WithMessage("Task type ID must be a positive number.");

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage("English name is required.")
            .MaximumLength(250).WithMessage("English name must not exceed 250 characters.");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("Arabic name is required.")
            .MaximumLength(250).WithMessage("Arabic name must not exceed 250 characters.");
    }
}

public sealed class UpdateFaTypeCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateFaTypeCommand, Result<long>>
{
    public async Task<Result<long>> Handle(UpdateFaTypeCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.FsmsFaTypes.FindAsync(new object[] { request.Id }, cancellationToken);

        if (entity is null)
            return Result<long>.Fail("FA type not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);

        if (!string.Equals(entity.FaTypeCode, request.FaTypeCode, StringComparison.OrdinalIgnoreCase))
        {
            var codeTaken = await context.FsmsFaTypes
                .AnyAsync(x => x.FaTypeCode == request.FaTypeCode && x.Id != request.Id, cancellationToken);

            if (codeTaken)
            {
                return Result<long>.Fail(
                    $"An FA type with code '{request.FaTypeCode}' already exists.",
                    ApiErrorCodes.ValidationError,
                    httpStatusCode: 409);
            }
        }

        entity.FaTypeCode = request.FaTypeCode;
        entity.TaskTypeId = request.TaskTypeId;
        entity.NameEn = request.NameEn;
        entity.NameAr = request.NameAr;
        entity.IsActive = request.IsActive;

        await context.SaveChangesAsync(cancellationToken);

        return Result<long>.Success(entity.Id);
    }
}
