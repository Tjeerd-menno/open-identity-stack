using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Groups;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Groups;
/// <summary>
/// Unit tests for ListGroupMappingsQueryHandler.
/// </summary>
public sealed class ListGroupMappingsQueryHandlerTests
{
    private readonly IGroupRepository _groupRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ListGroupMappingsQueryHandler _handler;
    private static readonly DateTimeOffset TestTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

    public ListGroupMappingsQueryHandlerTests()
    {
        this._groupRepository = Substitute.For<IGroupRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(TestTime);
        this._handler = new ListGroupMappingsQueryHandler(this._groupRepository);
    }

    private Group CreateGroup(string name, string? description = null)
    {
        return Group.Create(name, description, this._dateTimeProvider).Value;
    }

    [Fact]
    public async Task HandleAsync_GroupNotFound_ReturnsError()
    {
        // Arrange
        var groupId = new GroupId(Guid.NewGuid());
        this._groupRepository
            .GetByIdAsync(groupId, Arg.Any<CancellationToken>())
            .Returns((Group?)null);

        // Act
        Result<List<GroupMappingResponse>> result = await this._handler.HandleAsync(groupId);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("NotFound");
    }

    [Fact]
    public async Task HandleAsync_GroupWithNoMappings_ReturnsEmptyList()
    {
        // Arrange
        Group group = this.CreateGroup("empty-group", "Empty");

        this._groupRepository
            .GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act
        Result<List<GroupMappingResponse>> result = await this._handler.HandleAsync(group.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_GroupWithClaimMapping_ReturnsMappingResponse()
    {
        // Arrange
        Group group = this.CreateGroup("test-group", "Test");
        group.AddMapping(MappingType.Claim, "department", "Engineering", TokenTarget.AccessToken, this._dateTimeProvider);

        this._groupRepository
            .GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act
        Result<List<GroupMappingResponse>> result = await this._handler.HandleAsync(group.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(1);
        result.Value[0].MappingType.ShouldBe(MappingType.Claim);
        result.Value[0].ClaimType.ShouldBe("department");
        result.Value[0].ClaimValue.ShouldBe("Engineering");
        result.Value[0].TokenTarget.ShouldBe(TokenTarget.AccessToken);
    }

    [Fact]
    public async Task HandleAsync_GroupWithRoleMapping_ReturnsMappingWithRoleId()
    {
        // Arrange
        Group group = this.CreateGroup("admin-group", "Admins");

        var roleId = Guid.NewGuid();
        group.AddMapping(MappingType.Role, roleId.ToString(), null, TokenTarget.AccessToken, this._dateTimeProvider);

        this._groupRepository
            .GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act
        Result<List<GroupMappingResponse>> result = await this._handler.HandleAsync(group.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(1);
        result.Value[0].MappingType.ShouldBe(MappingType.Role);
        result.Value[0].TargetRoleId.ShouldBe(roleId);
        result.Value[0].ClaimType.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_GroupWithMultipleMappings_ReturnsAllMappings()
    {
        // Arrange
        Group group = this.CreateGroup("multi-group", "Multiple Mappings");

        group.AddMapping(MappingType.Claim, "claim1", "value1", TokenTarget.AccessToken, this._dateTimeProvider);
        group.AddMapping(MappingType.Claim, "claim2", "value2", TokenTarget.IdToken, this._dateTimeProvider);
        var roleId = Guid.NewGuid();
        group.AddMapping(MappingType.Role, roleId.ToString(), null, TokenTarget.Both, this._dateTimeProvider);

        this._groupRepository
            .GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act
        Result<List<GroupMappingResponse>> result = await this._handler.HandleAsync(group.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(3);
    }

    [Fact]
    public async Task HandleAsync_ReturnsDeterministicMappingIds()
    {
        // Arrange
        Group group = this.CreateGroup("id-group", "ID Test");
        group.AddMapping(MappingType.Claim, "test-claim", "test-value", TokenTarget.AccessToken, this._dateTimeProvider);

        this._groupRepository
            .GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act
        Result<List<GroupMappingResponse>> result1 = await this._handler.HandleAsync(group.Id);
        Result<List<GroupMappingResponse>> result2 = await this._handler.HandleAsync(group.Id);

        // Assert
        result1.IsSuccess.ShouldBeTrue();
        result2.IsSuccess.ShouldBeTrue();
        result1.Value[0].Id.ShouldBe(result2.Value[0].Id);
    }

    [Fact]
    public async Task HandleAsync_TokenTargetBoth_ReturnsCorrectTarget()
    {
        // Arrange
        Group group = this.CreateGroup("both-group", "Both Tokens");
        group.AddMapping(MappingType.Claim, "shared", "value", TokenTarget.Both, this._dateTimeProvider);

        this._groupRepository
            .GetByIdAsync(group.Id, Arg.Any<CancellationToken>())
            .Returns(group);

        // Act
        Result<List<GroupMappingResponse>> result = await this._handler.HandleAsync(group.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value[0].TokenTarget.ShouldBe(TokenTarget.Both);
    }
}
