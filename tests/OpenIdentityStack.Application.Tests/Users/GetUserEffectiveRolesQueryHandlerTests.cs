using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Users;
public class GetUserEffectiveRolesQueryHandlerTests
{
    [Fact]
    public async Task GuidShapedRoleNameTakesPrecedenceForEntitlementsAndApproval()
    {
        Role other = Role.Create("other", null).Value;
        Role named = Role.Create(other.Id.Value.ToString(), null).Value;
        named.AddPermission("*");
        var userId = new UserId(Guid.NewGuid());
        Group group = Group.Create("mapped", null, this._dateTimeProvider).Value;
        group.AddMapping(MappingType.Role, named.Name, null, TokenTarget.Both, this._dateTimeProvider);
        this._roleRepository.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>()).Returns(Array.Empty<Role>());
        this._groupRepository.GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>()).Returns(new[] { group });
        this._roleRepository.GetByNameAsync(named.Name, Arg.Any<CancellationToken>()).Returns(named);
        this._roleRepository.GetByIdAsync(other.Id, Arg.Any<CancellationToken>()).Returns(other);
        this._roleRepository.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>()).Returns(new[] { named });
        this._roleRepository.GetByIdsAsync(Arg.Any<IEnumerable<RoleId>>(), Arg.Any<CancellationToken>()).Returns(new[] { other });

        Result<IReadOnlyList<RoleDto>> result = await this._handler.HandleAsync(userId);

        result.Value.Single().Id.ShouldBe(named.Id.Value);
        await this._roleRepository.DidNotReceive().GetByIdsAsync(Arg.Any<IEnumerable<RoleId>>(), Arg.Any<CancellationToken>());
        (await new OpenIdentityStack.Application.Authorization.UnrestrictedGrantPolicy(this._roleRepository)
            .GroupIsUnrestrictedAsync(group, CancellationToken.None)).ShouldBeTrue();
    }

    [Fact]
    public async Task HandleAsync_ShouldFallBackToIdLookup_OnlyForUnmatchedGuidShapedTargets()
    {
        Role byId = Role.Create("resolved-by-id", null).Value;
        var userId = new UserId(Guid.NewGuid());
        Group group = Group.Create("mapped", null, this._dateTimeProvider).Value;
        group.AddMapping(MappingType.Role, byId.Id.Value.ToString(), null, TokenTarget.Both, this._dateTimeProvider);
        group.AddMapping(MappingType.Role, "missing-role", null, TokenTarget.Both, this._dateTimeProvider);
        this._roleRepository.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>()).Returns(Array.Empty<Role>());
        this._groupRepository.GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>()).Returns(new[] { group });
        this._roleRepository.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Role>());
        this._roleRepository.GetByIdsAsync(Arg.Any<IEnumerable<RoleId>>(), Arg.Any<CancellationToken>()).Returns(new[] { byId });

        Result<IReadOnlyList<RoleDto>> result = await this._handler.HandleAsync(userId);

        result.Value.Single().Id.ShouldBe(byId.Id.Value);
        await this._roleRepository.Received(1).GetByNamesAsync(
            Arg.Is<IEnumerable<string>>(names => names.Count() == 2),
            Arg.Any<CancellationToken>());
        await this._roleRepository.Received(1).GetByIdsAsync(
            Arg.Is<IEnumerable<RoleId>>(ids => ids.Single() == byId.Id),
            Arg.Any<CancellationToken>());
        await this._roleRepository.DidNotReceive().GetByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await this._roleRepository.DidNotReceive().GetByIdAsync(Arg.Any<RoleId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldResolveGroupRolesInSingleBatchedQuery_WhenMultipleMappingsExist()
    {
        var userId = new UserId(Guid.NewGuid());
        this._roleRepository.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>()).Returns(new List<Role>());

        Role roleA = Role.Create("RoleA", "Role A").Value;
        Role roleB = Role.Create("RoleB", "Role B").Value;
        Role inactive = Role.Create("Inactive", "Inactive").Value;
        inactive.Disable();

        Group group1 = Group.Create("Group1", "Group 1", this._dateTimeProvider).Value;
        group1.AddMapping(MappingType.Role, "rolea", null, TokenTarget.AccessToken, this._dateTimeProvider);
        group1.AddMapping(MappingType.Role, "roleb", null, TokenTarget.AccessToken, this._dateTimeProvider);
        Group group2 = Group.Create("Group2", "Group 2", this._dateTimeProvider).Value;
        group2.AddMapping(MappingType.Role, "RoleA", null, TokenTarget.AccessToken, this._dateTimeProvider);
        group2.AddMapping(MappingType.Role, "inactive", null, TokenTarget.AccessToken, this._dateTimeProvider);
        this._groupRepository.GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>()).Returns(new List<Group> { group1, group2 });
        this._roleRepository.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Role> { roleA, roleB, inactive });

        Result<IReadOnlyList<RoleDto>> result = await this._handler.HandleAsync(userId, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(r => r.Name).ShouldBe(["rolea", "roleb"], ignoreOrder: true);
        await this._roleRepository.Received(1).GetByNamesAsync(
            Arg.Is<IEnumerable<string>>(names => names.Count() == 3),
            Arg.Any<CancellationToken>());
        await this._roleRepository.DidNotReceive().GetByIdsAsync(Arg.Any<IEnumerable<RoleId>>(), Arg.Any<CancellationToken>());
    }

    private readonly IRoleRepository _roleRepository;
    private readonly IGroupRepository _groupRepository;
    private readonly GetUserEffectiveRolesQueryHandler _handler;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetUserEffectiveRolesQueryHandlerTests()
    {
        this._roleRepository = Substitute.For<IRoleRepository>();
        this._groupRepository = Substitute.For<IGroupRepository>();
        this._handler = new GetUserEffectiveRolesQueryHandler(this._roleRepository, this._groupRepository);
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnDirectRoles_WhenNoGroupRoles()
    {
        // Arrange
        var userId = new UserId(Guid.NewGuid());
        Role role1 = Role.Create("Role1", "Role 1").Value;

        this._roleRepository.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>())
            .Returns(new List<Role> { role1 });

        this._groupRepository.GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Group>());

        // Act
        Result<IReadOnlyList<RoleDto>> result = await this._handler.HandleAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(1);
        result.Value.ShouldContain(r => r.Name == "role1"); // Role.Create lowercases name
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnDirectAndGroupRoles_WhenBothExist()
    {
        // Arrange
        var userId = new UserId(Guid.NewGuid());

        // Direct Role
        Role roleDirect = Role.Create("DirectRole", "Direct Role").Value;
        this._roleRepository.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>())
            .Returns(new List<Role> { roleDirect });

        // Group Role
        Role roleGroup = Role.Create("GroupRole", "Group Role").Value;

        // Group with Mapping
        Group group = Group.Create("Group1", "Group 1", this._dateTimeProvider).Value;
        // Assuming AddMapping adds to _mappings.
        group.AddMapping(MappingType.Role, "grouprole", null, TokenTarget.AccessToken, this._dateTimeProvider);

        this._groupRepository.GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Group> { group });

        this._roleRepository.GetByNamesAsync(
                Arg.Is<IEnumerable<string>>(names => names.Contains("grouprole")),
                Arg.Any<CancellationToken>())
            .Returns(new List<Role> { roleGroup });

        // Act
        Result<IReadOnlyList<RoleDto>> result = await this._handler.HandleAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value.ShouldContain(r => r.Name == "directrole");
        result.Value.ShouldContain(r => r.Name == "grouprole");
    }

    [Fact]
    public async Task HandleAsync_ShouldDeduplicateRoles_WhenOverlapExists()
    {
        // Arrange
        var userId = new UserId(Guid.NewGuid());
        Role role1 = Role.Create("Role1", "Role 1").Value;

        // Direct returns Role1 (name will be "role1")
        this._roleRepository.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>())
            .Returns(new List<Role> { role1 });

        // Group maps to Role1
        Group group = Group.Create("Group1", "Group 1", this._dateTimeProvider).Value;
        group.AddMapping(MappingType.Role, "role1", null, TokenTarget.AccessToken, this._dateTimeProvider);

        this._groupRepository.GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<Group> { group });

        // Should not need to query for group-mapped roles that are already granted directly.
        this._roleRepository.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Role> { role1 });

        // Act
        Result<IReadOnlyList<RoleDto>> result = await this._handler.HandleAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(1);
        result.Value.ShouldContain(r => r.Name == "role1");
        await this._roleRepository.DidNotReceive().GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
    }
}
