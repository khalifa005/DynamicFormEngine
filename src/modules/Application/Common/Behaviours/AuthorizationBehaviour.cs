using System.Reflection;
using KH.Application.Common.Exceptions;
using KH.Application.Common.Interfaces;
using KH.Application.Common.Security;
using KH.Domain.Constants;

namespace KH.Application.Common.Behaviours;

public class AuthorizationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private readonly IUser _user;

    public AuthorizationBehaviour(IUser user)
    {
        _user = user;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var authorizeAttributes = request.GetType().GetCustomAttributes<AuthorizeAttribute>();

        if (authorizeAttributes.Any())
        {
            // Must be authenticated user
            if (_user.Id == null)
            {
                throw new UnauthorizedAccessException();
            }

            // Role-based authorization
            var authorizeAttributesWithRoles = authorizeAttributes.Where(a => !string.IsNullOrWhiteSpace(a.Roles));

            if (authorizeAttributesWithRoles.Any())
            {
                var authorized = false;

                foreach (var roles in authorizeAttributesWithRoles.Select(a => a.Roles.Split(',')))
                {
                    foreach (var role in roles)
                    {
                        if (_user.IsInRole(role.Trim()))
                        {
                            authorized = true;
                            break;
                        }
                    }
                }

                // Must be a member of at least one role in roles
                if (!authorized)
                {
                    throw new ForbiddenAccessException();
                }
            }

            // Policy-based authorization (permission claims already carried on the JWT)
            var authorizeAttributesWithPolicies = authorizeAttributes.Where(a => !string.IsNullOrWhiteSpace(a.Policy));
            if (authorizeAttributesWithPolicies.Any())
            {
                var isAdministrator = _user.IsInRole(Roles.Administrator);

                if (!isAdministrator)
                {
                    foreach (var policy in authorizeAttributesWithPolicies.Select(a => a.Policy))
                    {
                        if (!IsAuthorizedForPolicy(policy))
                        {
                            throw new ForbiddenAccessException();
                        }
                    }
                }
            }
        }

        // User is authorized / authorization not required
        return await next();
    }

    private bool IsAuthorizedForPolicy(string policy)
    {
        // "Permissions.Any:CODE_A,CODE_B" — authorized if the user holds any listed permission.
        if (policy.StartsWith(PermissionPolicies.AnyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permissionCodes = policy[PermissionPolicies.AnyPrefix.Length..]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return permissionCodes.Any(_user.HasPermission);
        }

        // A named permission policy maps 1:1 to a permission code.
        return _user.HasPermission(policy);
    }
}
