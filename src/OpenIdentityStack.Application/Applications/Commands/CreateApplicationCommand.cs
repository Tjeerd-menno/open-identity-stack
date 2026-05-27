using OpenIdentityStack.Domain.Applications;
using SharedKernel;

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

public interface ICreateApplicationUseCase
{
    Task<Result<ApplicationCommandResult>> ExecuteAsync(
        CreateApplicationCommand command,
        CancellationToken cancellationToken = default);
}
