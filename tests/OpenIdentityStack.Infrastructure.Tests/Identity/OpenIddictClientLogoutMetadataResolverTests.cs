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
                .Add(OpenIddictClientLogoutMetadataResolver.FrontChannelLogoutUriProperty, JsonElement.Parse("\"https://portal.test/front\""))
                .Add(OpenIddictClientLogoutMetadataResolver.BackChannelLogoutUriProperty, JsonElement.Parse("\"https://portal.test/back\"")));
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
                .Add(OpenIddictClientLogoutMetadataResolver.FrontChannelLogoutUriProperty, JsonElement.Parse("\"javascript:alert(1)\"")));
        var resolver = new OpenIddictClientLogoutMetadataResolver(manager);

        ClientLogoutMetadata result = await resolver.ResolveAsync("portal");

        result.FrontChannelLogoutUri.ShouldBeNull();
        result.BackChannelLogoutUri.ShouldBeNull();
    }
}
