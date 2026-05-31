
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OpenIdentityStack.Api.Tests.Fixtures;
using OpenIdentityStack.Domain.ApplicationPermissions;
using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Api.Tests.Admin;
/// <summary>
/// Integration tests for the /api/admin/roles endpoint.
/// </summary>
public sealed class RolesEndpointWorkflowTests(AppHostFixture fixture) : IAsyncLifetime
{
    private static readonly string[] wildcardPermissions = ["users:*"];

    private readonly AppHostFixture _fixture = fixture;
    private HttpClient _client = null!;
    private string? _accessToken;

    private HttpClient Client => this._client;

    public async ValueTask InitializeAsync()
    {
        this._client = this._fixture.HttpClient!;
        await this.AuthenticateAsync();
    }

    public ValueTask DisposeAsync()
    {
        // Don't dispose the shared HttpClient from the fixture
        return ValueTask.CompletedTask;
    }

    private async Task AuthenticateAsync()
    {
        string clientId = $"roles-contract-{Guid.NewGuid():N}";
        const string clientSecret = "test-secret-123";

        await this._fixture.CreateServiceAccountAsync(clientId, clientSecret);
        this._accessToken = await this._fixture.GetAccessTokenAsync(clientId, clientSecret);
    }

    private async Task<HttpResponseMessage> SendRequestAsync(HttpMethod method, string url, object? content = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(this._accessToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", this._accessToken);
        }

        if (content != null)
        {
            request.Content = JsonContent.Create(content);
        }

        return await this.Client.SendAsync(request);
    }

    private async Task<Guid> CreateUserAsync()
    {
        var request = new
        {
            Email = $"role-user-{Guid.NewGuid():N}@example.com",
            DisplayName = "Role User",
            Password = "TestPassword123!"
        };

        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/users", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        return json?["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("User ID not returned.");
    }

    private async Task<Guid> CreateRoleAsync(string? name = null)
    {
        var request = new
        {
            Name = name ?? $"role-{Guid.NewGuid():N}",
            Description = "Test role"
        };

        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/roles", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        return json?["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("Role ID not returned.");
    }

    private async Task<string> CreateApplicationPermissionAsync()
    {
        string applicationIdentifier = $"role-app-{Guid.NewGuid():N}";
        const string permissionKey = "items:read";

        await this._fixture.ExecuteDbContextAsync(async dbContext =>
        {
            Result<RegisteredApplication> applicationResult = RegisteredApplication.Register(
                applicationIdentifier,
                "Role Permission App",
                "Application permissions for role tests",
                "owner-1",
                OwnerType.User,
                [(permissionKey, "Read items", "Read inventory items", "Items")],
                "test",
                new TestDateTimeProvider());

            applicationResult.IsSuccess.ShouldBeTrue();
            dbContext.RegisteredApplications.Add(applicationResult.Value);
            await dbContext.SaveChangesAsync();
        });

        return $"{applicationIdentifier}:{permissionKey}";
    }

    #region List Roles

