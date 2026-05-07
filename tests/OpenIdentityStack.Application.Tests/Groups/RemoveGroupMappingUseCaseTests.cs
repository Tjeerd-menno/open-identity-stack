using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Groups;
using OpenIdentityStack.Application.Groups.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Groups;
/// <summary>
/// Unit tests for the RemoveGroupMappingUseCase.
/// </summary>
public sealed class RemoveGroupMappingUseCaseTests
{
    private readonly IGroupRepository _groupRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly RemoveGroupMappingUseCase _useCase;
    private static readonly DateTimeOffset TestTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

    public RemoveGroupMappingUseCaseTests()
    {
        this._groupRepository = Substitute.For<IGroupRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(TestTime);

        this._useCase = new RemoveGroupMappingUseCase(this._groupRepository, this._dateTimeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_GroupNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var groupId = new GroupId(Guid.NewGuid());
        var command = new RemoveGroupMappingCommand(groupId, Guid.NewGuid());

        this._groupRepository.GetByIdAsync(groupId, Arg.Any<CancellationToken>())
            .Returns((Group?)null);

        // Act
        Result result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Group.NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_MappingNotFound_ReturnsNotFoundError()
    {
        // Arrange
        Group group = this.CreateTestGroup();
        var command = new RemoveGroupMappingCommand(group.Id, Guid.NewGuid()); // Non-existent mapping ID

        this._groupRepository.GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act
        Result result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("GroupMapping.NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_MappingFound_RemovesMapping()
    {
        // Arrange
        Group group = this.CreateTestGroupWithMapping();
        GroupMapping mapping = group.Mappings.First();
        Guid mappingId = mapping.GetDeterministicId();
        var command = new RemoveGroupMappingCommand(group.Id, mappingId);

        this._groupRepository.GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act
        Result result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        group.Mappings.ShouldBeEmpty();
        await this._groupRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationToken()
    {
        // Arrange
        Group group = this.CreateTestGroupWithMapping();
        GroupMapping mapping = group.Mappings.First();
        Guid mappingId = mapping.GetDeterministicId();
        var command = new RemoveGroupMappingCommand(group.Id, mappingId);

        using var cts = new CancellationTokenSource();

        this._groupRepository.GetByIdAsync(group.Id, cts.Token)
            .Returns(group);

        // Act
        await this._useCase.ExecuteAsync(command, cts.Token);

        // Assert
        await this._groupRepository.Received(1).GetByIdAsync(group.Id, cts.Token);
        await this._groupRepository.Received(1).SaveChangesAsync(cts.Token);
    }

    [Fact]
    public async Task ExecuteAsync_RemoveOneOfMultipleMappings_OnlyRemovesSpecified()
    {
        // Arrange
        Group group = this.CreateTestGroup();
        group.AddMapping(MappingType.Role, "Admin", null, TokenTarget.AccessToken, this._dateTimeProvider);
        group.AddMapping(MappingType.Role, "User", null, TokenTarget.AccessToken, this._dateTimeProvider);

        GroupMapping mappingToRemove = group.Mappings.First(m => m.Target == "Admin");
        Guid mappingId = mappingToRemove.GetDeterministicId();
        var command = new RemoveGroupMappingCommand(group.Id, mappingId);

        this._groupRepository.GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act
        Result result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        group.Mappings.Count.ShouldBe(1);
        group.Mappings.ShouldContain(m => m.Target == "User");
        group.Mappings.ShouldNotContain(m => m.Target == "Admin");
    }

    private Group CreateTestGroup()
    {
        Result<Group> result = Group.Create(
            "test-group",
            "Test Group",
            this._dateTimeProvider);

        return result.Value;
    }

    private Group CreateTestGroupWithMapping()
    {
        Group group = this.CreateTestGroup();
        group.AddMapping(MappingType.Role, "Admin", null, TokenTarget.AccessToken, this._dateTimeProvider);
        return group;
    }
}
