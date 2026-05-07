using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServiceAccounts;

using SharedKernel;
namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Command to update a service account's details.
/// </summary>
/// <param name="ServiceAccountId">The ID of the service account to update.</param>
/// <param name="DisplayName">The new display name (optional).</param>
public sealed record UpdateServiceAccountCommand(ServiceAccountId ServiceAccountId, string? DisplayName);

/// <summary>
/// Result of updating a service account.
/// </summary>
/// <param name="ServiceAccountId">The ID of the updated service account.</param>
/// <param name="UpdatedAt">When the service account was updated.</param>
public sealed record UpdateServiceAccountResult(ServiceAccountId ServiceAccountId, DateTimeOffset UpdatedAt);

/// <summary>
/// Interface for the update service account use case.
/// </summary>
public interface IUpdateServiceAccountUseCase
{
    /// <summary>
    /// Updates a service account's details.
    /// </summary>
    /// <param name="command">The update service account command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the update result or an error.</returns>
    Task<Result<UpdateServiceAccountResult>> ExecuteAsync(
        UpdateServiceAccountCommand command,
        CancellationToken cancellationToken = default);
}
