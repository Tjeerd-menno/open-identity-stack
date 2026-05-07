using SharedKernel;
namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Command to create a new service account.
/// </summary>
/// <param name="ClientId">The OAuth2 client_id for the service account.</param>
/// <param name="DisplayName">The display name for the service account.</param>
/// <param name="AllowedScopes">The allowed OAuth2 scopes.</param>
/// <param name="AllowedGrantTypes">The allowed OAuth2 grant types.</param>
public sealed record CreateServiceAccountCommand(
    string ClientId,
    string DisplayName,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> AllowedGrantTypes);

/// <summary>
/// Response from creating a service account.
/// </summary>
/// <param name="ServiceAccountId">The ID of the created service account.</param>
/// <param name="ClientId">The OAuth2 client_id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="InitialSecret">The initial client secret (shown only once).</param>
public sealed record CreateServiceAccountResponse(
    ServiceAccountId ServiceAccountId,
    string ClientId,
    string DisplayName,
    string InitialSecret);

/// <summary>
/// Use case interface for creating service accounts.
/// </summary>
public interface ICreateServiceAccountUseCase
{
    /// <summary>
    /// Creates a new service account with an initial secret.
    /// </summary>
    Task<Result<CreateServiceAccountResponse>> ExecuteAsync(
        CreateServiceAccountCommand command, 
        CancellationToken cancellationToken = default);
}
