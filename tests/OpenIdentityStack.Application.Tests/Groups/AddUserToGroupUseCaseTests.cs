using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Groups.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Groups;

public class AddUserToGroupUseCaseTests
{
    private readonly IGroupRepository _groupRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly AddUserToGroupUseCase _useCase;

    public AddUserToGroupUseCaseTests()
    {
        this._groupRepository = Substitute.For<IGroupRepository>();
        this._userRepository = Substitute.For<IUserRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._useCase = new AddUserToGroupUseCase(this._groupRepository, this._userRepository, this._dateTimeProvider, Substitute.For<IAdministrativeApproval>(), new OpenIdentityStack.Application.Authorization.UnrestrictedGrantPolicy(Substitute.For<IRoleRepository>()));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldAddUser_WhenGroupAndUserExist()
    {
        // Arrange
        var groupId = GroupId.Create();
        var userId = UserId.Create();
        var assignedBy = UserId.Create();
        var command = new AddUserToGroupCommand(groupId, userId, assignedBy);

        Group group = Group.Create("TestGroup", null, this._dateTimeProvider).Value;
        User user = User.CreateLocal("user@example.com", "User", "hash", this._dateTimeProvider).Value;

        this._groupRepository.GetByIdAsync(groupId, Arg.Any<CancellationToken>()).Returns(group);
        this._userRepository.GetByIdAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        // Act
        Result result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        group.Memberships.ShouldContain(m => m.UserId == userId && m.AssignedBy == assignedBy);
        await this._groupRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFail_WhenGroupNotFound()
    {
        // Arrange
        var command = new AddUserToGroupCommand(GroupId.Create(), UserId.Create(), UserId.Create());

        this._dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow); // Ensure it's configured
        User user = User.CreateLocal("user@example.com", "User", "hash", this._dateTimeProvider).Value;

        this._groupRepository.GetByIdAsync(command.GroupId, Arg.Any<CancellationToken>()).Returns((Group?)null);
        this._userRepository.GetByIdAsync(command.UserId, Arg.Any<CancellationToken>()).Returns(user);

        // Act
        Result result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Group.NotFound");
    }
}
