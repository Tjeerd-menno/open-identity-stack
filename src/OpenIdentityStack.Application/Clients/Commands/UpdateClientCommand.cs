using OpenIdentityStack.Domain.Clients;

namespace OpenIdentityStack.Application.Clients.Commands;

/// <summary>
/// Command to update an existing client's basic information.
/// </summary>
public sealed record UpdateClientCommand(
    ClientId ClientId,
    string DisplayName,
    string? Description);

/// <summary>
/// Response from updating a client.
/// </summary>
public sealed record UpdateClientResult(
    ClientId ClientId,
    string DisplayName,
    string? Description);
