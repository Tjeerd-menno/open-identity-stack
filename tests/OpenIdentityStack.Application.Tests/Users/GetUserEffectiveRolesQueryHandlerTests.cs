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

        this._roleRepository.GetByNameAsync("grouprole", Arg.Any<CancellationToken>())
            .Returns(roleGroup);

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

        // Mock GetByNameAsync just in case it's called (optimization might skip it)
        this._roleRepository.GetByNameAsync("role1", Arg.Any<CancellationToken>())
            .Returns(role1);

        // Act
        Result<IReadOnlyList<RoleDto>> result = await this._handler.HandleAsync(userId, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(1);
        result.Value.ShouldContain(r => r.Name == "role1");
    }
}
