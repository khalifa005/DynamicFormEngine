namespace KH.Application.Auth.Models;

/// <summary>
/// The outcome of a corporate SSO sign-in, once OAM has vouched for who the caller is and FSMS has
/// decided whether to let them in.
///
/// A refusal is not a failed <c>Result</c>: authentication succeeded, and the caller is owed a
/// specific explanation it can render — so the reason travels here rather than as an error.
/// </summary>
public sealed class SsoSignInResultDto
{
    /// <summary>The single-use code to trade for a token pair. Null when access was refused.</summary>
    public string? AuthorizationCode { get; init; }

    /// <summary>Why access was refused. Null when it was granted. See <c>SsoDefaults.DenialReasons</c>.</summary>
    public string? DenialReason { get; init; }

    public bool IsGranted => AuthorizationCode is not null;

    public static SsoSignInResultDto Granted(string authorizationCode) =>
        new() { AuthorizationCode = authorizationCode };

    public static SsoSignInResultDto Denied(string reason) =>
        new() { DenialReason = reason };
}
