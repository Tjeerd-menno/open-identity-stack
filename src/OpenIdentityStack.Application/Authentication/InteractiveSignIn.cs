using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Settings;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Application.Authentication;

internal sealed class InteractiveSignIn : IInteractiveSignIn
{
    private readonly IUserRepository users;
    private readonly ISessionRepository sessions;
    private readonly IAuthenticationSettingsRepository settings;
    private readonly IUpstreamProviderRepository providers;

    public InteractiveSignIn(
        IUserRepository users,
        ISessionRepository sessions,
        IAuthenticationSettingsRepository settings,
        IUpstreamProviderRepository providers)
    {
        this.users = users;
        this.sessions = sessions;
        this.settings = settings;
        this.providers = providers;
    }

    public Task<User?> GetUserAsync(UserId userId, CancellationToken cancellationToken = default) =>
        this.users.GetByIdAsync(userId, cancellationToken);

    public Task<UserSession?> GetSessionAsync(SessionId sessionId, CancellationToken cancellationToken = default) =>
        this.sessions.GetByIdAsync(sessionId, cancellationToken);

    public Task<AuthenticationSettings> GetOrCreateAuthenticationSettingsAsync(CancellationToken cancellationToken = default) =>
        this.settings.GetOrCreateAsync(cancellationToken);

    public Task<UpstreamProvider?> GetUpstreamProviderAsync(UpstreamProviderId providerId, CancellationToken cancellationToken = default) =>
        this.providers.GetByIdAsync(providerId, cancellationToken);

    public Task<UpstreamProvider?> GetUpstreamProviderByNameAsync(string name, CancellationToken cancellationToken = default) =>
        this.providers.GetByNameAsync(name, cancellationToken);

    public Task<IReadOnlyList<UpstreamProvider>> GetActiveUpstreamProvidersAsync(CancellationToken cancellationToken = default) =>
        this.providers.GetActiveProvidersAsync(cancellationToken);
}
