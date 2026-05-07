
using System.Reflection;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Groups.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Groups;
public sealed class DeleteGroupUseCaseTests
{
    private readonly IGroupRepository _groupRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly DeleteGroupUseCase _useCase;

    public DeleteGroupUseCaseTests()
    {
        this._groupRepository = Substitute.For<IGroupRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);
        this._useCase = new DeleteGroupUseCase(this._groupRepository);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenGroupMissing()
    {
        // Arrange
        var command = new DeleteGroupCommand(GroupId.Create());
        this._groupRepository.GetByIdAsync(command.Id, Arg.Any<CancellationToken>())
            .Returns((Group?)null);

        // Act
        Result result = await this._useCase.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Group.NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenGroupHasChildren()
    {
        // Arrange
        Group parent = Group.Create("Parent", "Parent group", this._dateTimeProvider).Value;
        Group child = Group.Create("Child", "Child group", this._dateTimeProvider).Value;

        FieldInfo? childGroupsField = typeof(Group).GetField("childGroups", BindingFlags.NonPublic | BindingFlags.Instance);
        var childGroups = (List<Group>)childGroupsField!.GetValue(parent)!;
        childGroups.Add(child);

        var command = new DeleteGroupCommand(parent.Id);
        this._groupRepository.GetByIdAsync(command.Id, Arg.Any<CancellationToken>())
            .Returns(parent);

        // Act
        Result result = await this._useCase.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Group.HasChildren");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRemoveGroup_WhenNoChildren()
    {
        // Arrange
        Group group = Group.Create("Group", "Test", this._dateTimeProvider).Value;
        var command = new DeleteGroupCommand(group.Id);

        this._groupRepository.GetByIdAsync(command.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act
        Result result = await this._useCase.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        this._groupRepository.Received(1).Remove(group);
        await this._groupRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
