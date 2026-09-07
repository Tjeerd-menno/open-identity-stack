
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Api.Tests.Fixtures;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Admin;
/// <summary>
/// Integration tests for Admin Upstream Identity Management.
/// These tests verify proof-required linking rejection and existing identity management.
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
    public async Task LinkUpstreamIdentity_WithRawIdentifiers_Returns403AndCreatesNoLink()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        Guid userId = await CreateUserAsync(client);
        Guid providerId = await CreateProviderAsync(client);
        var request = new { ProviderId = providerId, SubjectId = "subject-123", Email = "upstream@example.com" };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/admin/users/{userId}/upstream-identities", request);

        await AssertProofRequiredAsync(response);
        HttpResponseMessage list = await client.GetAsync($"/api/admin/users/{userId}/upstream-identities");
        JsonNode? identities = await list.Content.ReadFromJsonAsync<JsonNode>();
        identities!["items"]!.AsArray().ShouldBeEmpty();
    }

    [Fact]
    public async Task LinkUpstreamIdentity_UserNotFound_ReturnsSame403()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        Guid providerId = await CreateProviderAsync(client);
        var request = new { ProviderId = providerId, SubjectId = "subject-123", Email = "upstream@example.com" };

        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/admin/users/{Guid.NewGuid()}/upstream-identities", request);
        await AssertProofRequiredAsync(response);
    }

    [Fact]
    public async Task GetUpstreamIdentities_WithLinkedIdentity_ReturnsIdentities()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        Guid userId = await CreateUserAsync(client);
        Guid providerId = await CreateProviderAsync(client);
        await this.SeedExistingIdentityAsync(userId, providerId);

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
        await this.SeedExistingIdentityAsync(userId, providerId);

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
    public async Task LinkUpstreamIdentity_AlreadyLinked_ReturnsSame403()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        Guid userId = await CreateUserAsync(client);
        Guid providerId = await CreateProviderAsync(client);
        await this.SeedExistingIdentityAsync(userId, providerId);
        var request = new { ProviderId = providerId, SubjectId = "subject-123", Email = "upstream@example.com" };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/admin/users/{userId}/upstream-identities", request);

        await AssertProofRequiredAsync(response);
    }

    [Fact]
    public async Task LinkUpstreamIdentity_AfterUnlink_StillRequiresProof()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        Guid userId = await CreateUserAsync(client);
        Guid providerId = await CreateProviderAsync(client);
        var request = new { ProviderId = providerId, SubjectId = "subject-123", Email = "upstream@example.com" };

        await this.SeedExistingIdentityAsync(userId, providerId);

        HttpResponseMessage unlink = await client.DeleteAsync($"/api/admin/users/{userId}/upstream-identities/{providerId}");
        unlink.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        HttpResponseMessage relink = await client.PostAsJsonAsync($"/api/admin/users/{userId}/upstream-identities", request);

        // Assert
        await AssertProofRequiredAsync(relink);
    }

    private Task SeedExistingIdentityAsync(Guid userId, Guid providerId) =>
        this._fixture.ExecuteDbContextAsync(async dbContext =>
        {
            User user = await dbContext.Users.SingleAsync(user => user.Id == new UserId(userId));
            UpstreamProvider provider = await dbContext.UpstreamProviders.SingleAsync(provider => provider.Id == new UpstreamProviderId(providerId));
            user.LinkUpstreamIdentity(provider.Id, provider.Name, "subject-123", "upstream@example.com").IsSuccess.ShouldBeTrue();
            await dbContext.SaveChangesAsync();
        });

    private static async Task AssertProofRequiredAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        JsonNode? problem = await response.Content.ReadFromJsonAsync<JsonNode>();
        problem!["status"]!.GetValue<int>().ShouldBe(403);
        problem["code"]!.GetValue<string>().ShouldBe("Forbidden.UpstreamIdentity.ProofRequired");
    }
}
