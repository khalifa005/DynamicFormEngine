using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Lookups;
using Shared.Core.Common;

namespace KH.Application.Fsms.Lookups.TaskTypes;

[Authorize(Policy = FsmsPolicies.ManageLookups)]
public record CreateTaskTypeCommand : IRequest<Result<long>>
{
    public long Id { get; init; }
    public string Code { get; init; } = default!;
    public string NameEn { get; init; } = default!;
    public string NameAr { get; init; } = default!;
    public bool IsActive { get; init; } = true;
}

public sealed class CreateTaskTypeCommandValidator : AbstractValidator<CreateTaskTypeCommand>
{
    public CreateTaskTypeCommandValidator()
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

public sealed class CreateTaskTypeCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateTaskTypeCommand, Result<long>>
{
    public async Task<Result<long>> Handle(CreateTaskTypeCommand request, CancellationToken cancellationToken)
    {
        var idTaken = await context.FsmsTaskTypes
            .AnyAsync(x => x.Id == request.Id, cancellationToken);

        if (idTaken)
        {
            return Result<long>.Fail(
                $"A task type with id '{request.Id}' already exists.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        var codeTaken = await context.FsmsTaskTypes
            .AnyAsync(x => x.Code == request.Code, cancellationToken);

        if (codeTaken)
        {
            return Result<long>.Fail(
                $"A task type with code '{request.Code}' already exists.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        var entity = new FsmsTaskType
        {
            Id = request.Id,
            Code = request.Code,
            NameEn = request.NameEn,
            NameAr = request.NameAr,
            IsActive = request.IsActive
        };

        context.FsmsTaskTypes.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result<long>.Success(entity.Id);
    }
}
