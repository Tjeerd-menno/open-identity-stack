namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Use case interface for disabling service accounts.
/// </summary>
public interface IDisableServiceAccountUseCase
{
    /// <summary>
    /// Disables a service account.
    /// </summary>
    Task<Result> ExecuteAsync(
        DisableServiceAccountCommand command,
        CancellationToken cancellationToken = default);
}
