using OpenIdentityStack.Domain.Applications;
using SharedKernel;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.Applications.Commands;

public sealed record ConfigureApplicationOAuthCommand(
    DomainApplicationId ApplicationId,
    ApplicationProfile Profile,
    OAuthClientType ClientType,
    IReadOnlyList<string> AllowedGrantTypes,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    bool RequirePkce,
    bool RequireConsent);

public interface IConfigureApplicationOAuthUseCase
{
    Task<Result<ApplicationCommandResult>> ExecuteAsync(
        ConfigureApplicationOAuthCommand command,
        CancellationToken cancellationToken = default);
}
