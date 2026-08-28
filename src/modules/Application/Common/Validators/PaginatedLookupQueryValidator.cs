using KH.Application.Common.Models;

namespace KH.Application.Common.Validators;

public sealed class PaginatedLookupQueryValidator : AbstractValidator<PaginatedLookupQuery>
{
    private static readonly string[] AllowedSortFields =
    [
        "Id",
        "Code",
        "NameEn",
        "NameAr"
    ];

    public PaginatedLookupQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageNumber must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 1000)
            .WithMessage("PageSize must be between 1 and 1000.");

        RuleFor(x => x.SearchTerm)
            .MaximumLength(200)
            .WithMessage("SearchTerm must not exceed 200 characters.");

        RuleFor(x => x.SortBy)
            .Must(sortBy => string.IsNullOrWhiteSpace(sortBy) || AllowedSortFields.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            .WithMessage("SortBy must be one of: Id, Code, NameEn, NameAr.");

        RuleFor(x => x.SortDirection)
            .Must(direction => string.IsNullOrWhiteSpace(direction)
                || direction.Equals("asc", StringComparison.OrdinalIgnoreCase)
                || direction.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("SortDirection must be 'asc' or 'desc'.");
    }
}
