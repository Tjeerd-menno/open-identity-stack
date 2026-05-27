using OpenIdentityStack.Api.Tests.Fixtures;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace OpenIdentityStack.Api.Tests.Authentication;

[Collection("AppHost")]
public sealed class ApplicationClientCredentialsFlowTests(AppHostFixture fixture) : IAsyncLifetime
{
    private static readonly string[] ClientCredentialsGrantTypes = ["client_credentials"];
    private static readonly string[] ApiScopes = ["api"];

    private readonly AppHostFixture fixture = fixture;
    private HttpClient client = null!;
    private string? accessToken;

    public async ValueTask InitializeAsync()
    {
        this.client = this.fixture.HttpClient!;
        string adminClientId = $"application-token-flow-admin-{Guid.NewGuid():N}";
        const string adminSecret = "test-secret-123";
        await this.fixture.CreateServiceAccountAsync(adminClientId, adminSecret);
        this.accessToken = await this.fixture.GetAccessTokenAsync(adminClientId, adminSecret);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ClientCredentials_WithApplicationSecret_ReturnsAccessToken()
    {
        Guid applicationId = await this.CreateMachineToMachineApplicationAsync();
        string secret = await this.AddSecretAsync(applicationId);

        HttpResponseMessage response = await this.RequestTokenAsync(await this.GetClientIdAsync(applicationId), secret);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        JsonNode json = await ReadJsonAsync(response);
        json["access_token"]?.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ClientCredentials_WithRevokedApplicationSecret_ReturnsInvalidClient()
    {
        Guid applicationId = await this.CreateMachineToMachineApplicationAsync();
        JsonNode secretResponse = await this.AddSecretResponseAsync(applicationId);
        string secret = secretResponse["clientSecret"]?.GetValue<string>() ?? throw new InvalidOperationException("Secret not returned.");
        Guid credentialId = secretResponse["credentialId"]?.GetValue<Guid>() ?? throw new InvalidOperationException("Credential ID not returned.");
        await this.SendAdminRequestAsync(HttpMethod.Delete, $"/api/admin/applications/{applicationId}/credentials/{credentialId}");

        HttpResponseMessage response = await this.RequestTokenAsync(await this.GetClientIdAsync(applicationId), secret);

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
        string content = await response.Content.ReadAsStringAsync();
        (content.Contains("invalid_client", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("invalid_request", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue(content);
    }

    [Fact]
    public async Task ClientCredentials_WithDisabledApplication_ReturnsInvalidClient()
    {
        Guid applicationId = await this.CreateMachineToMachineApplicationAsync();
        string secret = await this.AddSecretAsync(applicationId);
        await this.SendAdminRequestAsync(HttpMethod.Post, $"/api/admin/applications/{applicationId}/disable");

        HttpResponseMessage response = await this.RequestTokenAsync(await this.GetClientIdAsync(applicationId), secret);

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
        string content = await response.Content.ReadAsStringAsync();
        (content.Contains("invalid_client", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("invalid_request", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue(content);
    }

    [Fact]
    public async Task ClientCredentials_WithCertificateCredentialButNoPresentedCertificate_ReturnsInvalidClient()
    {
        Guid applicationId = await this.CreateMachineToMachineApplicationAsync();
        await this.SendAdminRequestAsync(
            HttpMethod.Post,
            $"/api/admin/applications/{applicationId}/credentials/certificates",
            new
            {
                Thumbprint = $"THUMB{Guid.NewGuid():N}",
                Subject = "CN=worker.example.com",
                Description = "Worker certificate",
                ExpiresAt = DateTimeOffset.UtcNow.AddYears(1)
            });

        HttpResponseMessage response = await this.client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = await this.GetClientIdAsync(applicationId),
            ["scope"] = "api"
        }));

        response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
        string content = await response.Content.ReadAsStringAsync();
        (content.Contains("invalid_client", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("invalid_request", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue(content);
    }

    private async Task<Guid> CreateMachineToMachineApplicationAsync()
    {
        string clientId = $"m2m-token-{Guid.NewGuid():N}";
        HttpResponseMessage response = await this.SendAdminRequestAsync(
            HttpMethod.Post,
            "/api/admin/applications",
            new
            {
                ClientId = clientId,
                DisplayName = "Token Worker",
                Description = "Token worker application",
                Profile = "MachineToMachine",
                ClientType = "Confidential",
                AllowedGrantTypes = ClientCredentialsGrantTypes,
                AllowedScopes = ApiScopes,
                RedirectUris = Array.Empty<string>(),
                PostLogoutRedirectUris = Array.Empty<string>(),
                RequirePkce = false,
                RequireConsent = false
            });
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        JsonNode json = await ReadJsonAsync(response);
        return json["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("Application ID not returned.");
    }

    private async Task<string> AddSecretAsync(Guid applicationId)
    {
        JsonNode json = await this.AddSecretResponseAsync(applicationId);
        return json["clientSecret"]?.GetValue<string>() ?? throw new InvalidOperationException("Secret not returned.");
    }

    private async Task<JsonNode> AddSecretResponseAsync(Guid applicationId)
    {
        HttpResponseMessage response = await this.SendAdminRequestAsync(
            HttpMethod.Post,
            $"/api/admin/applications/{applicationId}/credentials/client-secrets",
            new
            {
                Description = "Token secret",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
                RevokeExisting = true
            });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await ReadJsonAsync(response);
    }

    private async Task<string> GetClientIdAsync(Guid applicationId)
    {
        HttpResponseMessage response = await this.SendAdminRequestAsync(HttpMethod.Get, $"/api/admin/applications/{applicationId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        JsonNode json = await ReadJsonAsync(response);
        return json["clientId"]?.GetValue<string>() ?? throw new InvalidOperationException("Client ID not returned.");
    }

    private async Task<HttpResponseMessage> RequestTokenAsync(string clientId, string clientSecret) =>
        await this.client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = "api"
        }));

    private async Task<HttpResponseMessage> SendAdminRequestAsync(HttpMethod method, string url, object? content = null)
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

    private static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonNode>()
        ?? throw new InvalidOperationException("JSON response not returned.");
}
