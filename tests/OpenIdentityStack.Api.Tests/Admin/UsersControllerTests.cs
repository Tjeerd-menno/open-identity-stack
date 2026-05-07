
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OpenIdentityStack.Api.Tests.Fixtures;

using SharedKernel;
namespace OpenIdentityStack.Api.Tests.Admin;
/// <summary>
/// Integration tests for the Admin Users CRUD operations.
/// These tests verify the full request/response cycle including database operations.
/// Currently skipped pending authentication infrastructure fixes.
/// </summary>
public sealed class UsersControllerTests
{
    private readonly AppHostFixture _fixture;

    public UsersControllerTests(AppHostFixture fixture)
    {
        this._fixture = fixture;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        string id = Guid.NewGuid().ToString("N").Substring(0, 8);
        return await this._fixture.CreateAuthenticatedClientAsync($"users-admin-{id}", "test-admin-secret");
    }

    private static async Task<(Guid UserId, string Email)> CreateUserAsync(HttpClient client)
    {
        string email = $"user-{Guid.NewGuid():N}@example.com";
        var request = new
        {
            Email = email,
            DisplayName = "Test User",
            Password = "TestPassword123!"
        };

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/admin/users", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        Guid id = json?["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("User ID not returned.");
        return (id, email);
    }

    [Fact]
    public async Task CreateUser_ThenGetUser_ReturnsCreatedUser()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        (Guid userId, string? email) = await CreateUserAsync(client);

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/admin/users/{userId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json.ShouldNotBeNull();
        json["id"]?.GetValue<Guid>().ShouldBe(userId);
        json["email"]?.GetValue<string>().ShouldBe(email);
    }

    [Fact]
    public async Task CreateUser_ThenListUsers_ContainsCreatedUser()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        (Guid userId, string _) = await CreateUserAsync(client);

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/admin/users?pageSize=100");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json.ShouldNotBeNull();
        json["items"]?.AsArray().Any(i => i?["id"]?.GetValue<Guid>() == userId).ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateUser_ChangesDisplayName()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        (Guid userId, string _) = await CreateUserAsync(client);
        var update = new { DisplayName = "Updated Name" };

        // Act
        HttpResponseMessage updateResponse = await client.PutAsJsonAsync($"/api/admin/users/{userId}", update);

        // Assert
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        HttpResponseMessage getResponse = await client.GetAsync($"/api/admin/users/{userId}");
        JsonNode? json = await getResponse.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json?["displayName"]?.GetValue<string>().ShouldBe("Updated Name");
    }

    [Fact]
    public async Task DisableUser_ThenEnable_RestoresAccess()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        (Guid userId, string _) = await CreateUserAsync(client);
        // Need to verify user first (only Active users can be disabled)
        await this._fixture.VerifyUserAsync(userId);
        var disableRequest = new { Reason = "Testing" };

        // Act
        HttpResponseMessage disableResponse = await client.PostAsJsonAsync($"/api/admin/users/{userId}/disable", disableRequest);
        disableResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        HttpResponseMessage enableResponse = await client.PostAsync($"/api/admin/users/{userId}/enable", null);

        // Assert
        enableResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_AllowsLoginWithNewPassword()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        (Guid userId, string? email) = await CreateUserAsync(client);
        string newPassword = "NewPassword123!";

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/admin/users/{userId}/reset-password", new { NewPassword = newPassword });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await this._fixture.ValidateUserCredentialsAsync(email, newPassword);
    }

    [Fact]
    public async Task DeleteUser_RemovesUser()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        (Guid userId, string _) = await CreateUserAsync(client);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/api/admin/users/{userId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        HttpResponseMessage getResponse = await client.GetAsync($"/api/admin/users/{userId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateUser_WithDuplicateEmail_Returns409Conflict()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        string email = $"dup-{Guid.NewGuid():N}@example.com";
        var request = new { Email = email, DisplayName = "Dup User", Password = "TestPassword123!" };
        HttpResponseMessage first = await client.PostAsJsonAsync("/api/admin/users", request);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/admin/users", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ListUsers_WithSearchFilter_ReturnsFilteredResults()
    {
        // Arrange
        HttpClient client = await this.CreateAuthenticatedClientAsync();
        string uniqueEmail = $"search-{Guid.NewGuid():N}@example.com";
        var request = new { Email = uniqueEmail, DisplayName = "Search User", Password = "TestPassword123!" };
        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/admin/users", request);
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/admin/users?search={uniqueEmail}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json.ShouldNotBeNull();
        json["items"]?.AsArray().Any(i => i?["email"]?.GetValue<string>() == uniqueEmail).ShouldBeTrue();
    }
}
