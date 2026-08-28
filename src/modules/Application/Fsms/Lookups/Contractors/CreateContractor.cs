using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants.Fsms;
using KH.Domain.Entities.Fsms.Lookups;
using Shared.Core.Common;

namespace KH.Application.Fsms.Lookups.Contractors;

[Authorize(Policy = FsmsPolicies.ManageLookups)]
public record CreateContractorCommand : IRequest<Result<int>>
{
    public string PoNumber { get; init; } = default!;
    public string NameEn { get; init; } = default!;
    public string NameAr { get; init; } = default!;
    public string? CommercialRegistration { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed class CreateContractorCommandValidator : AbstractValidator<CreateContractorCommand>
{
    public CreateContractorCommandValidator()
    {
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

public sealed class CreateContractorCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateContractorCommand, Result<int>>
{
    public async Task<Result<int>> Handle(CreateContractorCommand request, CancellationToken cancellationToken)
    {
        bool poTaken = await context.FsmsContractors
            .AnyAsync(x => x.PoNumber == request.PoNumber, cancellationToken);

        if (poTaken)
        {
            return Result<int>.Fail(
                $"A contractor with PO number '{request.PoNumber}' already exists.",
                ApiErrorCodes.ValidationError,
                httpStatusCode: 409);
        }

        var entity = new FsmsContractor
        {
            PoNumber = request.PoNumber.Trim(),
            NameEn = request.NameEn.Trim(),
            NameAr = request.NameAr.Trim(),
            CommercialRegistration = string.IsNullOrWhiteSpace(request.CommercialRegistration)
                ? null
                : request.CommercialRegistration.Trim(),
            IsActive = request.IsActive
        };

        context.FsmsContractors.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result<int>.Success(entity.Id);
    }
}
