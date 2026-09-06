using System.Collections.Immutable;
using OpenIddict.Abstractions;
using OpenIdentityStack.DbMigrator;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class DbMigratorOpenIddictSeedTests
{
    [Fact]
    public async Task LegacySeedUpdatePreservesProjectedApplicationIdentitySetting()
    {
        IOpenIddictApplicationManager manager = Substitute.For<IOpenIddictApplicationManager>();
        object existing = new();
        manager.GetSettingsAsync(existing, Arg.Any<CancellationToken>()).Returns(
            ImmutableDictionary<string, string>.Empty
                .Add("openidentitystack:application-id", "8716f90c-cb38-4266-a0df-f5b2e2d6b122")
                .Add("existing-setting", "preserved"));
        var replacement = new OpenIddictApplicationDescriptor { ClientId = "traceable-isotopes-web" };

        await SeededOpenIddictApplicationUpdater.UpdateAsync(manager, existing, replacement);

        await manager.Received(1).UpdateAsync(existing, replacement, Arg.Any<CancellationToken>());
        replacement.Settings["openidentitystack:application-id"].ShouldBe("8716f90c-cb38-4266-a0df-f5b2e2d6b122");
        replacement.Settings["existing-setting"].ShouldBe("preserved");
    }
}
