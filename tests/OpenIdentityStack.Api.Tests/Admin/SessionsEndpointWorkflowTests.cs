
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OpenIdentityStack.Api.Tests.Fixtures;

using SharedKernel;
namespace OpenIdentityStack.Api.Tests.Admin;
/// <summary>
/// Integration tests for the /api/admin/sessions endpoint.
/// Verifies API contract compliance for session management operations.
/// </summary>
public sealed class SessionsEndpointWorkflowTests(AppHostFixture fixture) : IAsyncLifetime
{
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
        string clientId = $"sessions-contract-{Guid.NewGuid():N}";
        const string clientSecret = "test-secret-123";

        using HttpClient authenticated = await this._fixture.CreateAuthenticatedClientAsync(clientId, clientSecret);
        this._accessToken = authenticated.DefaultRequestHeaders.Authorization!.Parameter;
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
            Email = $"session-{Guid.NewGuid():N}@example.com",
            DisplayName = "Session User",
            Password = "TestPassword123!"
        };

        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/users", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        return json?["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("User ID not returned.");
    }

    private async Task<Guid> CreateSessionAsync(Guid userId)
    {
        return await this._fixture.CreateSessionAsync(userId);
    }

    #region List Sessions

    [Fact]
    public async Task ListSessions_Returns200WithPaginatedResponse()
    {
        // Arrange
        Guid userId = await this.CreateUserAsync();
        await this.CreateSessionAsync(userId);

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, "/api/admin/sessions");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json.ShouldNotBeNull();
        json["items"].ShouldNotBeNull();
    }

    [Fact]
    public async Task ListSessions_WithPagination_ReturnsRequestedPage()
    {
        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, "/api/admin/sessions?page=1&pageSize=2");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json.ShouldNotBeNull();
        json["page"]?.GetValue<int>().ShouldBe(1);
        json["pageSize"]?.GetValue<int>().ShouldBe(2);
    }

    [Fact]
    public async Task ListSessions_WithUserIdFilter_ReturnsUserSessions()
    {
        // Arrange
        Guid userId = await this.CreateUserAsync();
        await this.CreateSessionAsync(userId);

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, $"/api/admin/sessions?userId={userId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json.ShouldNotBeNull();
        json["items"]?.AsArray().All(i => i?["userId"]?.GetValue<Guid>() == userId).ShouldBeTrue();
    }

    [Fact]
    public async Task ListSessions_WithStatusFilter_ReturnsFilteredSessions()
    {
        // Arrange
        Guid userId = await this.CreateUserAsync();
        Guid sessionId = await this.CreateSessionAsync(userId);
        HttpResponseMessage revoke = await this.SendRequestAsync(HttpMethod.Delete, $"/api/admin/sessions/{sessionId}");
        revoke.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, "/api/admin/sessions?status=Revoked");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json.ShouldNotBeNull();
        json["items"]?.AsArray().Any(i => string.Equals(i?["status"]?.GetValue<string>(), "Revoked", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
    }

    #endregion

    #region Get Session

    [Fact]
    public async Task GetSession_WithValidId_Returns200Ok()
    {
        // Arrange
        Guid userId = await this.CreateUserAsync();
        Guid sessionId = await this.CreateSessionAsync(userId);

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, $"/api/admin/sessions/{sessionId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json.ShouldNotBeNull();
        json["id"]?.GetValue<Guid>().ShouldBe(sessionId);
    }

    [Fact]
    public async Task GetSession_WithInvalidId_Returns404NotFound()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, $"/api/admin/sessions/{sessionId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion

    #region Revoke Session

    [Fact]
    public async Task RevokeSession_WithActiveSession_Returns204NoContent()
    {
        // Arrange
        Guid userId = await this.CreateUserAsync();
        Guid sessionId = await this.CreateSessionAsync(userId);

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Delete, $"/api/admin/sessions/{sessionId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RevokeSession_WithNonExistentSession_Returns404NotFound()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Delete, $"/api/admin/sessions/{sessionId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RevokeSession_WithAlreadyRevokedSession_Returns409Conflict()
    {
        // Arrange
        Guid userId = await this.CreateUserAsync();
        Guid sessionId = await this.CreateSessionAsync(userId);
        HttpResponseMessage first = await this.SendRequestAsync(HttpMethod.Delete, $"/api/admin/sessions/{sessionId}");
        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Delete, $"/api/admin/sessions/{sessionId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    #endregion

    #region Revoke All User Sessions

    [Fact]
    public async Task RevokeAllUserSessions_WithValidUser_Returns200Ok()
    {
        // Arrange
        Guid userId = await this.CreateUserAsync();
        await this.CreateSessionAsync(userId);
        await this.CreateSessionAsync(userId);

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Delete, $"/api/admin/users/{userId}/sessions");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RevokeAllUserSessions_ExcludeCurrentSession_PreservesCurrent()
    {
        // Arrange
        Guid userId = await this.CreateUserAsync();
        Guid keepSession = await this.CreateSessionAsync(userId);
        await this.CreateSessionAsync(userId);

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Delete, $"/api/admin/users/{userId}/sessions?excludeSessionId={keepSession}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        HttpResponseMessage get = await this.SendRequestAsync(HttpMethod.Get, $"/api/admin/sessions/{keepSession}");
        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await get.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json?["status"]?.GetValue<string>().ShouldBe("Active");
    }

    [Fact]
    public async Task RevokeAllUserSessions_WithNonExistentUser_Returns404NotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Delete, $"/api/admin/users/{userId}/sessions");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    #endregion

    #region Session Item Response Schema

    [Fact]
    public async Task SessionItem_HasRequiredFields()
    {
        // Arrange
        Guid userId = await this.CreateUserAsync();
        await this.CreateSessionAsync(userId);

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, "/api/admin/sessions?pageSize=1");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        JsonNode? item = json?["items"]?.AsArray().FirstOrDefault();
        item.ShouldNotBeNull();
        item?["id"].ShouldNotBeNull();
        item?["userId"].ShouldNotBeNull();
        item?["status"].ShouldNotBeNull();
    }

    #endregion

    #region Authentication and Authorization

    [Fact]
    public async Task Sessions_WithoutAuth_Returns401Unauthorized()
    {
        // Act
        HttpResponseMessage response = await this.Client.GetAsync("/api/admin/sessions");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Sessions_WithoutPermission_Returns403Forbidden()
    {
        // Arrange
        string clientId = $"sessions-limited-{Guid.NewGuid():N}";
        const string clientSecret = "test-secret-123";
        using HttpClient limited = await this._fixture.CreateAuthenticatedClientAsync(clientId, clientSecret, administrativePermissions: ["users:read"]);
        string token = limited.DefaultRequestHeaders.Authorization!.Parameter!;

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/sessions");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Act
        HttpResponseMessage response = await this.Client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    #endregion
}
