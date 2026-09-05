
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Api.Tests.Fixtures;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Authentication;
/// <summary>
/// Integration tests for federated login infrastructure.
/// Tests verify the upstream identity provider configuration and linking capabilities.
/// Note: Full external IdP interaction testing would require WireMock or similar.
/// </summary>
[Collection("AppHost")]
public sealed class FederatedLoginTests
{
    private readonly AppHostFixture _fixture;

    public FederatedLoginTests(AppHostFixture fixture)
    {
        this._fixture = fixture;
    }

    [Fact]
    public async Task ProviderManagement_RejectsAuthorityReplacementAndPreservesNonIdentityEdits()
    {
        HttpClient client = await this._fixture.CreateAuthenticatedClientAsync($"issuer-admin-{Guid.NewGuid():N}", "admin-secret-123!");
        HttpResponseMessage created = await client.PostAsJsonAsync("/api/admin/providers", new { name = $"immutable-{Guid.NewGuid():N}", authority = "https://original.example", clientId = "client" });
        created.EnsureSuccessStatusCode();
        JsonNode provider = (await created.Content.ReadFromJsonAsync<JsonNode>())!;
        string path = $"/api/admin/providers/{provider["id"]!.GetValue<Guid>()}";
        HttpResponseMessage replacement = await client.PatchAsJsonAsync(path, new { authority = "https://replacement.example" });
        replacement.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        HttpResponseMessage renamed = await client.PatchAsJsonAsync(path, new { displayName = "Renamed provider" });
        renamed.EnsureSuccessStatusCode();
        JsonNode after = (await renamed.Content.ReadFromJsonAsync<JsonNode>())!;
        after["authority"]!.GetValue<string>().ShouldBe("https://original.example");
        after["displayName"]!.GetValue<string>().ShouldBe("Renamed provider");
    }
    [Fact]
    public async Task UpstreamProvider_CanBeCreatedViaAdminApi()
    {
        // Arrange
        string clientId = $"admin-client-{Guid.NewGuid():N}";
        string clientSecret = "admin-secret-123!";
        HttpClient client = await this._fixture.CreateAuthenticatedClientAsync(clientId, clientSecret);

        string providerName = $"test-provider-{Guid.NewGuid():N}";
        var request = new
        {
            name = providerName,
            displayName = "Test OIDC Provider",
            authority = "https://login.example.com",
            clientId = "test-client-id",
            clientSecret = "test-client-secret"
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/admin/providers", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UpstreamProviders_CanBeListed()
    {
        // Arrange
        string clientId = $"admin-client-{Guid.NewGuid():N}";
        string clientSecret = "admin-secret-123!";
        HttpClient client = await this._fixture.CreateAuthenticatedClientAsync(clientId, clientSecret);

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/admin/providers");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpstreamIdentity_RawLinkToUser_RequiresProof()
    {
        // Arrange
        string clientId = $"admin-client-{Guid.NewGuid():N}";
        string clientSecret = "admin-secret-123!";
        HttpClient client = await this._fixture.CreateAuthenticatedClientAsync(clientId, clientSecret);

        // Create a user
        string email = $"federated-user-{Guid.NewGuid():N}@test.com";
        Guid userId = await this._fixture.CreateTestUserAsync(email, "Federated User", "Password123!");

        // Create an upstream provider first
        string providerName = $"link-provider-{Guid.NewGuid():N}";
        var providerRequest = new
        {
            name = providerName,
            displayName = "Link Test Provider",
            authority = "https://link.example.com",
            clientId = "link-client-id",
            clientSecret = "link-client-secret"
        };
        HttpResponseMessage providerResponse = await client.PostAsJsonAsync("/api/admin/providers", providerRequest);
        providerResponse.EnsureSuccessStatusCode();
        JsonNode? providerJson = await providerResponse.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        Guid providerId = providerJson!["id"]!.GetValue<Guid>();

        // Act - Link upstream identity
        var linkRequest = new
        {
            providerId = providerId,
            subjectId = "upstream-subject-123",
            email = email
        };
        HttpResponseMessage linkResponse = await client.PostAsJsonAsync($"/api/admin/users/{userId}/upstream-identities", linkRequest);

        // Assert
        linkResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpstreamIdentities_CanBeListedForUser()
    {
        // Arrange
        string clientId = $"admin-client-{Guid.NewGuid():N}";
        string clientSecret = "admin-secret-123!";
        HttpClient client = await this._fixture.CreateAuthenticatedClientAsync(clientId, clientSecret);

        string email = $"list-identities-user-{Guid.NewGuid():N}@test.com";
        Guid userId = await this._fixture.CreateTestUserAsync(email, "List Identities User", "Password123!");

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/admin/users/{userId}/upstream-identities");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpstreamProvider_CanBeDisabled()
    {
        // Arrange
        string clientId = $"admin-client-{Guid.NewGuid():N}";
        string clientSecret = "admin-secret-123!";
        HttpClient client = await this._fixture.CreateAuthenticatedClientAsync(clientId, clientSecret);

        // Create provider
        string providerName = $"disable-provider-{Guid.NewGuid():N}";
        var providerRequest = new
        {
            name = providerName,
            displayName = "Disable Test Provider",
            authority = "https://disable.example.com",
            clientId = "disable-client-id",
            clientSecret = "disable-client-secret"
        };
        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/admin/providers", providerRequest);
        createResponse.EnsureSuccessStatusCode();
        JsonNode? json = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        Guid providerId = json!["id"]!.GetValue<Guid>();

        // Act - Disable provider
        HttpResponseMessage disableResponse = await client.PostAsync($"/api/admin/providers/{providerId}/disable", null);

        // Assert - API returns OK with updated provider
        disableResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpstreamProvider_CanBeEnabled()
    {
        // Arrange
        string clientId = $"admin-client-{Guid.NewGuid():N}";
        string clientSecret = "admin-secret-123!";
        HttpClient client = await this._fixture.CreateAuthenticatedClientAsync(clientId, clientSecret);

        // Create and disable provider
        string providerName = $"enable-provider-{Guid.NewGuid():N}";
        var providerRequest = new
        {
            name = providerName,
            displayName = "Enable Test Provider",
            authority = "https://enable.example.com",
            clientId = "enable-client-id",
            clientSecret = "enable-client-secret"
        };
        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/admin/providers", providerRequest);
        createResponse.EnsureSuccessStatusCode();
        JsonNode? json = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        Guid providerId = json!["id"]!.GetValue<Guid>();

        await client.PostAsync($"/api/admin/providers/{providerId}/disable", null);

        // Act - Enable provider
        HttpResponseMessage enableResponse = await client.PostAsync($"/api/admin/providers/{providerId}/enable", null);

        // Assert - API returns OK with updated provider
        enableResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UpstreamProvider_WithInvalidAuthority_ReturnsBadRequest()
    {
        // Arrange
        string clientId = $"admin-client-{Guid.NewGuid():N}";
        string clientSecret = "admin-secret-123!";
        HttpClient client = await this._fixture.CreateAuthenticatedClientAsync(clientId, clientSecret);

        string providerName = $"invalid-provider-{Guid.NewGuid():N}";
        var request = new
        {
            name = providerName,
            displayName = "Invalid Provider",
            authority = "not-a-valid-url",
            clientId = "client-id",
            clientSecret = "client-secret"
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/admin/providers", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpstreamIdentity_WithNonExistentProvider_StillRequiresProof()
    {
        // Arrange
        string clientId = $"admin-client-{Guid.NewGuid():N}";
        string clientSecret = "admin-secret-123!";
        HttpClient client = await this._fixture.CreateAuthenticatedClientAsync(clientId, clientSecret);

        string email = $"no-provider-user-{Guid.NewGuid():N}@test.com";
        Guid userId = await this._fixture.CreateTestUserAsync(email, "No Provider User", "Password123!");

        var linkRequest = new
        {
            providerId = Guid.NewGuid(), // Non-existent provider
            subjectId = "subject-123",
            email = email
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/admin/users/{userId}/upstream-identities", linkRequest);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task QuarantinedUpstreamIdentity_CannotBeUnlinkedThroughOrdinaryEndpoint()
    {
        // Arrange
        string clientId = $"admin-client-{Guid.NewGuid():N}";
        string clientSecret = "admin-secret-123!";
        HttpClient client = await this._fixture.CreateAuthenticatedClientAsync(clientId, clientSecret);

        // Create user
        string email = $"unlink-user-{Guid.NewGuid():N}@test.com";
        Guid userId = await this._fixture.CreateTestUserAsync(email, "Unlink User", "Password123!");

        // Create provider
        string providerName = $"unlink-provider-{Guid.NewGuid():N}";
        var providerRequest = new
        {
            name = providerName,
            displayName = "Unlink Test Provider",
            authority = "https://unlink.example.com",
            clientId = "unlink-client-id",
            clientSecret = "unlink-client-secret"
        };
        HttpResponseMessage providerResponse = await client.PostAsJsonAsync("/api/admin/providers", providerRequest);
        JsonNode? providerJson = await providerResponse.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        Guid providerId = providerJson!["id"]!.GetValue<Guid>();

        // Seed a preexisting identity independently of the disabled raw-link endpoint.
        await this._fixture.ExecuteDbContextAsync(async dbContext =>
        {
            User user = await dbContext.Users.SingleAsync(user => user.Id == new UserId(userId));
            UpstreamProvider provider = await dbContext.UpstreamProviders.SingleAsync(provider => provider.Id == new UpstreamProviderId(providerId));
            user.LinkUpstreamIdentity(provider.Id, provider.Name, "unlink-subject-123", email).IsSuccess.ShouldBeTrue();
            await dbContext.SaveChangesAsync();
        });

        // Act - Unlink
        HttpResponseMessage unlinkResponse = await client.DeleteAsync($"/api/admin/users/{userId}/upstream-identities/{providerId}");

        // Assert
        unlinkResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await this._fixture.ExecuteDbContextAsync(async dbContext =>
        {
            User user = await dbContext.Users.SingleAsync(user => user.Id == new UserId(userId));
            user.UpstreamIdentities.ShouldContain(identity => identity.ProviderId == new UpstreamProviderId(providerId) && identity.IsQuarantined);
        });
    }
}
