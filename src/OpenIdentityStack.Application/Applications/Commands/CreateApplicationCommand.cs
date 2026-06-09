using OpenIdentityStack.Domain.Applications;
namespace OpenIdentityStack.Application.Applications.Commands;

public sealed record CreateApplicationCommand(
    string ClientId,
    string DisplayName,
    string? Description,
    ApplicationProfile Profile,
    OAuthClientType ClientType,
    IReadOnlyList<string> AllowedGrantTypes,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    bool RequirePkce,
    bool RequireConsent);

public sealed record CreateApplicationInitialSecretCommand(
    string? Description,
    DateTimeOffset? ExpiresAt);
