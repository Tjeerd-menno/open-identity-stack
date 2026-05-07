using OpenIdentityStack.Domain.Clients;

namespace OpenIdentityStack.Application.Clients.Commands;

/// <summary>
/// Command to delete a client.
/// </summary>
public sealed record DeleteClientCommand(ClientId ClientId);
