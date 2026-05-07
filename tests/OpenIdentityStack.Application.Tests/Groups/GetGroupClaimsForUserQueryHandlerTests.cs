using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Groups;
/// <summary>
/// Unit tests for GetGroupClaimsForUserQueryHandler.
/// </summary>
public sealed class GetGroupClaimsForUserQueryHandlerTests
{
    private readonly IGroupRepository _groupRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly GetGroupClaimsForUserQueryHandler _handler;
    private static readonly DateTimeOffset TestTime = new(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);

    public GetGroupClaimsForUserQueryHandlerTests()
    {
        this._groupRepository = Substitute.For<IGroupRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(TestTime);
        this._handler = new GetGroupClaimsForUserQueryHandler(this._groupRepository);
    }

    private Group CreateGroup(string name, string? description = null)
    {
        return Group.Create(name, description, this._dateTimeProvider).Value;
    }

    [Fact]
    public async Task HandleAsync_UserWithNoGroups_ReturnsEmptyList()
    {
        // Arrange
        var userId = UserId.Create();
        this._groupRepository
            .GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        Result<IReadOnlyList<GroupClaimDto>> result = await this._handler.HandleAsync(userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_GroupWithNoMappings_ReturnsEmptyList()
    {
        // Arrange
        var userId = UserId.Create();
        Group group = this.CreateGroup("test-group", "Test Group");

        this._groupRepository
            .GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns([group]);

        // Act
        Result<IReadOnlyList<GroupClaimDto>> result = await this._handler.HandleAsync(userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_GroupWithClaimMappings_ReturnsClaimDtos()
    {
        // Arrange
        var userId = UserId.Create();
        Group group = this.CreateGroup("dev-group", "Developers");

        // Add claim mappings
        group.AddMapping(MappingType.Claim, "department", "Engineering", TokenTarget.AccessToken, this._dateTimeProvider);
        group.AddMapping(MappingType.Claim, "team", "Backend", TokenTarget.IdToken, this._dateTimeProvider);

        this._groupRepository
            .GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns([group]);

        // Act
        Result<IReadOnlyList<GroupClaimDto>> result = await this._handler.HandleAsync(userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value.ShouldContain(c => c.Type == "department" && c.Value == "Engineering");
        result.Value.ShouldContain(c => c.Type == "team" && c.Value == "Backend");
    }

    [Fact]
    public async Task HandleAsync_GroupWithRoleMapping_DoesNotReturnRoleMappings()
    {
        // Arrange
        var userId = UserId.Create();
        Group group = this.CreateGroup("admin-group", "Admins");

        // Add role mapping (should not be returned as claims)
        var roleId = Guid.NewGuid();
        group.AddMapping(MappingType.Role, roleId.ToString(), null, TokenTarget.AccessToken, this._dateTimeProvider);

        this._groupRepository
            .GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns([group]);

        // Act
        Result<IReadOnlyList<GroupClaimDto>> result = await this._handler.HandleAsync(userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_MultipleGroups_AggregatesClaimsFromAllGroups()
    {
        // Arrange
        var userId = UserId.Create();

        Group group1 = this.CreateGroup("group1", "Group 1");
        group1.AddMapping(MappingType.Claim, "org", "Org1", TokenTarget.AccessToken, this._dateTimeProvider);

        Group group2 = this.CreateGroup("group2", "Group 2");
        group2.AddMapping(MappingType.Claim, "division", "Division1", TokenTarget.IdToken, this._dateTimeProvider);

        this._groupRepository
            .GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns([group1, group2]);

        // Act
        Result<IReadOnlyList<GroupClaimDto>> result = await this._handler.HandleAsync(userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value.ShouldContain(c => c.Type == "org");
        result.Value.ShouldContain(c => c.Type == "division");
    }

    [Fact]
    public async Task HandleAsync_ClaimWithNullValue_IsNotIncluded()
    {
        // Arrange
        var userId = UserId.Create();
        Group group = this.CreateGroup("test-group", "Test");

        // Add claim mapping with null value (should be filtered out)
        group.AddMapping(MappingType.Claim, "empty-claim", null, TokenTarget.AccessToken, this._dateTimeProvider);

        this._groupRepository
            .GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns([group]);

        // Act
        Result<IReadOnlyList<GroupClaimDto>> result = await this._handler.HandleAsync(userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_PreservesTokenTarget()
    {
        // Arrange
        var userId = UserId.Create();
        Group group = this.CreateGroup("test-group", "Test");

        group.AddMapping(MappingType.Claim, "access-claim", "value1", TokenTarget.AccessToken, this._dateTimeProvider);
        group.AddMapping(MappingType.Claim, "id-claim", "value2", TokenTarget.IdToken, this._dateTimeProvider);
        group.AddMapping(MappingType.Claim, "both-claim", "value3", TokenTarget.Both, this._dateTimeProvider);

        this._groupRepository
            .GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns([group]);

        // Act
        Result<IReadOnlyList<GroupClaimDto>> result = await this._handler.HandleAsync(userId);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Single(c => c.Type == "access-claim").TokenTarget.ShouldBe(TokenTarget.AccessToken);
        result.Value.Single(c => c.Type == "id-claim").TokenTarget.ShouldBe(TokenTarget.IdToken);
        result.Value.Single(c => c.Type == "both-claim").TokenTarget.ShouldBe(TokenTarget.Both);
    }
}
