using SharedKernel;

namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Registers OAuth/OIDC clients in the authorization server store.
/// </summary>
public interface IClientApplicationRegistrar
{
    /// <summary>
    /// Creates the OAuth client backing a service account.
    /// Returns a failure result if registration fails (e.g., duplicate client_id, unsupported grant type).
    /// </summary>
    Task<Result> RegisterApplicationAccountAsync(
        string clientId,
        string displayName,
        string clientSecret,
        IReadOnlyList<string> allowedScopes,
        IReadOnlyList<string> allowedGrantTypes,
        CancellationToken cancellationToken = default);
}
