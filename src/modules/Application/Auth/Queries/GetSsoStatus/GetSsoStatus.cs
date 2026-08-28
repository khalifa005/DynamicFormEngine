using Microsoft.Extensions.Options;
using Shared.Core.Common;
using Shared.Core.Options;

namespace KH.Application.Auth.Queries.GetSsoStatus;

/// <summary>
/// What the sign-in page needs to know before it draws itself. The server is the single source of
/// truth for whether SSO is on, so the flag is never duplicated into client config where the two
/// copies could disagree.
/// </summary>
public record GetSsoStatusQuery : IRequest<Result<SsoStatusDto>>;

public sealed class SsoStatusDto
{
    /// <summary>True when corporate SSO is the way in for back-office users.</summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// True when an Administrator may still sign in with a password while SSO is on. The login page
    /// uses this to decide whether to offer the credentials form at all.
    /// </summary>
    public bool AdministratorLocalLoginAllowed { get; init; }
}

public sealed class GetSsoStatusQueryHandler(IOptions<SsoSettings> ssoOptions)
    : IRequestHandler<GetSsoStatusQuery, Result<SsoStatusDto>>
{
    public Task<Result<SsoStatusDto>> Handle(GetSsoStatusQuery request, CancellationToken cancellationToken)
    {
        var settings = ssoOptions.Value;

        return Task.FromResult(Result<SsoStatusDto>.Success(new SsoStatusDto
        {
            Enabled = settings.Enabled,
            AdministratorLocalLoginAllowed = settings.AllowLocalLoginForAdministrators,
        }));
    }
}
