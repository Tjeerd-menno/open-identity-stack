namespace OpenIdentityStack.Application.Abstractions;

/// <summary>Resolves registered OpenID Connect logout metadata for a client.</summary>
public interface IClientLogoutMetadataResolver
{
    Task<ClientLogoutMetadata> ResolveAsync(string clientId, CancellationToken cancellationToken = default);
}

public sealed record ClientLogoutMetadata(string? FrontChannelLogoutUri, string? BackChannelLogoutUri);
