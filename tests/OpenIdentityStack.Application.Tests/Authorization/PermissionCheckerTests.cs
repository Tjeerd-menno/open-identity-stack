using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Tests.Authorization;
/// <summary>
/// Unit tests for permission checking logic.
/// </summary>
public sealed class PermissionCheckerTests
{
    private readonly IRoleRepository _roleRepository;
    private readonly PermissionChecker _permissionChecker;

    public PermissionCheckerTests()
    {
        this._roleRepository = Substitute.For<IRoleRepository>();
        this._permissionChecker = new PermissionChecker(this._roleRepository);
    }

    [Fact]
    public async Task HasPermission_UserWithExactPermission_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.Create();
        Role role = CreateRoleWithPermissions("Admin", Permissions.Users.Read, Permissions.Users.Write);

        this._roleRepository.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>())
            .Returns(new List<Role> { role });

        // Act
        bool result = await this._permissionChecker.HasPermissionAsync(userId, Permissions.Users.Read);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task HasPermission_UserWithWildcardPermission_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.Create();
        Role role = CreateRoleWithPermissions("SuperAdmin", Permissions.All);

        this._roleRepository.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>())
            .Returns(new List<Role> { role });

        // Act
        bool result = await this._permissionChecker.HasPermissionAsync(userId, Permissions.Users.Delete);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task HasPermission_UserWithResourceWildcard_MatchesResourceOperations()
    {
        // Arrange
        var userId = UserId.Create();
        Role role = CreateRoleWithPermissions("UserAdmin", "users:*");

        this._roleRepository.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>())
            .Returns(new List<Role> { role });

        // Act
        bool canRead = await this._permissionChecker.HasPermissionAsync(userId, Permissions.Users.Read);
        bool canWrite = await this._permissionChecker.HasPermissionAsync(userId, Permissions.Users.Write);
        bool canDelete = await this._permissionChecker.HasPermissionAsync(userId, Permissions.Users.Delete);
        bool canManageRoles = await this._permissionChecker.HasPermissionAsync(userId, Permissions.Roles.Read);

        // Assert - Should have all user permissions but not role permissions
        canRead.ShouldBeTrue();
        canWrite.ShouldBeTrue();
        canDelete.ShouldBeTrue();
        canManageRoles.ShouldBeFalse();
    }

    [Fact]
    public async Task HasPermission_UserWithoutPermission_ReturnsFalse()
    {
        // Arrange
        var userId = UserId.Create();
        Role role = CreateRoleWithPermissions("ReadOnly", Permissions.Users.Read);

        this._roleRepository.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>())
            .Returns(new List<Role> { role });

        // Act
        bool result = await this._permissionChecker.HasPermissionAsync(userId, Permissions.Users.Delete);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task HasPermission_UserWithMultipleRoles_CombinesPermissions()
    {
        // Arrange
        var userId = UserId.Create();
        Role role1 = CreateRoleWithPermissions("UserReader", Permissions.Users.Read);
        Role role2 = CreateRoleWithPermissions("RoleReader", Permissions.Roles.Read);

        this._roleRepository.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>())
            .Returns(new List<Role> { role1, role2 });

        // Act
        bool canReadUsers = await this._permissionChecker.HasPermissionAsync(userId, Permissions.Users.Read);
        bool canReadRoles = await this._permissionChecker.HasPermissionAsync(userId, Permissions.Roles.Read);

        // Assert
        canReadUsers.ShouldBeTrue();
        canReadRoles.ShouldBeTrue();
    }

    [Fact]
    public async Task HasPermission_UserNotFound_ReturnsFalse()
    {
        // Arrange
        var userId = UserId.Create();
        this._roleRepository.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>())
            .Returns(new List<Role>());

        // Act
        bool result = await this._permissionChecker.HasPermissionAsync(userId, Permissions.Users.Read);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task HasPermission_UserWithNoRoles_ReturnsFalse()
    {
        // Arrange
        var userId = UserId.Create();

        this._roleRepository.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>())
            .Returns(new List<Role>());

        // Act
        bool result = await this._permissionChecker.HasPermissionAsync(userId, Permissions.Users.Read);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task HasAnyPermission_UserHasOne_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.Create();
        Role role = CreateRoleWithPermissions("UserReader", Permissions.Users.Read);

        this._roleRepository.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>())
            .Returns(new List<Role> { role });

        // Act
        bool result = await this._permissionChecker.HasAnyPermissionAsync(
            userId,
            new[] { Permissions.Users.Delete, Permissions.Users.Read });

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task HasAnyPermission_UserHasNone_ReturnsFalse()
    {
        // Arrange
        var userId = UserId.Create();
        Role role = CreateRoleWithPermissions("UserReader", Permissions.Users.Read);

        this._roleRepository.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>())
            .Returns(new List<Role> { role });

        // Act
        bool result = await this._permissionChecker.HasAnyPermissionAsync(
            userId,
            new[] { Permissions.Users.Delete, Permissions.Roles.Write });

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task HasAllPermissions_UserHasAll_ReturnsTrue()
    {
        // Arrange
        var userId = UserId.Create();
        Role role = CreateRoleWithPermissions("UserAdmin", Permissions.Users.Read, Permissions.Users.Write);

        this._roleRepository.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>())
            .Returns(new List<Role> { role });

        // Act
        bool result = await this._permissionChecker.HasAllPermissionsAsync(
            userId,
            new[] { Permissions.Users.Read, Permissions.Users.Write });

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task HasAllPermissions_UserMissingOne_ReturnsFalse()
    {
        // Arrange
        var userId = UserId.Create();
        Role role = CreateRoleWithPermissions("UserReader", Permissions.Users.Read);

        this._roleRepository.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>())
            .Returns(new List<Role> { role });

        // Act
        bool result = await this._permissionChecker.HasAllPermissionsAsync(
            userId,
            new[] { Permissions.Users.Read, Permissions.Users.Write });

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task GetAllPermissions_ReturnsAllPermissionsFromAllRoles()
    {
        // Arrange
        var userId = UserId.Create();
        Role role1 = CreateRoleWithPermissions("UserAdmin", Permissions.Users.Read, Permissions.Users.Write);
        Role role2 = CreateRoleWithPermissions("RoleAdmin", Permissions.Roles.Read, Permissions.Roles.Write);

        this._roleRepository.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>())
            .Returns(new List<Role> { role1, role2 });

        // Act
        IReadOnlyList<string> permissions = await this._permissionChecker.GetAllPermissionsAsync(userId);

        // Assert
        permissions.ShouldContain(Permissions.Users.Read);
        permissions.ShouldContain(Permissions.Users.Write);
        permissions.ShouldContain(Permissions.Roles.Read);
        permissions.ShouldContain(Permissions.Roles.Write);
    }

    [Fact]
    public async Task GetAllPermissions_DeduplicatesPermissions()
    {
        // Arrange
        var userId = UserId.Create();
        Role role1 = CreateRoleWithPermissions("UserReader", Permissions.Users.Read);
        Role role2 = CreateRoleWithPermissions("UserReader2", Permissions.Users.Read);

        this._roleRepository.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>())
            .Returns(new List<Role> { role1, role2 });

        // Act
        IReadOnlyList<string> permissions = await this._permissionChecker.GetAllPermissionsAsync(userId);

        // Assert
        permissions.Count.ShouldBe(1);
        permissions.ShouldContain(Permissions.Users.Read);
    }

    private static Role CreateRoleWithPermissions(string name, params string[] permissions)
    {
        Role role = Role.Create(name, $"{name} role for testing").Value;
        foreach (string permission in permissions)
        {
            role.AddPermission(permission);
        }
        return role;
    }
}
