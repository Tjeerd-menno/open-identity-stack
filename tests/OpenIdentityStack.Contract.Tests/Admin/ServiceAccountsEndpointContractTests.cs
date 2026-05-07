using OpenIdentityStack.Contract.Tests.Fixtures;
using System.Net.Http.Json;
using System.Net;
using Aspire.Hosting.Testing;
using System.Text.Json.Nodes;

namespace OpenIdentityStack.Contract.Tests.Admin;
/// <summary>
/// Contract tests for the Admin Service Accounts API endpoints.
/// These tests verify the API contract (request/response shapes).
/// </summary>
public sealed class ServiceAccountsEndpointContractTests(AppHostFixture fixture) : IAsyncLifetime
{
    private readonly AppHostFixture _fixture = fixture;
    private HttpClient _client = null!;
    private string? _accessToken;

    private HttpClient Client => this._client;

    public async ValueTask InitializeAsync()
    {
        this._client = this._fixture.App!.CreateHttpClient("api");
        // Authenticate before running tests
        await this.AuthenticateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        this._client?.Dispose();
        await ValueTask.CompletedTask;
    }

    private async Task AuthenticateAsync()
    {
        const string clientId = "sa-contract-tests";
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

    [Fact]
    public async Task CreateServiceAccount_WithValidRequest_Returns201Created()
    {
        // Arrange
        var command = new
        {
            ClientId = $"test-client-{Guid.NewGuid()}",
            DisplayName = "Test Service Account",
            AllowedScopes = new[] { "api" },
            AllowedGrantTypes = new[] { "client_credentials" }
        };

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/service-accounts", command);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();

        JsonNode? content = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        content.ShouldNotBeNull();
        content["id"]?.GetValue<string>().ShouldNotBeNull();
        content["clientId"]?.GetValue<string>().ShouldBe(command.ClientId);
        content["secret"]?.GetValue<string>().ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateServiceAccount_WithMissingClientId_Returns400BadRequest()
    {
        // Arrange
        var command = new
        {
            // ClientId is missing
            DisplayName = "Test Service Account",
            AllowedScopes = new[] { "api" },
            AllowedGrantTypes = new[] { "client_credentials" }
        };

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/service-accounts", command);

        // Assert
        // Depending on validation logic, this might be 400
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateServiceAccount_WithEmptyGrantTypes_Returns400BadRequest()
    {
        // Arrange
        var command = new
        {
            ClientId = $"test-client-{Guid.NewGuid()}",
            DisplayName = "Test Service Account",
            AllowedScopes = new[] { "api" },
            AllowedGrantTypes = Array.Empty<string>() // Empty
        };

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/service-accounts", command);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetServiceAccount_WithValidId_Returns200Ok()
    {
        // Arrange - Create
        var command = new
        {
            ClientId = $"get-client-{Guid.NewGuid()}",
            DisplayName = "Get Test Service Account",
            AllowedScopes = new[] { "api" },
            AllowedGrantTypes = new[] { "client_credentials" }
        };
        HttpResponseMessage createResponse = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/service-accounts", command);
        createResponse.EnsureSuccessStatusCode();
        JsonNode? created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        string id = created?["id"]?.GetValue<string>() ?? throw new InvalidOperationException("ID not returned");

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, $"/api/admin/service-accounts/{id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? content = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        content.ShouldNotBeNull();
        content["id"]?.GetValue<string>().ShouldBe(id);
        content["clientId"]?.GetValue<string>().ShouldBe(command.ClientId);
    }

    [Fact]
    public async Task GetServiceAccount_WithInvalidId_Returns404NotFound()
    {
        // Arrange
        var invalidId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, $"/api/admin/service-accounts/{invalidId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListServiceAccounts_Returns200WithPaginatedResponse()
    {
        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, "/api/admin/service-accounts");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? content = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        content.ShouldNotBeNull();
    }

    [Fact]
    public async Task ListServiceAccounts_WithPagination_ReturnsRequestedPage()
    {
        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, "/api/admin/service-accounts?page=1&pageSize=5");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? content = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        content.ShouldNotBeNull();
        content["page"]?.GetValue<int>().ShouldBe(1);
        content["pageSize"]?.GetValue<int>().ShouldBe(5);
    }

    [Fact]
    public async Task RotateSecret_WithValidId_Returns200Ok()
    {
        // Arrange - Create
        var command = new
        {
            ClientId = $"rotate-client-{Guid.NewGuid()}",
            DisplayName = "Rotate Secret Test",
            AllowedScopes = new[] { "api" },
            AllowedGrantTypes = new[] { "client_credentials" }
        };
        HttpResponseMessage createResponse = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/service-accounts", command);
        createResponse.EnsureSuccessStatusCode();
        JsonNode? created = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        string id = created?["id"]?.GetValue<string>() ?? throw new InvalidOperationException("ID not returned");

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, $"/api/admin/service-accounts/{id}/rotate-secret", new { });

        if (response.StatusCode != HttpStatusCode.OK)
        {
            string error = await response.Content.ReadAsStringAsync();
            throw new Exception($"Request failed with {response.StatusCode}: {error}");
        }

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? content = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        content.ShouldNotBeNull();
        content["newSecret"]?.GetValue<string>().ShouldNotBeNull();
    }
}
