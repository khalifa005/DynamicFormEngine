using KH.Domain.Constants;
using KH.Domain.Constants.Fsms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace KH.Infrastructure.Authorization;

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _fallbackPolicyProvider.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _fallbackPolicyProvider.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PermissionPolicies.AnyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permissionCodes = policyName[PermissionPolicies.AnyPrefix.Length..]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (permissionCodes.Length == 0)
            {
                return _fallbackPolicyProvider.GetPolicyAsync(policyName);
            }

            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new AnyPermissionRequirement(permissionCodes))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        var isKnownPermissionPolicy =
            Policies.All.Contains(policyName, StringComparer.Ordinal) ||
            FsmsPolicies.All.Contains(policyName, StringComparer.Ordinal);

        if (!isKnownPermissionPolicy)
        {
            return _fallbackPolicyProvider.GetPolicyAsync(policyName);
        }

        var permissionPolicy = new AuthorizationPolicyBuilder()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(permissionPolicy);
    }
}
