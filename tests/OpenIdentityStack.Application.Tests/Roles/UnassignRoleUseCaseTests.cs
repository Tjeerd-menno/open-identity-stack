using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Roles;
public sealed class UnassignRoleUseCaseTests
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLog _auditLog;
    private readonly UnassignRoleUseCase _useCase;

    public UnassignRoleUseCaseTests()
    {
        this._userRepository = Substitute.For<IUserRepository>();
        this._roleRepository = Substitute.For<IRoleRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._auditLog = Substitute.For<IAuditLog>();
        this._dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);
        this._useCase = new UnassignRoleUseCase(this._userRepository, this._roleRepository, this._auditLog);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenUserMissing()
    {
        // Arrange
        var command = new UnassignRoleCommand(UserId.Create(), RoleId.Create(), "admin-1");
        this._userRepository.GetByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        Result result = await this._useCase.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.UserNotFound");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenRoleNotAssigned()
    {
        // Arrange
        User user = User.CreateLocal("user@test.com", "User", "hash", this._dateTimeProvider).Value;
        var command = new UnassignRoleCommand(user.Id, RoleId.Create(), "admin-1");

        this._userRepository.GetByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(user);
        this._roleRepository.IsRoleAssignedAsync(command.UserId, command.RoleId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Result result = await this._useCase.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("NotFound.RoleNotAssigned");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRemoveAssignment_WhenRoleAssigned()
    {
        // Arrange
        User user = User.CreateLocal("user@test.com", "User", "hash", this._dateTimeProvider).Value;
        var command = new UnassignRoleCommand(user.Id, RoleId.Create(), "admin-1");

        this._userRepository.GetByIdAsync(command.UserId, Arg.Any<CancellationToken>())
            .Returns(user);
        this._roleRepository.IsRoleAssignedAsync(command.UserId, command.RoleId, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        Result result = await this._useCase.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await this._roleRepository.Received(1).RemoveRoleAssignmentAsync(command.UserId, command.RoleId, Arg.Any<CancellationToken>());
        await this._roleRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
