using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Domain.Tests.ApplicationPermissions;

public sealed class RegisteredApplicationRegistrationTests
{
    private readonly TestDateTimeProvider dateTimeProvider = new(new DateTimeOffset(2026, 1, 18, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Register_WithResourceActionPermissionKey_PrefixesApplicationIdentifier()
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "orders-api",
            "Orders API",
            "Manages orders",
            "owner-1",
            OwnerType.User,
            [Permission("order:cancel", "Cancel order")],
            "actor-1",
            this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Permissions[0].PermissionKey.ShouldBe("order:cancel");
        result.Value.Permissions[0].FullPermissionKey.ShouldBe("orders-api:order:cancel");
    }

    [Fact]
    public void Register_WithSingleSegmentPermissionKey_ReturnsValidationError()
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "orders-api",
            "Orders API",
            "Manages orders",
            "owner-1",
            OwnerType.User,
            [Permission("read-orders", "Read orders")],
            "actor-1",
            this.dateTimeProvider);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.ApplicationPermission.PermissionKeyInvalidFormat");
    }

    [Fact]
    public void Register_WithValidData_ReturnsActiveServiceWithPermissions()
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "orders-api",
            "Orders API",
            "Manages orders",
            "owner-1",
            OwnerType.User,
            [Permission("order:read", "Read orders"), Permission("order:write", "Write orders")],
            "actor-1",
            this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ApplicationIdentifier.ShouldBe("orders-api");
        result.Value.Status.ShouldBe(ApplicationLifecycleStatus.Active);
        result.Value.Permissions.Count.ShouldBe(2);
        result.Value.Permissions[0].FullPermissionKey.ShouldBe("orders-api:order:read");
    }

    [Fact]
    public void Register_NormalizesIdentifierAndPermissionKeys()
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            " Orders-API ",
            "Orders API",
            null,
            "owner-1",
            OwnerType.User,
            [Permission(" Order:Read ", "Read orders")],
            "actor-1",
            this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ApplicationIdentifier.ShouldBe("orders-api");
        result.Value.Permissions[0].PermissionKey.ShouldBe("order:read");
    }

    [Fact]
    public void Register_WithManifestPermissionKey_PrefixesApplicationIdentifier()
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "patient-api",
            "Patient API",
            "1.0.0",
            "owner-1",
            OwnerType.User,
            [("read:patients", "read:patients", "Allows reading patient data", "Patients")],
            "actor-1",
            this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Permissions[0].PermissionKey.ShouldBe("read:patients");
        result.Value.Permissions[0].FullPermissionKey.ShouldBe("patient-api:read:patients");
        result.Value.Permissions[0].Description.ShouldBe("Allows reading patient data");
        result.Value.Permissions[0].Category.ShouldBe("Patients");
    }

    [Fact]
    public void Register_WithManifestPermissionNameExceedingColumnLimit_ReturnsValidationError()
    {
        string permissionName = "read:" + new string('a', 64);

        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "patient-api",
            "Patient API",
            "1.0.0",
            "owner-1",
            OwnerType.User,
            [(permissionName, permissionName, "Allows reading patient data", "Patients")],
            "actor-1",
            this.dateTimeProvider);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.ApplicationPermission.PermissionKeyInvalidFormat");
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("1orders")]
    [InlineData("orders_api")]
    public void Register_WithInvalidIdentifier_ReturnsValidationError(string identifier)
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            identifier,
            "Orders API",
            null,
            "owner-1",
            OwnerType.User,
            [Permission("order:read", "Read orders")],
            "actor-1",
            this.dateTimeProvider);

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Register_WithReservedIdentifier_ReturnsConflict()
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "users",
            "Users",
            null,
            "owner-1",
            OwnerType.User,
            [Permission("user:read", "Read users")],
            "actor-1",
            this.dateTimeProvider,
            ["users"]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.RegisteredApplication.IdentifierReserved");
    }

    [Fact]
    public void Register_WithDuplicatePermissionKeys_ReturnsValidationError()
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "orders-api",
            "Orders API",
            null,
            "owner-1",
            OwnerType.User,
            [Permission("order:read", "Read orders"), Permission("ORDER:READ", "Read orders duplicate")],
            "actor-1",
            this.dateTimeProvider);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.RegisteredApplication.DuplicatePermissionKeys");
    }

    [Fact]
    public void Register_WithoutPermissions_ReturnsValidationError()
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "orders-api",
            "Orders API",
            null,
            "owner-1",
            OwnerType.User,
            [],
            "actor-1",
            this.dateTimeProvider);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.RegisteredApplication.AtLeastOnePermissionRequired");
    }

    private static (string Key, string DisplayName, string? Description, string? Category) Permission(string key, string displayName)
        => (key, displayName, null, null);

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
