using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Application.Fsms.Users.Common;
using KH.Application.Fsms.Users.Models;
using KH.Domain.Constants.Fsms;
using Shared.Core.Common;

namespace KH.Application.Fsms.Users.Queries.GetUserById;

[Authorize(Policy = FsmsPolicies.ManageUsers)]
public record GetUserByIdQuery : IRequest<Result<UserDetailDto>>
{
    public string UserId { get; init; } = default!;
}

public sealed class GetUserByIdQueryHandler(IUserAccountService accountService, IApplicationDbContext context)
    : IRequestHandler<GetUserByIdQuery, Result<UserDetailDto>>
{
    public async Task<Result<UserDetailDto>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var result = await accountService.GetUserAsync(request.UserId, cancellationToken);

        if (!result.IsSuccess)
        {
            return Result<UserDetailDto>.Fail(result.Errors);
        }

        var scopes = await context.LoadScopesAsync(request.UserId, cancellationToken);

        return Result<UserDetailDto>.Success(result.Data!.ToDetailDto(scopes));
    }
}
