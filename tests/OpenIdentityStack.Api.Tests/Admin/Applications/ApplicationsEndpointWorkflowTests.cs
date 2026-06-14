using OpenIdentityStack.Api.Tests.Fixtures;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace OpenIdentityStack.Api.Tests.Admin.Applications;

public sealed class ApplicationsEndpointWorkflowTests(AppHostFixture fixture) : IAsyncLifetime
{
    private readonly AppHostFixture fixture = fixture;
    private HttpClient client = null!;
    private string? accessToken;

    public async ValueTask InitializeAsync()
    {
        this.client = this.fixture.HttpClient!;
        await this.AuthenticateAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CreateApplication_WithValidRequest_Returns201Created()
    {
        var request = new
        {
            ClientId = $"app-{Guid.NewGuid():N}",
            DisplayName = "Orders Web",
            Description = "Orders web app",
            Profile = "Web",
            ClientType = "Confidential",
            AllowedGrantTypes = new[] { "authorization_code" },
            AllowedScopes = new[] { "openid", "orders.read" },
            RedirectUris = new[] { "https://orders.example.com/callback" },
            PostLogoutRedirectUris = Array.Empty<string>(),
            RequirePkce = false,
            RequireConsent = true
        };

        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/applications", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        response.Headers.Location.ShouldNotBeNull();
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json.ShouldNotBeNull();
        json["id"]?.GetValue<Guid>().ShouldNotBe(Guid.Empty);
        json["clientId"]?.GetValue<string>().ShouldBe(request.ClientId);
        json["displayName"]?.GetValue<string>().ShouldBe("Orders Web");
        json["status"]?.GetValue<string>().ShouldBe("Active");
    }

    [Fact]
    public async Task CreateApplication_WithInitialClientSecret_ReturnsInitialSecretAndListsCredentialMetadataOnly()
    {
        var request = new
        {
            ClientId = $"app-{Guid.NewGuid():N}",
            DisplayName = "Orders API",
            Description = "Orders API",
            Profile = "MachineToMachine",
            ClientType = "Confidential",
            AllowedGrantTypes = new[] { "client_credentials" },
            AllowedScopes = new[] { "orders.read" },
            RedirectUris = Array.Empty<string>(),
            PostLogoutRedirectUris = Array.Empty<string>(),
            RequirePkce = false,
            RequireConsent = false,
            InitialCredential = new
            {
                Type = "ClientSecret",
                Description = "Initial secret",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
            }
        };

        HttpResponseMessage createResponse = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/applications", request);

        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await createResponse.Content.ReadAsStringAsync());
        JsonNode created = await createResponse.Content.ReadFromJsonAsync<JsonNode>()
            ?? throw new InvalidOperationException("Application response not returned.");
        Guid applicationId = created["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("Application ID not returned.");
        string initialSecret = created["initialSecret"]?.GetValue<string>() ?? string.Empty;
        initialSecret.ShouldNotBeNullOrWhiteSpace();

        HttpResponseMessage credentialsResponse = await this.SendRequestAsync(
            HttpMethod.Get,
            $"/api/admin/applications/{applicationId}/credentials");

        credentialsResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await credentialsResponse.Content.ReadAsStringAsync());
        JsonNode credentials = await credentialsResponse.Content.ReadFromJsonAsync<JsonNode>()
            ?? throw new InvalidOperationException("Credentials response not returned.");
        JsonNode credential = credentials.AsArray().Single()
            ?? throw new InvalidOperationException("Credential response not returned.");
        credential["description"]?.GetValue<string>().ShouldBe("Initial secret");
        credential["clientSecret"].ShouldBeNull();
        credential["secretHash"].ShouldBeNull();
        credentials.ToJsonString().ShouldNotContain(initialSecret);
        credentials.ToJsonString().ShouldNotContain("hashed-secret");
    }

