namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Command to disable a service account.
/// </summary>
/// <param name="ServiceAccountId">The service account ID.</param>
public sealed record DisableServiceAccountCommand(ServiceAccountId ServiceAccountId);
