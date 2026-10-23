using Microsoft.Extensions.DependencyInjection;
using OpenIdentityStack.Application;
using OpenIdentityStack.Application.Resources;

namespace OpenIdentityStack.Application.Tests.Authorization;

public sealed class AdministrativeAccessDecisionCacheTests
{
    [Fact]
    public void OlderAuthorityRevisionCannotReplaceANewerDecision()
    {
        var cache = new AdministrativeAccessDecisionCache();
        ResourceTokenProjection allow = Projection("users:read");
        ResourceTokenProjection withdrawn = Projection("roles:read");
        cache.Store(1, "operator", allow);
        cache.Store(2, "operator", withdrawn);
        cache.Store(1, "operator", allow);

        cache.Find(2, "operator")!.Permissions.ShouldBe(["roles:read"]);
        cache.Find(1, "operator").ShouldBeNull();
    }

    [Fact]
    public void FullRevisionDropsCachedDecisionsInsteadOfGrowingWithoutBound()
    {
        var cache = new AdministrativeAccessDecisionCache();
        ResourceTokenProjection projection = Projection("users:read");
        for (int index = 0; index < AdministrativeAccessDecisionCache.MaximumEntries; index++)
        {
            cache.Store(3, "key-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture), projection);
        }

        cache.Find(3, "key-0").ShouldNotBeNull();
        cache.Store(3, "overflow", projection);
        cache.Find(3, "overflow")!.Permissions.ShouldBe(["users:read"]);
        cache.Find(3, "key-0").ShouldBeNull();
    }

    [Fact]
    public void AdministrativeProjectionCacheIsSharedAcrossScopes()
    {
        ServiceCollection services = new();
        services.AddApplication();
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope first = provider.CreateScope();
        using IServiceScope second = provider.CreateScope();

        first.ServiceProvider.GetRequiredService<AdministrativeAccessDecisionCache>().ShouldBeSameAs(
            second.ServiceProvider.GetRequiredService<AdministrativeAccessDecisionCache>());
    }

    private static ResourceTokenProjection Projection(string permission) =>
        new(["urn:openidentitystack:admin-api"], [permission], new Dictionary<Guid, long>());
}