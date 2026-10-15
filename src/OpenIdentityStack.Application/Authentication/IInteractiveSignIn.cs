using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Settings;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Application.Authentication;

/// <summary>
/// Repository reads for interactive sign-in: account login, authorize, cookie validation, and logout setup.
/// </summary>
public interface IInteractiveSignIn
{
    Task<User?> GetUserAsync(UserId userId, CancellationToken cancellationToken = default);

    Task<UserSession?> GetSessionAsync(SessionId sessionId, CancellationToken cancellationToken = default);

    Task<AuthenticationSettings> GetOrCreateAuthenticationSettingsAsync(CancellationToken cancellationToken = default);

    Task<UpstreamProvider?> GetUpstreamProviderAsync(UpstreamProviderId providerId, CancellationToken cancellationToken = default);

    Task<UpstreamProvider?> GetUpstreamProviderByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UpstreamProvider>> GetActiveUpstreamProvidersAsync(CancellationToken cancellationToken = default);
}
