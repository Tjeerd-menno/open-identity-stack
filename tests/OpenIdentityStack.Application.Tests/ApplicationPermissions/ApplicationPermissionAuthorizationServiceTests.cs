using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.ApplicationPermissions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Domain.ApplicationPermissions;
using OpenIdentityStack.Domain.Groups;
using SharedKernel;

namespace OpenIdentityStack.Application.Tests.ApplicationPermissions;

public sealed class ApplicationPermissionAuthorizationServiceTests
{
    private readonly IPermissionChecker permissionChecker;
    private readonly IGroupRepository groupRepository;
    private readonly TestDateTimeProvider dateTimeProvider = new();
    private readonly ApplicationPermissionAuthorizationService service;

    public ApplicationPermissionAuthorizationServiceTests()
    {
        this.permissionChecker = Substitute.For<IPermissionChecker>();
        this.groupRepository = Substitute.For<IGroupRepository>();
        this.service = new ApplicationPermissionAuthorizationService(this.permissionChecker, this.groupRepository);
    }

    [Fact]
    public async Task CanManageApplicationAsync_WhenActorIsUserOwner_ReturnsTrue()
    {
        string actorId = Guid.NewGuid().ToString();
        RegisteredApplication application = this.CreateApplication(actorId, OwnerType.User);

        bool result = await this.service.CanManageApplicationAsync(actorId, application);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task CanManageApplicationAsync_WhenActorIsGroupOwnerMember_ReturnsTrue()
    {
        var actorId = UserId.Create();
        Group group = this.CreateGroupWithMember(actorId);
        RegisteredApplication application = this.CreateApplication(group.Id.Value.ToString(), OwnerType.Group);
        this.groupRepository.GetGroupsForUserAsync(actorId, Arg.Any<CancellationToken>()).Returns([group]);

        bool result = await this.service.CanManageApplicationAsync(actorId.Value.ToString(), application);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task CanManageApplicationAsync_WhenActorIsDelegatedUserMaintainer_ReturnsTrue()
    {
        string actorId = Guid.NewGuid().ToString();
        RegisteredApplication application = this.CreateApplication(Guid.NewGuid().ToString(), OwnerType.User);
        application.AddMaintainer(actorId, OwnerType.User, "owner-1", this.dateTimeProvider).IsSuccess.ShouldBeTrue();

        bool result = await this.service.CanManageApplicationAsync(actorId, application);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task CanManageApplicationAsync_WhenActorIsDelegatedGroupMaintainerMember_ReturnsTrue()
    {
        var actorId = UserId.Create();
        Group group = this.CreateGroupWithMember(actorId);
        RegisteredApplication application = this.CreateApplication(Guid.NewGuid().ToString(), OwnerType.User);
        application.AddMaintainer(group.Id.Value.ToString(), OwnerType.Group, "owner-1", this.dateTimeProvider).IsSuccess.ShouldBeTrue();
        this.groupRepository.GetGroupsForUserAsync(actorId, Arg.Any<CancellationToken>()).Returns([group]);

        bool result = await this.service.CanManageApplicationAsync(actorId.Value.ToString(), application);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task CanManageApplicationAsync_WhenActorHasWriteButNoRelationship_ReturnsFalse()
    {
        var actorId = UserId.Create();
        RegisteredApplication application = this.CreateApplication(Guid.NewGuid().ToString(), OwnerType.User);
        this.permissionChecker.HasAnyPermissionAsync(actorId, Arg.Is<IEnumerable<string>>(permissions => permissions!.Contains(Permissions.ApplicationPermissions.Admin)), Arg.Any<CancellationToken>())
            .Returns(false);

        bool result = await this.service.CanManageApplicationAsync(actorId.Value.ToString(), application);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task CanManageApplicationAsync_WhenActorHasAdminPermission_ReturnsTrue()
    {
        var actorId = UserId.Create();
        RegisteredApplication application = this.CreateApplication(Guid.NewGuid().ToString(), OwnerType.User);
        this.permissionChecker.HasAnyPermissionAsync(actorId, Arg.Is<IEnumerable<string>>(permissions => permissions!.Contains(Permissions.ApplicationPermissions.Admin)), Arg.Any<CancellationToken>())
            .Returns(true);

        bool result = await this.service.CanManageApplicationAsync(actorId.Value.ToString(), application);

        result.ShouldBeTrue();
    }

    private RegisteredApplication CreateApplication(string ownerId, OwnerType ownerType)
    {
        Result<RegisteredApplication> result = RegisteredApplication.Register(
            "orders-api",
            "Orders API",
            "Orders",
            ownerId,
            ownerType,
            [("order:read", "Read order", null, "Orders")],
            "actor-1",
            this.dateTimeProvider,
            manifestVersion: "1.0.0");

        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private Group CreateGroupWithMember(UserId userId)
    {
        Group group = Group.Create("owners", null, this.dateTimeProvider).Value;
        group.AddMember(userId, UserId.Create(), this.dateTimeProvider);
        return group;
    }

    private sealed class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => new(2026, 5, 29, 10, 0, 0, TimeSpan.Zero);
        public DateTimeOffset Now => this.UtcNow;
    }
}
