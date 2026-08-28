using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Users.Common;
using KH.Application.Fsms.Users.Models;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Users.Queries.GetUsers;

[Authorize(Policy = FsmsPolicies.ManageUsers)]
public record GetUsersQuery : IRequest<Result<PagedResult<UserListItemDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;

    /// <summary>Matches user name or email.</summary>
    public string? SearchTerm { get; init; }

    public string? Role { get; init; }
    public bool? IsEnabled { get; init; }
}

public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    public GetUsersQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0).WithMessage("Page number must be greater than 0.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 1000).WithMessage("Page size must be between 1 and 1000.");
    }
}

public sealed class GetUsersQueryHandler(IUserAccountService accountService)
    : IRequestHandler<GetUsersQuery, Result<PagedResult<UserListItemDto>>>
{
    public async Task<Result<PagedResult<UserListItemDto>>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        var result = await accountService.GetUsersAsync(
            new UserAccountQuery
            {
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                SearchTerm = request.SearchTerm,
                Role = request.Role,
                IsEnabled = request.IsEnabled,

                // Crew logins belong to the teams screen, which raises and retires them with the
                // team itself. Showing them here would be a second, partial way to manage a team.
                ExcludeFieldTeamAccounts = true
            },
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Result<PagedResult<UserListItemDto>>.Fail(result.Errors);
        }

        var page = result.Data!;
        var items = page.Items.Select(x => x.ToListItemDto()).ToList();

        return Result<PagedResult<UserListItemDto>>.Success(
            new PagedResult<UserListItemDto>(items, page.TotalCount, page.Page, page.PageSize));
    }
}
