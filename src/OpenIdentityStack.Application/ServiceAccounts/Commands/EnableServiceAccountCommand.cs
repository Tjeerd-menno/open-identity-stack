namespace OpenIdentityStack.Application.ServiceAccounts.Commands;

/// <summary>
/// Command to enable a service account.
/// </summary>
/// <param name="ServiceAccountId">The service account ID.</param>
public sealed record EnableServiceAccountCommand(ServiceAccountId ServiceAccountId);
