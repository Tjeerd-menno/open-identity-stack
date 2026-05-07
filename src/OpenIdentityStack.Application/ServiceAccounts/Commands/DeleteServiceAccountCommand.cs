namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Command to delete a service account.
/// </summary>
/// <param name="ServiceAccountId">The service account ID.</param>
public sealed record DeleteServiceAccountCommand(ServiceAccountId ServiceAccountId);

/// <summary>
/// Use case interface for deleting service accounts.
/// </summary>
public interface IDeleteServiceAccountUseCase
{
    /// <summary>
    /// Deletes a service account.
    /// </summary>
    Task<Result> ExecuteAsync(
        DeleteServiceAccountCommand command,
        CancellationToken cancellationToken = default);
}