    [Fact]
    public async Task CreateApplication_WithMissingInitialCredentialType_ReturnsBadRequest()
    {
        var request = new
        {
            ClientId = $"app-{Guid.NewGuid():N}",
            DisplayName = "Orders API",
            Description = "Orders API",
            Profile = "MachineToMachine",
            ClientType = "Confidential",
            AllowedGrantTypes = new[] { "client_credentials" },
            AllowedScopes = new[] { "orders.read" },
            RedirectUris = Array.Empty<string>(),
            PostLogoutRedirectUris = Array.Empty<string>(),
            RequirePkce = false,
            RequireConsent = false,
            InitialCredential = new
            {
                Description = "Initial secret",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30)
            }
        };

        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/applications", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetApplication_WithValidId_Returns200Ok()
    {
        JsonNode created = await this.CreateApplicationAsync();
        Guid id = created["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("ID not returned.");

        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, $"/api/admin/applications/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json.ShouldNotBeNull();
        json["id"]?.GetValue<Guid>().ShouldBe(id);
        json["clientId"]?.GetValue<string>().ShouldNotBeNull();
    }

    [Fact]
    public async Task ListApplications_Returns200WithPaginatedResponse()
    {
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, "/api/admin/applications?page=1&pageSize=5");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json.ShouldNotBeNull();
        json["items"].ShouldNotBeNull();
        json["page"]?.GetValue<int>().ShouldBe(1);
        json["pageSize"]?.GetValue<int>().ShouldBe(5);
    }

    [Fact]
    public async Task DisableAndEnableApplication_WithValidId_Returns200Ok()
    {
        JsonNode created = await this.CreateApplicationAsync();
        Guid id = created["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("ID not returned.");

        HttpResponseMessage disableResponse = await this.SendRequestAsync(HttpMethod.Post, $"/api/admin/applications/{id}/disable");
        HttpResponseMessage enableResponse = await this.SendRequestAsync(HttpMethod.Post, $"/api/admin/applications/{id}/enable");

        disableResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        enableResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateApplicationMetadata_WithValidRequest_Returns200Ok()
    {
        JsonNode created = await this.CreateApplicationAsync();
        Guid id = created["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("ID not returned.");
        var request = new
        {
            DisplayName = "Updated Orders Web",
            Description = "Updated description"
        };

        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Patch, $"/api/admin/applications/{id}", request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json.ShouldNotBeNull();
        json["displayName"]?.GetValue<string>().ShouldBe("Updated Orders Web");
        json["description"]?.GetValue<string>().ShouldBe("Updated description");
    }

    [Fact]
    public async Task ConfigureApplicationOAuth_WithValidRequest_Returns200Ok()
    {
        JsonNode created = await this.CreateApplicationAsync();
        Guid id = created["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("ID not returned.");
        var request = new
        {
            Profile = "Web",
            ClientType = "Confidential",
            AllowedGrantTypes = new[] { "authorization_code" },
            AllowedScopes = new[] { "openid", "orders.read", "orders.write" },
            RedirectUris = new[] { "https://orders.example.com/updated-callback" },
            PostLogoutRedirectUris = Array.Empty<string>(),
            RequirePkce = false,
            RequireConsent = false
        };

        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Put, $"/api/admin/applications/{id}/oauth", request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json.ShouldNotBeNull();
        json["profile"]?.GetValue<string>().ShouldBe("Web");
        json["allowedGrantTypes"]?.AsArray().Select(node => node?.GetValue<string>()).ShouldContain("authorization_code");
    }

    [Fact]
    public async Task DeleteApplication_WithValidId_Returns204NoContent()
    {
        JsonNode created = await this.CreateApplicationAsync();
        Guid id = created["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("ID not returned.");

        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Delete, $"/api/admin/applications/{id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task AuthenticateAsync()
    {
        string clientId = $"applications-workflow-{Guid.NewGuid():N}";
        const string clientSecret = "test-secret-123";

        await this.fixture.CreateServiceAccountAsync(clientId, clientSecret);
        this.accessToken = await this.fixture.GetAccessTokenAsync(clientId, clientSecret);
    }

    private async Task<JsonNode> CreateApplicationAsync()
    {
        var request = new
        {
            ClientId = $"app-{Guid.NewGuid():N}",
            DisplayName = "Orders Web",
            Description = "Orders web app",
            Profile = "Web",
            ClientType = "Confidential",
            AllowedGrantTypes = new[] { "authorization_code" },
            AllowedScopes = new[] { "openid", "orders.read" },
            RedirectUris = new[] { "https://orders.example.com/callback" },
            PostLogoutRedirectUris = Array.Empty<string>(),
            RequirePkce = false,
            RequireConsent = true
        };

        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/applications", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonNode>()
            ?? throw new InvalidOperationException("Application response not returned.");
    }

    private async Task<HttpResponseMessage> SendRequestAsync(HttpMethod method, string url, object? content = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(this.accessToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", this.accessToken);
        }

        if (content is not null)
        {
            request.Content = JsonContent.Create(content);
        }

        return await this.client.SendAsync(request);
    }
}
