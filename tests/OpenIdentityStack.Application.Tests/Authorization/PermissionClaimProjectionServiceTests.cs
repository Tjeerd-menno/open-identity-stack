using System.Security.Claims;

using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Domain.ApplicationPermissions;
using OpenIdentityStack.Domain.Common;

using SharedKernel;

namespace OpenIdentityStack.Application.Tests.Authorization;

public sealed class PermissionClaimProjectionServiceTests
{
    private readonly IApplicationPermissionRegistryRepository repository = Substitute.For<IApplicationPermissionRegistryRepository>();
    private readonly IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly PermissionClaimProjectionService service;

    public PermissionClaimProjectionServiceTests()
    {
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 1, 18, 12, 0, 0, TimeSpan.Zero));
        this.service = new PermissionClaimProjectionService(this.repository);
    }

    [Fact]
    public async Task ExpandAssignedPermissionsAsync_ExpandsPlatformWildcardToConcretePermissions()
    {
        IReadOnlyList<string> projected = await this.service.ExpandAssignedPermissionsAsync([Permissions.Users.All]);

        projected.ShouldContain(Permissions.Users.Read);
        projected.ShouldContain(Permissions.Users.Write);
        projected.ShouldNotContain(Permissions.Users.All);
    }

    [Fact]
    public async Task ExpandAssignedPermissionsAsync_ExpandsDynamicWildcardForActiveRegisteredApplication()
    {
        RegisteredApplication application = CreateApplication(ApplicationLifecycleStatus.Active, ["order:read", "order:write", "shipment:read"]);
        this.repository.GetByIdentifierAsync("orders-api", Arg.Any<CancellationToken>())
            .Returns(application);

        IReadOnlyList<string> projected = await this.service.ExpandAssignedPermissionsAsync(["orders-api:order:*"]);

        projected.ShouldBe(["orders-api:order:read", "orders-api:order:write"]);
    }

    [Theory]
    [InlineData(ApplicationLifecycleStatus.Disabled)]
    [InlineData(ApplicationLifecycleStatus.Retired)]
    public async Task ExpandAssignedPermissionsAsync_FailsClosedWhenDynamicWildcardApplicationIsInactive(ApplicationLifecycleStatus status)
    {
        RegisteredApplication application = CreateApplication(status, ["order:read"]);
        this.repository.GetByIdentifierAsync("orders-api", Arg.Any<CancellationToken>())
            .Returns(application);

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => this.service.ExpandAssignedPermissionsAsync(["orders-api:order:*"]));

        exception.Message.ShouldContain("orders-api:order:*");
    }

    [Fact]
    public async Task ExpandAssignedPermissionsAsync_FailsClosedWhenDynamicWildcardApplicationIsMissing()
    {
        this.repository.GetByIdentifierAsync("orders-api", Arg.Any<CancellationToken>())
            .Returns((RegisteredApplication?)null);

        await Should.ThrowAsync<InvalidOperationException>(
            () => this.service.ExpandAssignedPermissionsAsync(["orders-api:order:*"]));
    }

    [Fact]
    public void FilterPermissionsForCaller_KeepsGlobalWildcardAndCallerPrefixedPermissionsOnly()
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity(
        [
            new Claim("permission", "* orders-api:order:read inventory-api:stock:read"),
            new Claim("permissions", "orders-api:order:write")
        ]));

        IReadOnlyList<string> projected = this.service.FilterPermissionsForCaller(
            PermissionClaimProjectionService.GetPermissionClaims(principal),
            "orders-api");

        projected.ShouldBe(["*", "orders-api:order:read", "orders-api:order:write"]);
    }

    private RegisteredApplication CreateApplication(ApplicationLifecycleStatus status, IReadOnlyList<string> permissionKeys)
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "orders-api",
            "Orders API",
            null,
            "owner-1",
            OwnerType.User,
            permissionKeys.Select(key => (key, key, (string?)null, (string?)null)),
            "actor-1",
            this.dateTimeProvider);
        result.IsSuccess.ShouldBeTrue();

        if (status != ApplicationLifecycleStatus.Active)
        {
            result.Value.ChangeStatus(status, false, "actor-1", this.dateTimeProvider).IsSuccess.ShouldBeTrue();
        }

        return result.Value;
    }
}
