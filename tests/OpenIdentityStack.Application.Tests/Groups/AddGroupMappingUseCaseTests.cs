using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Groups.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Groups;
/// <summary>
/// Unit tests for the AddGroupMappingUseCase.
/// </summary>
public sealed class AddGroupMappingUseCaseTests
{
    private readonly IGroupRepository _groupRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly AddGroupMappingUseCase _useCase;
    private static readonly DateTimeOffset TestTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

    public AddGroupMappingUseCaseTests()
    {
        this._groupRepository = Substitute.For<IGroupRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(TestTime);

        this._useCase = new AddGroupMappingUseCase(this._groupRepository, this._dateTimeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_GroupNotFound_ReturnsNotFoundError()
    {
        // Arrange
        var groupId = new GroupId(Guid.NewGuid());
        var command = new AddGroupMappingCommand(
            groupId,
            MappingType.Role,
            "Admin",
            null,
            TokenTarget.AccessToken);

        this._groupRepository.GetByIdAsync(groupId, Arg.Any<CancellationToken>())
            .Returns((Group?)null);

        // Act
        Result result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Group.NotFound");
    }

    [Fact]
    public async Task ExecuteAsync_AddRoleMapping_ReturnsSuccess()
    {
        // Arrange
        Group group = this.CreateTestGroup();
        var command = new AddGroupMappingCommand(
            group.Id,
            MappingType.Role,
            "Admin",
            null,
            TokenTarget.AccessToken);

        this._groupRepository.GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act
        Result result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        group.Mappings.ShouldContain(m => m.Type == MappingType.Role && m.Target == "Admin");
        await this._groupRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_AddClaimMapping_ReturnsSuccess()
    {
        // Arrange
        Group group = this.CreateTestGroup();
        var command = new AddGroupMappingCommand(
            group.Id,
            MappingType.Claim,
            "department",
            "engineering",
            TokenTarget.IdToken);

        this._groupRepository.GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act
        Result result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        group.Mappings.ShouldContain(m =>
            m.Type == MappingType.Claim &&
            m.Target == "department" &&
            m.Value == "engineering");
    }

    [Fact]
    public async Task ExecuteAsync_InvalidTarget_ReturnsValidationError()
    {
        // Arrange
        Group group = this.CreateTestGroup();
        var command = new AddGroupMappingCommand(
            group.Id,
            MappingType.Role,
            "", // Invalid empty target
            null,
            TokenTarget.AccessToken);

        this._groupRepository.GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act
        Result result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("Invalid");
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationToken()
    {
        // Arrange
        Group group = this.CreateTestGroup();
        var command = new AddGroupMappingCommand(
            group.Id,
            MappingType.Role,
            "Admin",
            null,
            TokenTarget.AccessToken);

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
    public async Task ExecuteAsync_BothTokenTarget_AddsMappingCorrectly()
    {
        // Arrange
        Group group = this.CreateTestGroup();
        var command = new AddGroupMappingCommand(
            group.Id,
            MappingType.Claim,
            "custom_claim",
            "custom_value",
            TokenTarget.Both);

        this._groupRepository.GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act
        Result result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        group.Mappings.ShouldContain(m => m.TokenTarget == TokenTarget.Both);
    }

    private Group CreateTestGroup()
    {
        Result<Group> result = Group.Create(
            "test-group",
            "Test Group",
            this._dateTimeProvider);

        return result.Value;
    }
}
