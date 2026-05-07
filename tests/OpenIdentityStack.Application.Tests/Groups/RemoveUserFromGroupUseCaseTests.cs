using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Groups.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Groups;
public class RemoveUserFromGroupUseCaseTests
{
    private readonly IGroupRepository _groupRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly RemoveUserFromGroupUseCase _useCase;

    public RemoveUserFromGroupUseCaseTests()
    {
        this._groupRepository = Substitute.For<IGroupRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._useCase = new RemoveUserFromGroupUseCase(this._groupRepository, this._dateTimeProvider);
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenMemberRemoved()
    {
        // Arrange
        var groupId = GroupId.Create();
        Group group = Group.Create("test-group", "description", this._dateTimeProvider).Value;
        
        // We need to inject the ID into the group or mock the repo to return a group with that ID.
        // Since ID is private setter or strictly initialized, we can't easily set it to a specific value if GroupId.Create() is random.
        // We should ensure the repo returns the group we created, and use that group's ID for the command.
        
        var userId = new UserId(Guid.NewGuid());
        var command = new RemoveUserFromGroupCommand(group.Id, userId);

        // Pre-add member so we can verify removal? 
        // RemoveMember is void, checking internal state is hard without exposing it.
        // Group.Memberships is IReadOnlyCollection.
        // We can add member first.
        group.AddMember(userId, new UserId(Guid.NewGuid()), this._dateTimeProvider);

        this._groupRepository.GetByIdAsync(command.GroupId, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act
        Result result = await this._useCase.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        group.Memberships.ShouldNotContain(m => m.UserId == userId);
        await this._groupRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnSuccess_WhenMemberNotExists()
    {
        // Arrange
        Group group = Group.Create("test-group", "description", this._dateTimeProvider).Value;
        var userId = new UserId(Guid.NewGuid());
        var command = new RemoveUserFromGroupCommand(group.Id, userId);

        this._groupRepository.GetByIdAsync(command.GroupId, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act
        Result result = await this._useCase.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue(); // Idempotent
        await this._groupRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnNotFound_WhenGroupNotFound()
    {
        // Arrange
        var command = new RemoveUserFromGroupCommand(GroupId.Create(), new UserId(Guid.NewGuid()));

        this._groupRepository.GetByIdAsync(command.GroupId, Arg.Any<CancellationToken>())
            .Returns((Group?)null);

        // Act
        Result result = await this._useCase.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Group.NotFound");
        await this._groupRepository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
