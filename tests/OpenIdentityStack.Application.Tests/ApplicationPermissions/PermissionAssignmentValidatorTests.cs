using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ApplicationPermissions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Domain.ApplicationPermissions;
using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Application.Tests.ApplicationPermissions;

public sealed class PermissionAssignmentValidatorTests
{
    private readonly IApplicationPermissionRegistryRepository repository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly PermissionAssignmentValidator validator;

    public PermissionAssignmentValidatorTests()
    {
        this.repository = Substitute.For<IApplicationPermissionRegistryRepository>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 1, 18, 12, 0, 0, TimeSpan.Zero));
        this.validator = new PermissionAssignmentValidator(this.repository);
    }

    [Fact]
    public async Task ValidateAssignableAsync_AllowsConcretePlatformPermission()
    {
        Result result = await this.validator.ValidateAssignableAsync([Permissions.Users.Read]);

        result.IsSuccess.ShouldBeTrue();
        await this.repository.DidNotReceive().IsPermissionAssignableAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("*")]
    [InlineData("users:*")]
    [InlineData("orders-api:order:*")]
    public async Task ValidateAssignableAsync_RequiresAcknowledgementForBroadGrant(string permission)
    {
        if (permission == "orders-api:order:*")
        {
            RegisteredApplication application = CreateApplication(ApplicationLifecycleStatus.Active, ["order:read"]);
            this.repository.GetByIdentifierAsync("orders-api", Arg.Any<CancellationToken>())
                .Returns(application);
        }

        Result result = await this.validator.ValidateAssignableAsync([permission]);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.RolePermissions.BroadGrantAcknowledgementRequired");
    }

    [Theory]
    [InlineData("*")]
    [InlineData("users:*")]
    [InlineData("orders-api:order:*")]
    public async Task ValidateAssignableAsync_AllowsBroadGrantWhenAcknowledged(string permission)
    {
        if (permission == "orders-api:order:*")
        {
            RegisteredApplication application = CreateApplication(ApplicationLifecycleStatus.Active, ["order:read"]);
            this.repository.GetByIdentifierAsync("orders-api", Arg.Any<CancellationToken>())
                .Returns(application);
        }

        Result result = await this.validator.ValidateAssignableAsync([permission], acknowledgeWildcardGrant: true);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAssignableAsync_AllowsActiveDynamicConcretePermission()
    {
        this.repository.IsPermissionAssignableAsync("orders-api:order:read", Arg.Any<CancellationToken>())
            .Returns(true);

        Result result = await this.validator.ValidateAssignableAsync(["ORDERS-API:ORDER:READ"]);

        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("orders-api:order:read")]
    [InlineData("orders-api:order:*")]
    public async Task ValidateAssignableAsync_RejectsDisabledApplicationPermissions(string permission)
    {
        RegisteredApplication application = CreateApplication(ApplicationLifecycleStatus.Disabled, ["order:read"]);
        this.repository.GetByIdentifierAsync("orders-api", Arg.Any<CancellationToken>())
            .Returns(application);
        this.repository.IsPermissionAssignableAsync("orders-api:order:read", Arg.Any<CancellationToken>())
            .Returns(false);

        Result result = await this.validator.ValidateAssignableAsync([permission], acknowledgeWildcardGrant: true);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.PermissionAssignment.PermissionUnavailable");
    }

    [Theory]
    [InlineData("orders-api:order:read")]
    [InlineData("orders-api:order:*")]
    public async Task ValidateAssignableAsync_RejectsRetiredApplicationPermissions(string permission)
    {
        RegisteredApplication application = CreateApplication(ApplicationLifecycleStatus.Retired, ["order:read"]);
        this.repository.GetByIdentifierAsync("orders-api", Arg.Any<CancellationToken>())
            .Returns(application);
        this.repository.IsPermissionAssignableAsync("orders-api:order:read", Arg.Any<CancellationToken>())
            .Returns(false);

        Result result = await this.validator.ValidateAssignableAsync([permission], acknowledgeWildcardGrant: true);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.PermissionAssignment.PermissionUnavailable");
    }

    [Theory]
    [InlineData("orders-api:*")]
    [InlineData("orders-api:order:read:extra")]
    [InlineData("orders-api:*:read")]
    [InlineData("not-a-permission")]
    public async Task ValidateAssignableAsync_RejectsMalformedOrApplicationWideDynamicPermissions(string permission)
    {
        RegisteredApplication application = CreateApplication(ApplicationLifecycleStatus.Active, ["order:read"]);
        this.repository.GetByIdentifierAsync("orders-api", Arg.Any<CancellationToken>())
            .Returns(application);

        Result result = await this.validator.ValidateAssignableAsync([permission], acknowledgeWildcardGrant: true);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation.PermissionAssignment.PermissionUnavailable");
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
