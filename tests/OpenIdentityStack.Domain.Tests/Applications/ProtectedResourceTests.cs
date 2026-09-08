using OpenIdentityStack.Domain.Resources;
using SharedKernel;

namespace OpenIdentityStack.Domain.Tests.Applications;

public sealed class ProtectedResourceTests
{
    [Fact]
    public void Create_SeparatesAudienceScopeAndPermissionNamespace()
    {
        Result<ProtectedResource> result = ProtectedResource.Create("https://orders.example.com", "orders-api", "Orders", ["order-permissions"]);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldNotBe(Guid.Empty);
        result.Value.Audience.ShouldBe("https://orders.example.com");
        result.Value.Scope.ShouldBe("orders-api");
        result.Value.PermissionNamespaces.ShouldBe(["order-permissions"]);
    }

    [Theory]
    [InlineData("http://orders.example.com", "orders", "orders")]
    [InlineData("https://orders.example.com#fragment", "orders", "orders")]
    [InlineData("urn:orders", "openid", "orders")]
    [InlineData("urn:orders", "orders", "openidentitystack")]
    [InlineData("urn:openidentitystack:admin-api", "orders", "orders")]
    [InlineData("urn:orders", "ois.admin", "orders")]
    public void Create_RejectsUnsafeOrReservedResource(string audience, string scope, string permissionNamespace)
    {
        ProtectedResource.Create(audience, scope, "Orders", [permissionNamespace]).IsFailure.ShouldBeTrue();
    }
}
