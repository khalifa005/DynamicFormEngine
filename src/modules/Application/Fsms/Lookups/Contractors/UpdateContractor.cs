using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Lookups;
using Shared.Core.Common;

namespace KH.Application.Fsms.Lookups.Contractors;

[Authorize(Policy = FsmsPolicies.ManageLookups)]
public record UpdateContractorCommand : IRequest<Result<int>>
{
    public int Id { get; init; }
    public string PoNumber { get; init; } = default!;
    public string NameEn { get; init; } = default!;
    public string NameAr { get; init; } = default!;
    public string? CommercialRegistration { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class UpdateContractorCommandValidator : AbstractValidator<UpdateContractorCommand>
{
    public UpdateContractorCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage("Contractor id is required.");

        RuleFor(x => x.PoNumber)
            .NotEmpty().WithMessage("PO number is required.")
            .MaximumLength(FsmsContractor.PoNumberMaxLength)
            .WithMessage($"PO number must not exceed {FsmsContractor.PoNumberMaxLength} characters.");

        RuleFor(x => x.NameEn)
            .NotEmpty().WithMessage("English name is required.")
            .MaximumLength(FsmsContractor.NameMaxLength)
            .WithMessage($"English name must not exceed {FsmsContractor.NameMaxLength} characters.");

        RuleFor(x => x.NameAr)
            .NotEmpty().WithMessage("Arabic name is required.")
            .MaximumLength(FsmsContractor.NameMaxLength)
            .WithMessage($"Arabic name must not exceed {FsmsContractor.NameMaxLength} characters.");

        RuleFor(x => x.CommercialRegistration)
            .MaximumLength(FsmsContractor.CommercialRegistrationMaxLength)
            .WithMessage($"Commercial registration must not exceed {FsmsContractor.CommercialRegistrationMaxLength} characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.CommercialRegistration));
    }
}

public sealed class UpdateContractorCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateContractorCommand, Result<int>>
{
    public async Task<Result<int>> Handle(UpdateContractorCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.FsmsContractors.FindAsync([request.Id], cancellationToken);

        if (entity is null)
            return Result<int>.Fail("Contractor not found.", ApiErrorCodes.NotFound, httpStatusCode: 404);

        string poNumber = request.PoNumber.Trim();

        if (!string.Equals(entity.PoNumber, poNumber, StringComparison.OrdinalIgnoreCase))
        {
            bool poTaken = await context.FsmsContractors
                .AnyAsync(x => x.PoNumber == poNumber && x.Id != request.Id, cancellationToken);

            if (poTaken)
            {
                return Result<int>.Fail(
                    $"A contractor with PO number '{poNumber}' already exists.",
                    ApiErrorCodes.ValidationError,
                    httpStatusCode: 409);
            }
        }

        entity.PoNumber = poNumber;
        entity.NameEn = request.NameEn.Trim();
        entity.NameAr = request.NameAr.Trim();
        entity.CommercialRegistration = string.IsNullOrWhiteSpace(request.CommercialRegistration)
            ? null
            : request.CommercialRegistration.Trim();
        entity.IsActive = request.IsActive;

        await context.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(entity.Id);
    }
}
