using OpenIdentityStack.Api.Tests.Fixtures;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace OpenIdentityStack.Api.Tests.Admin.Applications;

public sealed class ApplicationTypePolicyEndpointTests(AppHostFixture fixture) : IAsyncLifetime
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
    public async Task GetPolicies_ReturnsApplicationTypePolicyMetadata()
    {
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, "/api/admin/applications/policies/types");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json.ShouldNotBeNull();

        JsonArray policies = json.AsArray();
        JsonNode? web = policies.SingleOrDefault(node => node?["applicationType"]?.GetValue<string>() == "Web");
        JsonNode? device = policies.SingleOrDefault(node => node?["applicationType"]?.GetValue<string>() == "Device");

        web.ShouldNotBeNull();
        web["defaultClientProfile"]?.GetValue<string>().ShouldBe("Confidential");
        web["defaultGrantTypes"]?.AsArray().Select(node => node?.GetValue<string>()).ShouldContain("authorization_code");
        web["options"]?["clientSecrets"]?.GetValue<string>().ShouldBe("Available");

        device.ShouldNotBeNull();
        device["isSelectable"]?.GetValue<bool>().ShouldBeFalse();
        device["unavailabilityReason"]?.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateApplication_WithValidSinglePageDefaults_ReturnsCreated()
    {
        var request = new
        {
            ClientId = $"spa-{Guid.NewGuid():N}",
            DisplayName = "Orders SPA",
            Description = "Orders browser app",
            Type = "SinglePage",
            ClientType = "Public",
            AllowedGrantTypes = new[] { "authorization_code" },
            AllowedScopes = new[] { "openid", "orders.read" },
            RedirectUris = new[] { "https://spa.example.com/callback" },
            PostLogoutRedirectUris = new[] { "https://spa.example.com/logout/callback" },
            RequirePkce = true,
            RequireConsent = true
        };

        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/applications", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json.ShouldNotBeNull();
        json["type"]?.GetValue<string>().ShouldBe("SinglePage");
        json["clientType"]?.GetValue<string>().ShouldBe("Public");
    }

    [Fact]
    public async Task CreateApplication_WithReservedDeviceType_ReturnsBadRequest()
    {
        var request = new
        {
            ClientId = $"device-{Guid.NewGuid():N}",
            DisplayName = "Living Room Device",
            Description = "Device flow app",
            Type = "Device",
            ClientType = "Public",
            AllowedGrantTypes = new[] { "urn:ietf:params:oauth:grant-type:device_code" },
            AllowedScopes = new[] { "openid" },
            RedirectUris = Array.Empty<string>(),
            PostLogoutRedirectUris = Array.Empty<string>(),
            RequirePkce = false,
            RequireConsent = true
        };

        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/applications", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json.ShouldNotBeNull();
        json["error"]?.GetValue<string>().ShouldBe("Validation.Application.TypeNotAvailable");
    }

    [Fact]
    public async Task CreateApplication_WithMachineToMachineConsentEnabled_ReturnsBadRequest()
    {
        var request = new
        {
            ClientId = $"worker-{Guid.NewGuid():N}",
            DisplayName = "Orders Worker",
            Description = "Background worker",
            Type = "MachineToMachine",
            ClientType = "Confidential",
            AllowedGrantTypes = new[] { "client_credentials" },
            AllowedScopes = new[] { "orders.read" },
            RedirectUris = Array.Empty<string>(),
            PostLogoutRedirectUris = Array.Empty<string>(),
            RequirePkce = false,
            RequireConsent = true
        };

        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/applications", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json.ShouldNotBeNull();
        json["error"]?.GetValue<string>().ShouldBe("Validation.Application.ConsentNotAllowed");
    }

    [Fact]
    public async Task ConfigureApplicationOAuth_WhenTypeChanges_ReturnsBadRequest()
    {
        JsonNode created = await this.CreateApplicationAsync();
        Guid id = created["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("ID not returned.");
        var request = new
        {
            Type = "MachineToMachine",
            ClientType = "Confidential",
            AllowedGrantTypes = new[] { "client_credentials" },
            AllowedScopes = new[] { "orders.read" },
            RedirectUris = Array.Empty<string>(),
            PostLogoutRedirectUris = Array.Empty<string>(),
            RequirePkce = false,
            RequireConsent = false
        };

        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Put, $"/api/admin/applications/{id}/oauth", request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json.ShouldNotBeNull();
        json["error"]?.GetValue<string>().ShouldBe("Validation.Application.TypeChangeNotAllowed");
    }

    private async Task AuthenticateAsync()
    {
        string clientId = $"application-policy-{Guid.NewGuid():N}";
        const string clientSecret = "test-secret-123";

        await this.fixture.CreateServiceAccountAsync(clientId, clientSecret);
        this.accessToken = await this.fixture.GetAccessTokenAsync(clientId, clientSecret);
    }

    private async Task<JsonNode> CreateApplicationAsync()
    {
        var request = new
        {
            ClientId = $"web-{Guid.NewGuid():N}",
            DisplayName = "Orders Web",
            Description = "Orders web app",
            Type = "Web",
            ClientType = "Confidential",
            AllowedGrantTypes = new[] { "authorization_code" },
            AllowedScopes = new[] { "openid", "orders.read" },
            RedirectUris = new[] { "https://orders.example.com/callback" },
            PostLogoutRedirectUris = Array.Empty<string>(),
            RequirePkce = true,
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
