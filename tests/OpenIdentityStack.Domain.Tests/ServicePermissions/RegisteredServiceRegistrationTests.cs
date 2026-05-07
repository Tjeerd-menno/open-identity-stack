using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServicePermissions;

namespace OpenIdentityStack.Domain.Tests.ServicePermissions;

public sealed class RegisteredServiceRegistrationTests
{
    private readonly TestDateTimeProvider dateTimeProvider = new(new DateTimeOffset(2026, 1, 18, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Register_WithValidData_ReturnsActiveServiceWithPermissions()
    {
        Result<RegisteredService> result = RegisteredService.Register(
            "orders-api",
            "Orders API",
            "Manages orders",
            "owner-1",
            OwnerType.User,
            [Permission("read-orders", "Read orders"), Permission("write-orders", "Write orders")],
            "actor-1",
            this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ServiceIdentifier.ShouldBe("orders-api");
        result.Value.Status.ShouldBe(ServiceLifecycleStatus.Active);
        result.Value.Permissions.Count.ShouldBe(2);
        result.Value.Permissions[0].FullPermissionKey.ShouldBe("orders-api:read-orders");
        result.Value.Permissions[0].IsAssignable.ShouldBeTrue();
    }

    [Fact]
    public void Register_NormalizesIdentifierAndPermissionKeys()
    {
        Result<RegisteredService> result = RegisteredService.Register(
            " Orders-API ",
            "Orders API",
            null,
            "owner-1",
            OwnerType.User,
            [Permission(" Read-Orders ", "Read orders")],
            "actor-1",
            this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ServiceIdentifier.ShouldBe("orders-api");
        result.Value.Permissions[0].PermissionKey.ShouldBe("read-orders");
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("1orders")]
    [InlineData("orders_api")]
    public void Register_WithInvalidIdentifier_ReturnsValidationError(string identifier)
    {
        Result<RegisteredService> result = RegisteredService.Register(
            identifier,
            "Orders API",
            null,
            "owner-1",
            OwnerType.User,
            [Permission("read-orders", "Read orders")],
            "actor-1",
            this.dateTimeProvider);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Register_WithReservedIdentifier_ReturnsConflict()
    {
        Result<RegisteredService> result = RegisteredService.Register(
            "users",
            "Users",
            null,
            "owner-1",
            OwnerType.User,
            [Permission("read-users", "Read users")],
            "actor-1",
            this.dateTimeProvider,
            ["users"]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.RegisteredService.IdentifierReserved");
    }

    [Fact]
    public void Register_WithDuplicatePermissionKeys_ReturnsValidationError()
    {
        Result<RegisteredService> result = RegisteredService.Register(
            "orders-api",
            "Orders API",
            null,
            "owner-1",
            OwnerType.User,
            [Permission("read-orders", "Read orders"), Permission("READ-ORDERS", "Read orders duplicate")],
            "actor-1",
            this.dateTimeProvider);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.RegisteredService.DuplicatePermissionKeys");
    }

    [Fact]
    public void Register_WithoutPermissions_ReturnsValidationError()
    {
        Result<RegisteredService> result = RegisteredService.Register(
            "orders-api",
            "Orders API",
            null,
            "owner-1",
            OwnerType.User,
            [],
            "actor-1",
            this.dateTimeProvider);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.RegisteredService.AtLeastOnePermissionRequired");
    }

    private static (string Key, string DisplayName, string? Description, string? IntendedUse, string? DocUrl) Permission(string key, string displayName)
        => (key, displayName, null, null, null);

    private sealed class TestDateTimeProvider : IDateTimeProvider
    {
        public TestDateTimeProvider(DateTimeOffset now)
        {
            this.UtcNow = now;
            this.Now = now;
        }

        public DateTimeOffset UtcNow { get; }

        public DateTimeOffset Now { get; }
    }
}
