namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Use case interface for enabling service accounts.
/// </summary>
public interface IEnableServiceAccountUseCase
{
    /// <summary>
    /// Enables a service account.
    /// </summary>
    Task<Result> ExecuteAsync(
        EnableServiceAccountCommand command,
        CancellationToken cancellationToken = default);
}