    [Fact]
    public async Task ListRoles_Returns200WithPaginatedResponse()
    {
        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, "/api/admin/roles");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json.ShouldNotBeNull();
        json["items"].ShouldNotBeNull();
    }

    [Fact]
    public async Task ListRoles_WithPagination_ReturnsRequestedPage()
    {
        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, "/api/admin/roles?page=1&pageSize=2");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json.ShouldNotBeNull();
        json["page"]?.GetValue<int>().ShouldBe(1);
        json["pageSize"]?.GetValue<int>().ShouldBe(2);
    }

    #endregion

    #region Create Role

    [Fact]
    public async Task CreateRole_WithValidRequest_Returns201Created()
    {
        // Arrange
        var request = new
        {
            Name = $"role-{Guid.NewGuid():N}",
            Description = "Test role"
        };

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/roles", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateRole_WithMissingName_Returns400BadRequest()
    {
        // Arrange
        var request = new
        {
            Description = "Missing name"
        };

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/roles", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateRole_WithDuplicateName_Returns409Conflict()
    {
        // Arrange
        string name = $"role-{Guid.NewGuid():N}";
        await this.CreateRoleAsync(name);

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/roles", new { Name = name, Description = "Dup" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateRole_WithWildcardPermissionWithoutAcknowledgement_Returns409Conflict()
    {
        var request = new
        {
            Name = $"role-{Guid.NewGuid():N}",
            Description = "Broad grant role",
            Permissions = wildcardPermissions
        };

        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/roles", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateRole_WithWildcardPermissionAndAcknowledgement_Returns201Created()
    {
        var request = new
        {
            Name = $"role-{Guid.NewGuid():N}",
            Description = "Broad grant role",
            Permissions = wildcardPermissions,
            AcknowledgeWildcardGrant = true
        };

        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/roles", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json?["permissions"]?.AsArray().Select(p => p?.GetValue<string>()).ShouldContain("users:*");
    }

    #endregion

    #region Get Role

    [Fact]
    public async Task GetRole_WithValidId_Returns200Ok()
    {
        // Arrange
        Guid roleId = await this.CreateRoleAsync();

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, $"/api/admin/roles/{roleId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRole_WithInvalidId_Returns404NotFound()
    {
        // Arrange
        var roleId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, $"/api/admin/roles/{roleId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion

    #region Update Role

    [Fact]
    public async Task UpdateRole_WithValidRequest_Returns200Ok()
    {
        // Arrange
        Guid roleId = await this.CreateRoleAsync();

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Put, $"/api/admin/roles/{roleId}", new { Description = "Updated" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateRole_WithApplicationPermission_PersistsAssignedPermission()
    {
        Guid roleId = await this.CreateRoleAsync();
        string applicationPermission = await this.CreateApplicationPermissionAsync();

        HttpResponseMessage response = await this.SendRequestAsync(
            HttpMethod.Put,
            $"/api/admin/roles/{roleId}",
            new
            {
                Description = "Updated with application permissions",
                Permissions = new[] { applicationPermission }
            });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json?["permissions"]?.AsArray().Select(p => p?.GetValue<string>()).ShouldContain(applicationPermission);
    }

    #endregion

    #region Disable/Enable Role

    [Fact]
    public async Task DisableRole_WithValidId_Returns200Ok()
    {
        // Arrange
        Guid roleId = await this.CreateRoleAsync();

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, $"/api/admin/roles/{roleId}/disable");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EnableRole_WithValidId_Returns200Ok()
    {
        // Arrange
        Guid roleId = await this.CreateRoleAsync();
        HttpResponseMessage disable = await this.SendRequestAsync(HttpMethod.Post, $"/api/admin/roles/{roleId}/disable");
        disable.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, $"/api/admin/roles/{roleId}/enable");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DisableRole_WhenSystemRole_Returns400BadRequest()
    {
        // Arrange
        HttpResponseMessage list = await this.SendRequestAsync(HttpMethod.Get, "/api/admin/roles?pageSize=200");
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await list.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        JsonNode? role = json?["items"]?.AsArray()
            .FirstOrDefault(r => string.Equals(r?["name"]?.GetValue<string>(), "super-admin", StringComparison.OrdinalIgnoreCase));
        role.ShouldNotBeNull();
        Guid roleId = role?["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("System role ID not found.");

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, $"/api/admin/roles/{roleId}/disable");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Role Permissions

    [Fact]
    public async Task AddPermission_WithWildcardPermissionWithoutAcknowledgement_Returns409Conflict()
    {
        Guid roleId = await this.CreateRoleAsync();

        HttpResponseMessage response = await this.SendRequestAsync(
            HttpMethod.Post,
            $"/api/admin/roles/{roleId}/permissions",
            new { Permission = "users:*" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddPermission_WithWildcardPermissionAndAcknowledgement_Returns200Ok()
    {
        Guid roleId = await this.CreateRoleAsync();

        HttpResponseMessage response = await this.SendRequestAsync(
            HttpMethod.Post,
            $"/api/admin/roles/{roleId}/permissions",
            new { Permission = "users:*", AcknowledgeWildcardGrant = true });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json?["permissions"]?.AsArray().Select(p => p?.GetValue<string>()).ShouldContain("users:*");
    }

    [Fact]
    public async Task SetPermissions_WithWildcardPermissionWithoutAcknowledgement_Returns409Conflict()
    {
        Guid roleId = await this.CreateRoleAsync();

        HttpResponseMessage response = await this.SendRequestAsync(
            HttpMethod.Put,
            $"/api/admin/roles/{roleId}/permissions",
            new { Permissions = wildcardPermissions });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SetPermissions_WithWildcardPermissionAndAcknowledgement_Returns200Ok()
    {
        Guid roleId = await this.CreateRoleAsync();

        HttpResponseMessage response = await this.SendRequestAsync(
            HttpMethod.Put,
            $"/api/admin/roles/{roleId}/permissions",
            new { Permissions = wildcardPermissions, AcknowledgeWildcardGrant = true });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json?["permissions"]?.AsArray().Select(p => p?.GetValue<string>()).ShouldContain("users:*");
    }

    [Fact]
    public async Task RemovePermission_WithWildcardPermission_Returns200Ok()
    {
        var createRequest = new
        {
            Name = $"role-{Guid.NewGuid():N}",
            Permissions = wildcardPermissions,
            AcknowledgeWildcardGrant = true
        };
        HttpResponseMessage create = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/roles", createRequest);
        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        JsonNode? created = await create.Content.ReadFromJsonAsync<JsonNode>();
        Guid roleId = created?["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("Role ID not returned.");

        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Delete, $"/api/admin/roles/{roleId}/permissions/users%3A%2A");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json?["permissions"]?.AsArray().Select(p => p?.GetValue<string>()).ShouldNotContain("users:*");
    }

    #endregion

    #region Role Assignments

    [Fact]
    public async Task AssignRole_WithValidRequest_Returns200Ok()
    {
        // Arrange
        Guid userId = await this.CreateUserAsync();
        Guid roleId = await this.CreateRoleAsync();

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, $"/api/admin/users/{userId}/roles/{roleId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnassignRole_WithValidRequest_Returns200Ok()
    {
        // Arrange
        Guid userId = await this.CreateUserAsync();
        Guid roleId = await this.CreateRoleAsync();
        HttpResponseMessage assign = await this.SendRequestAsync(HttpMethod.Post, $"/api/admin/users/{userId}/roles/{roleId}");
        assign.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Delete, $"/api/admin/users/{userId}/roles/{roleId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUserRoles_WithValidUserId_Returns200Ok()
    {
        // Arrange
        Guid userId = await this.CreateUserAsync();
        Guid roleId = await this.CreateRoleAsync();
        HttpResponseMessage assign = await this.SendRequestAsync(HttpMethod.Post, $"/api/admin/users/{userId}/roles/{roleId}");
        assign.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, $"/api/admin/users/{userId}/roles");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    #endregion

    #region Authentication

    [Fact]
    public async Task RolesEndpoints_WithoutAuthentication_Returns401Unauthorized()
    {
        // Act
        HttpResponseMessage response = await this.Client.GetAsync("/api/admin/roles");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RolesEndpoints_WithoutAdminRole_Returns403Forbidden()
    {
        // Arrange
        string clientId = $"roles-limited-{Guid.NewGuid():N}";
        const string clientSecret = "test-secret-123";
        string[] limitedScopes = new[] { "limited" };

        await this._fixture.CreateServiceAccountAsync(clientId, clientSecret, limitedScopes);
        string token = await this._fixture.GetAccessTokenAsync(clientId, clientSecret, "limited");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/roles");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        HttpResponseMessage response = await this.Client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    #endregion

    private sealed class TestDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

        public DateTimeOffset Now => DateTimeOffset.Now;
    }
}
