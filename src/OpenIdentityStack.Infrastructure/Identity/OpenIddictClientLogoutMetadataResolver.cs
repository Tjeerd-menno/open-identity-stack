using OpenIddict.Abstractions;
using OpenIdentityStack.Application.Abstractions;

namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>Reads standard OIDC logout metadata from registered application properties.</summary>
public sealed class OpenIddictClientLogoutMetadataResolver : IClientLogoutMetadataResolver
{
    public const string FrontChannelLogoutUriProperty = "frontchannel_logout_uri";
    public const string BackChannelLogoutUriProperty = "backchannel_logout_uri";

    private readonly IOpenIddictApplicationManager applicationManager;

    public OpenIddictClientLogoutMetadataResolver(IOpenIddictApplicationManager applicationManager)
    {
        this.applicationManager = applicationManager;
    }

    public async Task<ClientLogoutMetadata> ResolveAsync(string clientId, CancellationToken cancellationToken = default)
    {
        object? application = await this.applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (application is null)
        {
            return new ClientLogoutMetadata(null, null);
        }

        System.Collections.Immutable.ImmutableDictionary<string, System.Text.Json.JsonElement> properties =
            await this.applicationManager.GetPropertiesAsync(application, cancellationToken);
        return new ClientLogoutMetadata(
            ReadUri(properties, FrontChannelLogoutUriProperty),
            ReadUri(properties, BackChannelLogoutUriProperty));
    }

    private static string? ReadUri(
        System.Collections.Immutable.ImmutableDictionary<string, System.Text.Json.JsonElement> properties,
        string propertyName)
    {
        if (!properties.TryGetValue(propertyName, out System.Text.Json.JsonElement value) || value.ValueKind != System.Text.Json.JsonValueKind.String)
        {
            return null;
        }

        string? uri = value.GetString();
        return Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed) &&
               (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp)
            ? uri
            : null;
    }
}
