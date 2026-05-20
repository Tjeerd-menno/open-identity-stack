using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Application.Tests.Roles;

public sealed class GetUserRolesQueryHandlerTests
{
    private readonly IUserRepository userRepository;
    private readonly IRoleRepository roleRepository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly GetUserRolesQueryHandler handler;

    public GetUserRolesQueryHandlerTests()
    {
        this.userRepository = Substitute.For<IUserRepository>();
        this.roleRepository = Substitute.For<IRoleRepository>();
        this.dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 5, 20, 10, 0, 0, TimeSpan.Zero));
        this.handler = new GetUserRolesQueryHandler(this.userRepository, this.roleRepository);
    }

    [Fact]
    public async Task HandleAsync_WhenUserDoesNotExist_ReturnsNotFound()
    {
        var userId = UserId.Create();
        this.userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        Result<GetUserRolesResponse> result = await this.handler.HandleAsync(userId);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.UserNotFound");
        await this.roleRepository.DidNotReceive().GetUserRolesAsync(Arg.Any<UserId>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenUserExists_ReturnsMappedRoles()
    {
        User user = this.CreateUser();
        Role admin = Role.CreateSystemRole("admin", "Admin", "System admin").Value;
        Role support = Role.Create("support", "Support", "Support role").Value;
        support.SetPermissions(["tickets:read", "tickets:write"]);
        this.userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        this.roleRepository.GetUserRolesAsync(user.Id, false, Arg.Any<CancellationToken>())
            .Returns([admin, support]);

        Result<GetUserRolesResponse> result = await this.handler.HandleAsync(user.Id, activeOnly: false);

        result.IsSuccess.ShouldBeTrue();
        result.Value.UserId.ShouldBe(user.Id.Value);
        result.Value.Roles.Count.ShouldBe(2);
        result.Value.Roles[0].Name.ShouldBe("admin");
        result.Value.Roles[0].IsSystemRole.ShouldBeTrue();
        result.Value.Roles[1].Name.ShouldBe("support");
        result.Value.Roles[1].Permissions.ShouldBe(["tickets:read", "tickets:write"]);
    }

    [Fact]
    public async Task HandleAsync_PassesActiveOnlyAndCancellationToken()
    {
        User user = this.CreateUser();
        using var cts = new CancellationTokenSource();
        this.userRepository.GetByIdAsync(user.Id, cts.Token).Returns(user);
        this.roleRepository.GetUserRolesAsync(user.Id, true, cts.Token).Returns([]);

        await this.handler.HandleAsync(user.Id, activeOnly: true, cts.Token);

        await this.userRepository.Received(1).GetByIdAsync(user.Id, cts.Token);
        await this.roleRepository.Received(1).GetUserRolesAsync(user.Id, true, cts.Token);
    }

    private User CreateUser()
    {
        return User.CreateLocal(
            "user@example.com",
            "Test User",
            "hashed-password",
            this.dateTimeProvider).Value;
    }
}
