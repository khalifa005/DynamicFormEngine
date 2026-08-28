using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Lookups.TaskTypes;

[Authorize(Policy = FsmsPolicies.ManageLookups)]
public record UpdateTaskTypeCommand : IRequest<Result<long>>
{
    public long Id { get; init; }
    public string Code { get; init; } = default!;
    public string NameEn { get; init; } = default!;
    public string NameAr { get; init; } = default!;
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateTaskTypeCommandValidator : AbstractValidator<UpdateTaskTypeCommand>
{
    public UpdateTaskTypeCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0L).WithMessage("Task type id is required.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Task type code is required.")
            .MaximumLength(50).WithMessage("Task type code must not exceed 50 characters.");

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage("English name is required.")
            .MaximumLength(250).WithMessage("English name must not exceed 250 characters.");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("Arabic name is required.")
            .MaximumLength(250).WithMessage("Arabic name must not exceed 250 characters.");
    }
}

public sealed class UpdateTaskTypeCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateTaskTypeCommand, Result<long>>
{
    public async Task<Result<long>> Handle(UpdateTaskTypeCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.FsmsTaskTypes.FindAsync([request.Id], cancellationToken);

        if (entity is null)
            return Result<long>.Fail("Task type not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);

        if (!string.Equals(entity.Code, request.Code, StringComparison.OrdinalIgnoreCase))
        {
            var codeTaken = await context.FsmsTaskTypes
                .AnyAsync(x => x.Code == request.Code && x.Id != request.Id, cancellationToken);

            if (codeTaken)
            {
                return Result<long>.Fail(
                    $"A task type with code '{request.Code}' already exists.",
                    ApiErrorCodes.ValidationError,
                    httpStatusCode: 409);
            }
        }

        entity.Code = request.Code;
        entity.NameEn = request.NameEn;
        entity.NameAr = request.NameAr;
        entity.IsActive = request.IsActive;

        await context.SaveChangesAsync(cancellationToken);

        return Result<long>.Success(entity.Id);
    }
}
