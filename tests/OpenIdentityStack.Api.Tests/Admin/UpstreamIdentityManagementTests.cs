
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OpenIdentityStack.Api.Tests.Fixtures;

namespace OpenIdentityStack.Api.Tests.Admin;
/// <summary>
/// Integration tests for Admin Upstream Identity Management.
/// These tests verify linking and unlinking of upstream identities to users.
/// Currently skipped pending authentication infrastructure fixes.
/// </summary>
public sealed class UpstreamIdentityManagementTests
{
    private readonly AppHostFixture _fixture;

    public UpstreamIdentityManagementTests(AppHostFixture fixture)
    {
        this._fixture = fixture;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        string id = Guid.NewGuid().ToString("N").Substring(0, 8);
        return await this._fixture.CreateAuthenticatedClientAsync($"upstream-admin-{id}", "test-admin-secret");
    }

    private static async Task<Guid> CreateUserAsync(HttpClient client)
    {
        var request = new
        {
            Email = $"upstream-{Guid.NewGuid():N}@example.com",
            DisplayName = "Upstream User",
            Password = "TestPassword123!"
        };

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/admin/users", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        return json?["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("User ID not returned.");
    }

    private static async Task<Guid> CreateProviderAsync(HttpClient client)
    {
        var request = new
        {
            Name = $"provider-{Guid.NewGuid():N}",
            DisplayName = "Test Provider",
            Authority = "https://example.com",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            Scopes = new[] { "openid", "profile" },
            JitProvisioningEnabled = true
        };

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/admin/providers", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        return json?["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("Provider ID not returned.");
    }

    [Fact]
    public async Task LinkUpstreamIdentity_WithValidData_Returns201Created()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        Guid userId = await CreateUserAsync(client);
        Guid providerId = await CreateProviderAsync(client);
        var request = new { ProviderId = providerId, SubjectId = "subject-123", Email = "upstream@example.com" };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/admin/users/{userId}/upstream-identities", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task LinkUpstreamIdentity_UserNotFound_Returns404()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        Guid providerId = await CreateProviderAsync(client);
        var request = new { ProviderId = providerId, SubjectId = "subject-123", Email = "upstream@example.com" };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/admin/users/{Guid.NewGuid()}/upstream-identities", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUpstreamIdentities_WithLinkedIdentity_ReturnsIdentities()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        Guid userId = await CreateUserAsync(client);
        Guid providerId = await CreateProviderAsync(client);
        var request = new { ProviderId = providerId, SubjectId = "subject-123", Email = "upstream@example.com" };
        HttpResponseMessage link = await client.PostAsJsonAsync($"/api/admin/users/{userId}/upstream-identities", request);
        link.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/admin/users/{userId}/upstream-identities");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json?["items"]?.AsArray().Any(i => i?["providerId"]?.GetValue<Guid>() == providerId).ShouldBeTrue();
    }

    [Fact]
    public async Task GetUpstreamIdentities_UserNotFound_Returns404()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/admin/users/{Guid.NewGuid()}/upstream-identities");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnlinkUpstreamIdentity_WithLinkedIdentity_Returns204()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        Guid userId = await CreateUserAsync(client);
        Guid providerId = await CreateProviderAsync(client);
        var request = new { ProviderId = providerId, SubjectId = "subject-123", Email = "upstream@example.com" };
        HttpResponseMessage link = await client.PostAsJsonAsync($"/api/admin/users/{userId}/upstream-identities", request);
        link.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/api/admin/users/{userId}/upstream-identities/{providerId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UnlinkUpstreamIdentity_IdentityNotLinked_Returns404()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        Guid userId = await CreateUserAsync(client);
        Guid providerId = await CreateProviderAsync(client);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/api/admin/users/{userId}/upstream-identities/{providerId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LinkUpstreamIdentity_AlreadyLinked_Returns409Conflict()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        Guid userId = await CreateUserAsync(client);
        Guid providerId = await CreateProviderAsync(client);
        var request = new { ProviderId = providerId, SubjectId = "subject-123", Email = "upstream@example.com" };
        HttpResponseMessage link = await client.PostAsJsonAsync($"/api/admin/users/{userId}/upstream-identities", request);
        link.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/admin/users/{userId}/upstream-identities", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task LinkUpstreamIdentity_ThenUnlink_CanRelinkSuccessfully()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        Guid userId = await CreateUserAsync(client);
        Guid providerId = await CreateProviderAsync(client);
        var request = new { ProviderId = providerId, SubjectId = "subject-123", Email = "upstream@example.com" };

        HttpResponseMessage link = await client.PostAsJsonAsync($"/api/admin/users/{userId}/upstream-identities", request);
        link.StatusCode.ShouldBe(HttpStatusCode.Created);

        HttpResponseMessage unlink = await client.DeleteAsync($"/api/admin/users/{userId}/upstream-identities/{providerId}");
        unlink.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage relink = await client.PostAsJsonAsync($"/api/admin/users/{userId}/upstream-identities", request);

        // Assert
        relink.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
