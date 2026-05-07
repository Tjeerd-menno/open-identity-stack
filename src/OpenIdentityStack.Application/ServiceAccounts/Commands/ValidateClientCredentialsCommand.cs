using SharedKernel;
namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Command to validate client credentials (client_id + client_secret).
/// </summary>
public sealed record ValidateClientCredentialsCommand(
    string ClientId,
    string ClientSecret);

/// <summary>
/// Result of validating client credentials.
/// </summary>
public sealed record ValidateClientCredentialsResult(
    ServiceAccountId ServiceAccountId,
    string ClientId,
    string DisplayName,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> AllowedGrantTypes);

/// <summary>
/// Interface for the validate client credentials use case.
/// </summary>
public interface IValidateClientCredentialsUseCase
{
    /// <summary>
    /// Validates the client credentials.
    /// </summary>
    Task<Result<ValidateClientCredentialsResult>> ExecuteAsync(
        ValidateClientCredentialsCommand command,
        CancellationToken cancellationToken = default);
}
