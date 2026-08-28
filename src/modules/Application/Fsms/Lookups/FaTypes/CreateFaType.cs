using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Lookups;
using Shared.Core.Common;

namespace KH.Application.Fsms.Lookups.FaTypes;

[Authorize(Policy = FsmsPolicies.ManageLookups)]
public record CreateFaTypeCommand : IRequest<Result<long>>
{
    public string FaTypeCode { get; init; } = default!;
    public long TaskTypeId { get; init; }
    public string NameEn { get; init; } = default!;
    public string NameAr { get; init; } = default!;
    public bool IsActive { get; init; } = true;
}

public sealed class CreateFaTypeCommandValidator : AbstractValidator<CreateFaTypeCommand>
{
    public CreateFaTypeCommandValidator()
    {
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

public sealed class CreateFaTypeCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateFaTypeCommand, Result<long>>
{
    public async Task<Result<long>> Handle(CreateFaTypeCommand request, CancellationToken cancellationToken)
    {
        var codeTaken = await context.FsmsFaTypes
            .AnyAsync(x => x.FaTypeCode == request.FaTypeCode, cancellationToken);

        if (codeTaken)
        {
            return Result<long>.Fail(
                $"An FA type with code '{request.FaTypeCode}' already exists.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        var entity = new FsmsFaType
        {
            FaTypeCode = request.FaTypeCode,
            TaskTypeId = request.TaskTypeId,
            NameEn = request.NameEn,
            NameAr = request.NameAr,
            IsActive = request.IsActive
        };

        context.FsmsFaTypes.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result<long>.Success(entity.Id);
    }
}
