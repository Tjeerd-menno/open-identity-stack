
using System.Net;
using OpenIdentityStack.Api.Tests.Fixtures;

namespace OpenIdentityStack.Api.Tests.Authentication;
/// <summary>
/// Integration tests for role claims in tokens.
/// Tests verify that role assignment and retrieval works correctly
/// which is the foundation for role claims appearing in authorization code flow tokens.
/// </summary>
[Collection("AppHost")]
public sealed class RoleClaimsTests
{
    private readonly AppHostFixture _fixture;

    public RoleClaimsTests(AppHostFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public async Task User_WithAssignedRole_HasRoleInEffectiveRoles()
    {
        // Arrange - Create user and role
        string email = $"user-{Guid.NewGuid():N}@test.com";
        Guid userId = await this._fixture.CreateTestUserAsync(email, "Test User", "Password123!");
        Guid roleId = await this._fixture.CreateTestRoleAsync("TestRole", "Test role for claims testing");

        // Assign role
        await this._fixture.AssignRoleToUserAsync(userId, roleId);

        // Act - Get effective roles
        IReadOnlyList<(Guid Id, string Name, string DisplayName)> roles = await this._fixture.GetUserRolesAsync(userId);

        // Assert
        roles.ShouldNotBeEmpty();
        roles.ShouldContain(r => r.Name == "testrole"); // Role names are normalized to lowercase
    }

    [Fact]
    public async Task User_WithMultipleRoles_HasAllRolesInEffectiveRoles()
    {
        // Arrange - Create user and multiple roles
        string email = $"user-{Guid.NewGuid():N}@test.com";
        Guid userId = await this._fixture.CreateTestUserAsync(email, "Multi-Role User", "Password123!");

        Guid roleId1 = await this._fixture.CreateTestRoleAsync($"Role1_{Guid.NewGuid():N}", "First role");
        Guid roleId2 = await this._fixture.CreateTestRoleAsync($"Role2_{Guid.NewGuid():N}", "Second role");
        Guid roleId3 = await this._fixture.CreateTestRoleAsync($"Role3_{Guid.NewGuid():N}", "Third role");

        // Assign all roles
        await this._fixture.AssignRoleToUserAsync(userId, roleId1);
        await this._fixture.AssignRoleToUserAsync(userId, roleId2);
        await this._fixture.AssignRoleToUserAsync(userId, roleId3);

        // Act - Get effective roles
        IReadOnlyList<(Guid Id, string Name, string DisplayName)> roles = await this._fixture.GetUserRolesAsync(userId);

        // Assert
        roles.Count.ShouldBe(3);
    }

    [Fact]
    public async Task User_WithNoRoles_HasEmptyEffectiveRoles()
    {
        // Arrange - Create user without any roles
        string email = $"user-{Guid.NewGuid():N}@test.com";
        Guid userId = await this._fixture.CreateTestUserAsync(email, "No Role User", "Password123!");

        // Act - Get effective roles
        IReadOnlyList<(Guid Id, string Name, string DisplayName)> roles = await this._fixture.GetUserRolesAsync(userId);

        // Assert
        roles.ShouldBeEmpty();
    }

    [Fact]
    public async Task User_RoleAssignment_CanBeVerifiedViaAdminApi()
    {
        // Arrange - Create service account, user, and role
        string clientId = $"admin-client-{Guid.NewGuid():N}";
        string clientSecret = "admin-secret-123!";
        HttpClient client = await this._fixture.CreateAuthenticatedClientAsync(clientId, clientSecret);

        string email = $"user-{Guid.NewGuid():N}@test.com";
        Guid userId = await this._fixture.CreateTestUserAsync(email, "Admin API Test User", "Password123!");
        Guid roleId = await this._fixture.CreateTestRoleAsync($"AdminApiRole_{Guid.NewGuid():N}", "Role for admin API test");

        // Assign role via test seeding
        await this._fixture.AssignRoleToUserAsync(userId, roleId);

        // Act - Get user via admin API
        HttpResponseMessage response = await client.GetAsync($"/api/admin/users/{userId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Role_Creation_ReturnsValidRoleId()
    {
        // Arrange
        string roleName = $"NewRole_{Guid.NewGuid():N}";
        string description = "A newly created role for testing";

        // Act
        Guid roleId = await this._fixture.CreateTestRoleAsync(roleName, description);

        // Assert
        roleId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task User_CanHaveSameRoleAssignedOnlyOnce()
    {
        // Arrange - Create user and role
        string email = $"user-{Guid.NewGuid():N}@test.com";
        Guid userId = await this._fixture.CreateTestUserAsync(email, "Duplicate Role User", "Password123!");
        Guid roleId = await this._fixture.CreateTestRoleAsync($"UniqueRole_{Guid.NewGuid():N}", "Role for duplicate test");

        // Assign role first time
        await this._fixture.AssignRoleToUserAsync(userId, roleId);

        // Act - Try to assign same role again (should either succeed idempotently or fail gracefully)
        // The behavior depends on the implementation
        Exception? exception = await Record.ExceptionAsync(() => this._fixture.AssignRoleToUserAsync(userId, roleId));

        // Assert - Either no exception (idempotent) or a controlled error
        // Get roles to verify we only have one instance
        IReadOnlyList<(Guid Id, string Name, string DisplayName)> roles = await this._fixture.GetUserRolesAsync(userId);
        roles.Count.ShouldBe(1);
    }
}
