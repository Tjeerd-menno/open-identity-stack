
using Microsoft.Extensions.Logging;

using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Roles;
/// <summary>
/// Unit tests for the AssignRoleUseCase.
/// </summary>
public sealed class AssignRoleUseCaseTests
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLog _auditLog;
    private readonly ILogger<AssignRoleUseCase> _logger;
    private readonly IAssignRoleUseCase _sut;

    public AssignRoleUseCaseTests()
    {
        this._userRepository = Substitute.For<IUserRepository>();
        this._roleRepository = Substitute.For<IRoleRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._auditLog = Substitute.For<IAuditLog>();
        this._logger = Substitute.For<ILogger<AssignRoleUseCase>>();
        this._dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);

        this._sut = new AssignRoleUseCase(
            this._userRepository,
            this._roleRepository,
            this._dateTimeProvider,
            this._auditLog,
            this._logger);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidUserAndRole_AssignsRole()
    {
        // Arrange
        User user = CreateUser();
        Role role = CreateRole();
        var command = new AssignRoleCommand(user.Id, role.Id, "admin-1");

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        this._roleRepository.GetByIdAsync(role.Id, Arg.Any<CancellationToken>())
            .Returns(role);
        this._roleRepository.IsRoleAssignedAsync(user.Id, role.Id, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await this._roleRepository.Received(1).AssignRoleAsync(
            Arg.Is<RoleAssignment>(a => a!.UserId == user.Id && a.RoleId == role.Id),
            Arg.Any<CancellationToken>());
        await this._roleRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentUser_ReturnsError()
    {
        // Arrange
        var userId = UserId.Create();
        Role role = CreateRole();
        var command = new AssignRoleCommand(userId, role.Id, "admin-1");

        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((User?)null);
        this._roleRepository.GetByIdAsync(role.Id, Arg.Any<CancellationToken>())
            .Returns(role);

        // Act
        Result result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentRole_ReturnsError()
    {
        // Arrange
        User user = CreateUser();
        var roleId = RoleId.Create();
        var command = new AssignRoleCommand(user.Id, roleId, "admin-1");

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        this._roleRepository.GetByIdAsync(roleId, Arg.Any<CancellationToken>())
            .Returns((Role?)null);

        // Act
        Result result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_WithDisabledRole_ReturnsError()
    {
        // Arrange
        User user = CreateUser();
        Role role = CreateDisabledRole();
        var command = new AssignRoleCommand(user.Id, role.Id, "admin-1");

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        this._roleRepository.GetByIdAsync(role.Id, Arg.Any<CancellationToken>())
            .Returns(role);

        // Act
        Result result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("Disabled");
    }

    [Fact]
    public async Task ExecuteAsync_WithAlreadyAssignedRole_ReturnsError()
    {
        // Arrange
        User user = CreateUser();
        Role role = CreateRole();
        var command = new AssignRoleCommand(user.Id, role.Id, "admin-1");

        this._userRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        this._roleRepository.GetByIdAsync(role.Id, Arg.Any<CancellationToken>())
            .Returns(role);
        this._roleRepository.IsRoleAssignedAsync(user.Id, role.Id, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        Result result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("AlreadyAssigned");
    }

    [Fact]
    public async Task ExecuteAsync_WithDisabledUser_ReturnsError()
    {
        // Arrange
        User user = CreateDisabledUser();
        if (user.Status != UserStatus.Disabled)
        {
            throw new Exception($"Setup failed. User status is {user.Status}");
        }

        Role role = CreateRole();
        var command = new AssignRoleCommand(user.Id, role.Id, "admin-1");

        this._userRepository.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(user);
        this._roleRepository.GetByIdAsync(Arg.Any<RoleId>(), Arg.Any<CancellationToken>())
            .Returns(role);

        // Act
        Result result = await this._sut.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("Disabled");
    }

    private static User CreateUser()
    {
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);

        return User.CreateLocal(
            "user@example.com",
            "Test User",
            "hashed-password",
            dateTimeProvider).Value;
    }

    private static User CreateDisabledUser()
    {
        User user = CreateUser();
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);

        // Ensure user is active before disabling, as per domain rules
        Result verifyResult = user.VerifyEmail(dateTimeProvider);
        if (verifyResult.IsFailure)
        {
            throw new Exception($"VerifyEmail failed: {verifyResult.Error.Code}");
        }

        Result disableResult = user.Disable("Disabled for testing", dateTimeProvider);
        if (disableResult.IsFailure)
        {
            throw new Exception($"Disable failed: {disableResult.Error.Code}");
        }

        return user;
    }

    private static Role CreateRole()
    {
        return Role.Create("Admin", "Administrator role").Value;
    }

    private static Role CreateDisabledRole()
    {
        Role role = Role.Create("OldRole", "Disabled role").Value;
        role.Disable();
        return role;
    }
}
