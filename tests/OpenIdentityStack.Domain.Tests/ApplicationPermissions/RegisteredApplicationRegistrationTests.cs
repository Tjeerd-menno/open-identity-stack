using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Domain.Tests.ApplicationPermissions;

public sealed class RegisteredApplicationRegistrationTests
{
    private readonly TestDateTimeProvider dateTimeProvider = new(new DateTimeOffset(2026, 1, 18, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Register_WithValidData_ReturnsActiveServiceWithPermissions()
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "orders-api",
            "Orders API",
            "Manages orders",
            "owner-1",
            OwnerType.User,
            [Permission("read-orders", "Read orders"), Permission("write-orders", "Write orders")],
            "actor-1",
            this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ApplicationIdentifier.ShouldBe("orders-api");
        result.Value.Status.ShouldBe(ApplicationLifecycleStatus.Active);
        result.Value.Permissions.Count.ShouldBe(2);
        result.Value.Permissions[0].FullPermissionKey.ShouldBe("orders-api:read-orders");
        result.Value.Permissions[0].IsAssignable.ShouldBeTrue();
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
            [Permission(" Read-Orders ", "Read orders")],
            "actor-1",
            this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ApplicationIdentifier.ShouldBe("orders-api");
        result.Value.Permissions[0].PermissionKey.ShouldBe("read-orders");
    }

    [Fact]
    public void Register_WithManifestPermissionName_PreservesPermissionNameAsFullKey()
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "patient-api",
            "Patient API",
            "1.0.0",
            "owner-1",
            OwnerType.User,
            [("read:patients", "read:patients", "Allows reading patient data", "Patients", null)],
            "actor-1",
            this.dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Permissions[0].PermissionKey.ShouldBe("read:patients");
        result.Value.Permissions[0].FullPermissionKey.ShouldBe("read:patients");
        result.Value.Permissions[0].Description.ShouldBe("Allows reading patient data");
        result.Value.Permissions[0].IntendedUse.ShouldBe("Patients");
    }

    [Fact]
    public void Register_WithManifestPermissionNameExceedingColumnLimit_ReturnsValidationError()
    {
        string permissionName = "read:" + new string('a', 60);

        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "patient-api",
            "Patient API",
            "1.0.0",
            "owner-1",
            OwnerType.User,
            [(permissionName, permissionName, "Allows reading patient data", "Patients", null)],
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
            [Permission("read-orders", "Read orders")],
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
            [Permission("read-users", "Read users")],
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
            [Permission("read-orders", "Read orders"), Permission("READ-ORDERS", "Read orders duplicate")],
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
