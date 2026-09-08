using System.Collections.Immutable;
using System.Text.Json;
using OpenIddict.Abstractions;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Infrastructure.Identity;

namespace OpenIdentityStack.Infrastructure.Tests.Identity;

public sealed class OpenIddictClientLogoutMetadataResolverTests
{
    [Fact]
    public async Task ResolveAsync_RegisteredProperties_ReturnsValidatedUris()
    {
        IOpenIddictApplicationManager manager = Substitute.For<IOpenIddictApplicationManager>();
        object application = new object();
        manager.FindByClientIdAsync("portal", Arg.Any<CancellationToken>()).Returns(application);
        manager.GetPropertiesAsync(application, Arg.Any<CancellationToken>()).Returns(
            ImmutableDictionary<string, JsonElement>.Empty
                .Add(OpenIddictClientLogoutMetadataResolver.FrontChannelLogoutUriProperty, JsonDocument.Parse("\"https://portal.test/front\"").RootElement.Clone())
                .Add(OpenIddictClientLogoutMetadataResolver.BackChannelLogoutUriProperty, JsonDocument.Parse("\"https://portal.test/back\"").RootElement.Clone()));
        var resolver = new OpenIddictClientLogoutMetadataResolver(manager);

        ClientLogoutMetadata result = await resolver.ResolveAsync("portal");

        result.FrontChannelLogoutUri.ShouldBe("https://portal.test/front");
        result.BackChannelLogoutUri.ShouldBe("https://portal.test/back");
    }

    [Fact]
    public async Task ResolveAsync_MissingOrInvalidProperties_ReturnsNoUris()
    {
        IOpenIddictApplicationManager manager = Substitute.For<IOpenIddictApplicationManager>();
        object application = new object();
        manager.FindByClientIdAsync("portal", Arg.Any<CancellationToken>()).Returns(application);
        manager.GetPropertiesAsync(application, Arg.Any<CancellationToken>()).Returns(
            ImmutableDictionary<string, JsonElement>.Empty
                .Add(OpenIddictClientLogoutMetadataResolver.FrontChannelLogoutUriProperty, JsonDocument.Parse("\"javascript:alert(1)\"").RootElement.Clone()));
        var resolver = new OpenIddictClientLogoutMetadataResolver(manager);

        ClientLogoutMetadata result = await resolver.ResolveAsync("portal");

        result.FrontChannelLogoutUri.ShouldBeNull();
        result.BackChannelLogoutUri.ShouldBeNull();
    }
}
