
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OpenIdentityStack.Api.Tests.Fixtures;

using SharedKernel;
namespace OpenIdentityStack.Api.Tests.Admin;
/// <summary>
/// Integration tests for the Admin Users API endpoints.
/// These tests verify the HTTP workflow against the in-process API test host.
/// Currently skipped pending authentication infrastructure fixes.
/// </summary>
public sealed class UsersEndpointWorkflowTests(AppHostFixture fixture) : IAsyncLifetime
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
        string clientId = $"users-contract-{Guid.NewGuid():N}";
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

    private async Task<(Guid UserId, string Email)> CreateUserAsync(string? email = null, bool verify = false)
    {
        string userEmail = email ?? $"user-{Guid.NewGuid():N}@example.com";
        var request = new
        {
            Email = userEmail,
            DisplayName = "Test User",
            Password = "TestPassword123!"
        };

        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/users", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        Guid id = json?["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("User ID not returned.");

        if (verify)
        {
            await this._fixture.VerifyUserAsync(id);
        }

        return (id, userEmail);
    }

    [Fact]
    public async Task CreateUser_WithValidRequest_Returns201Created()
    {
        // Arrange
        var request = new
        {
            Email = $"user-{Guid.NewGuid():N}@example.com",
            DisplayName = "Contract User",
            Password = "TestPassword123!"
        };

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/users", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json.ShouldNotBeNull();
        json["id"]?.GetValue<Guid>().ShouldNotBe(Guid.Empty);
        json["email"]?.GetValue<string>().ShouldBe(request.Email);
    }

    [Fact]
    public async Task CreateUser_WithInvalidEmail_Returns400BadRequest()
    {
        // Arrange
        var request = new
        {
            Email = "not-an-email",
            DisplayName = "Invalid Email User",
            Password = "TestPassword123!"
        };

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/users", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateUser_WithMissingEmail_Returns400BadRequest()
    {
        // Arrange
        var request = new
        {
            DisplayName = "Missing Email User",
            Password = "TestPassword123!"
        };

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/users", request);;

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetUser_WithValidId_Returns200Ok()
    {
        // Arrange
        (Guid userId, string? email) = await this.CreateUserAsync();

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, $"/api/admin/users/{userId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json.ShouldNotBeNull();
        json["id"]?.GetValue<Guid>().ShouldBe(userId);
        json["email"]?.GetValue<string>().ShouldBe(email);
    }

    [Fact]
    public async Task GetUser_WithInvalidId_Returns404NotFound()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, $"/api/admin/users/{id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListUsers_Returns200WithPaginatedResponse()
    {
        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, "/api/admin/users");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json.ShouldNotBeNull();
        json["items"].ShouldNotBeNull();
    }

    [Fact]
    public async Task ListUsers_WithPagination_ReturnsRequestedPage()
    {
        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, "/api/admin/users?page=1&pageSize=2");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        json.ShouldNotBeNull();
        json["page"]?.GetValue<int>().ShouldBe(1);
        json["pageSize"]?.GetValue<int>().ShouldBe(2);
    }

    [Fact]
    public async Task UpdateUser_WithValidRequest_Returns200Ok()
    {
        // Arrange
        (Guid userId, string _) = await this.CreateUserAsync();
        var update = new { DisplayName = "Updated Name" };

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Put, $"/api/admin/users/{userId}", update);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DisableUser_WithValidId_Returns200Ok()
    {
        // Arrange - Create verified user (only Active users can be disabled)
        (Guid userId, string _) = await this.CreateUserAsync(verify: true);
        var request = new { Reason = "Test disable" };

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, $"/api/admin/users/{userId}/disable", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EnableUser_WithValidId_Returns200Ok()
    {
        // Arrange - Create verified user (only Active users can be disabled, then re-enabled)
        (Guid userId, string _) = await this.CreateUserAsync(verify: true);
        var request = new { Reason = "Test disable" };
        HttpResponseMessage disableResponse = await this.SendRequestAsync(HttpMethod.Post, $"/api/admin/users/{userId}/disable", request);
        disableResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, $"/api/admin/users/{userId}/enable");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_WithValidRequest_Returns200Ok()
    {
        // Arrange
        (Guid userId, string? email) = await this.CreateUserAsync();
        var request = new { NewPassword = "NewPassword123!" };

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, $"/api/admin/users/{userId}/reset-password", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await this._fixture.ValidateUserCredentialsAsync(email, request.NewPassword);
    }

    [Fact]
    public async Task DeleteUser_WithValidId_Returns204NoContent()
    {
        // Arrange
        (Guid userId, string _) = await this.CreateUserAsync();

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Delete, $"/api/admin/users/{userId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AdminEndpoints_WithoutAuthentication_Returns401Unauthorized()
    {
        // Act
        HttpResponseMessage response = await this.Client.GetAsync("/api/admin/users");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
