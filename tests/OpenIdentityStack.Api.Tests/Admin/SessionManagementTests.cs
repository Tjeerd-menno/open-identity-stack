using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OpenIdentityStack.Api.Tests.Fixtures;

using SharedKernel;
namespace OpenIdentityStack.Api.Tests.Admin;
/// <summary>
/// Integration tests for Admin Session Management.
/// These tests verify session visibility and revocation via the Admin API.
/// </summary>
public sealed class SessionManagementTests
{
    private readonly AppHostFixture _fixture;

    public SessionManagementTests(AppHostFixture fixture)
    {
        this._fixture = fixture;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        string id = Guid.NewGuid().ToString("N").Substring(0, 8);
        return await this._fixture.CreateAuthenticatedClientAsync($"sessions-admin-{id}", "test-admin-secret");
    }

    private Task<Guid> CreateUserAsync() =>
        this._fixture.CreateTestUserAsync($"session-user-{Guid.NewGuid():N}@example.com", "Session User", "TestPassword123!");
    [Fact]
    public async Task ListSessions_WithActiveSessions_ReturnsSessionList()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        Guid userId = await this.CreateUserAsync();
        Guid sessionId = await this._fixture.CreateSessionAsync(userId);

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/admin/sessions?pageSize=50");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json.ShouldNotBeNull();
        json["items"]?.AsArray().Any(i => i?["id"]?.GetValue<Guid>() == sessionId).ShouldBeTrue();
    }

    [Fact]
    public async Task ListSessions_FilterByUserId_ReturnsUserSessions()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        Guid userId = await this.CreateUserAsync();
        await this._fixture.CreateSessionAsync(userId);

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/admin/sessions?userId={userId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json.ShouldNotBeNull();
        json["items"]?.AsArray().All(i => i?["userId"]?.GetValue<Guid>() == userId).ShouldBeTrue();
    }

    [Fact]
    public async Task RevokeSession_WithActiveSession_Returns204()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        Guid userId = await this.CreateUserAsync();
        Guid sessionId = await this._fixture.CreateSessionAsync(userId);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/api/admin/sessions/{sessionId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RevokeSession_SessionNotFound_Returns404()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        var sessionId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/api/admin/sessions/{sessionId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RevokeSession_AlreadyRevokedSession_Returns204()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        Guid userId = await this.CreateUserAsync();
        Guid sessionId = await this._fixture.CreateSessionAsync(userId);
        HttpResponseMessage first = await client.DeleteAsync($"/api/admin/sessions/{sessionId}");
        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/api/admin/sessions/{sessionId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RevokeAllUserSessions_Returns200WithCount()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        Guid userId = await this.CreateUserAsync();
        await this._fixture.CreateSessionAsync(userId);
        await this._fixture.CreateSessionAsync(userId);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/api/admin/users/{userId}/sessions");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json?["revokedCount"]?.GetValue<int>().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task RevokedSession_RemainsVisibleAsRevoked()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        Guid userId = await this.CreateUserAsync();
        Guid sessionId = await this._fixture.CreateSessionAsync(userId);
        HttpResponseMessage revoke = await client.DeleteAsync($"/api/admin/sessions/{sessionId}");
        revoke.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage getResponse = await client.GetAsync($"/api/admin/sessions/{sessionId}");

        // Assert
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await getResponse.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json?["status"]?.GetValue<string>().ShouldBe("Revoked");
    }
}
