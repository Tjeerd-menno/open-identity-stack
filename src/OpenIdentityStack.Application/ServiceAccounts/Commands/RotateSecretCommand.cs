using SharedKernel;
namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Command to rotate a service account's secret.
/// </summary>
/// <param name="ServiceAccountId">The service account ID.</param>
/// <param name="RevokeExisting">Whether to revoke all existing credentials.</param>
/// <param name="Description">Optional description for the new credential.</param>
/// <param name="ExpiresAt">Optional expiry date for the new credential.</param>
public sealed record RotateSecretCommand(
    ServiceAccountId ServiceAccountId,
    bool RevokeExisting,
    string? Description,
    DateTimeOffset? ExpiresAt);

/// <summary>
/// Response from rotating a service account secret.
/// </summary>
/// <param name="CredentialId">The ID of the new credential.</param>
/// <param name="NewSecret">The new secret (shown only once).</param>
public sealed record RotateSecretResponse(
    Guid CredentialId,
    string NewSecret);

/// <summary>
/// Use case interface for rotating service account secrets.
/// </summary>
public interface IRotateSecretUseCase
{
    /// <summary>
    /// Rotates the secret for a service account.
    /// </summary>
    Task<Result<RotateSecretResponse>> ExecuteAsync(
        RotateSecretCommand command, 
        CancellationToken cancellationToken = default);
}
