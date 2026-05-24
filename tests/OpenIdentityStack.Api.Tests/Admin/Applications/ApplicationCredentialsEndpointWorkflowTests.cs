using OpenIdentityStack.Api.Tests.Fixtures;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace OpenIdentityStack.Api.Tests.Admin.Applications;

public sealed class ApplicationCredentialsEndpointWorkflowTests(AppHostFixture fixture) : IAsyncLifetime
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
    public async Task AddSecret_WithConfidentialApplication_ReturnsOneTimeSecret()
    {
        Guid applicationId = await this.CreateMachineToMachineApplicationAsync();
        var request = new
        {
            Description = "Primary secret",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            RevokeExisting = false
        };

        HttpResponseMessage response = await this.SendRequestAsync(
            HttpMethod.Post,
            $"/api/admin/applications/{applicationId}/credentials/client-secrets",
            request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        JsonNode json = await ReadJsonAsync(response);
        json["credentialId"]?.GetValue<Guid>().ShouldNotBe(Guid.Empty);
        json["clientSecret"]?.GetValue<string>().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AddSecret_WithRevokeExisting_RevokesExistingSecret()
    {
        Guid applicationId = await this.CreateMachineToMachineApplicationAsync();
        JsonNode first = await this.AddSecretAsync(applicationId, revokeExisting: false);
        Guid firstCredentialId = first["credentialId"]?.GetValue<Guid>() ?? throw new InvalidOperationException("Credential ID not returned.");

        JsonNode second = await this.AddSecretAsync(applicationId, revokeExisting: true);

        second["credentialId"]?.GetValue<Guid>().ShouldNotBe(firstCredentialId);
        JsonNode credentials = await this.ListCredentialsAsync(applicationId);
        JsonNode? revoked = credentials.AsArray()
            .SingleOrDefault(item => item?["id"]?.GetValue<Guid>() == firstCredentialId);
        revoked.ShouldNotBeNull();
        revoked!["revokedAt"].ShouldNotBeNull();
    }

    [Fact]
    public async Task AddCertificate_WithConfidentialApplication_ReturnsCreatedAndCredentialMetadata()
    {
        Guid applicationId = await this.CreateMachineToMachineApplicationAsync();
        var request = new
        {
            Thumbprint = $"THUMB{Guid.NewGuid():N}",
            Subject = "CN=worker.example.com",
            Description = "Worker certificate",
            ExpiresAt = DateTimeOffset.UtcNow.AddYears(1)
        };

        HttpResponseMessage response = await this.SendRequestAsync(
            HttpMethod.Post,
            $"/api/admin/applications/{applicationId}/credentials/certificates",
            request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        JsonNode json = await ReadJsonAsync(response);
        Guid credentialId = json["credentialId"]?.GetValue<Guid>() ?? throw new InvalidOperationException("Credential ID not returned.");
        JsonNode credentials = await this.ListCredentialsAsync(applicationId);
        JsonNode? certificate = credentials.AsArray()
            .SingleOrDefault(item => item?["id"]?.GetValue<Guid>() == credentialId);
        certificate.ShouldNotBeNull();
        certificate!["thumbprint"]?.GetValue<string>().ShouldBe(request.Thumbprint);
        certificate["subject"]?.GetValue<string>().ShouldBe(request.Subject);
    }

    [Fact]
    public async Task RevokeCredential_WithExistingCredential_ReturnsNoContentAndMarksRevoked()
    {
        Guid applicationId = await this.CreateMachineToMachineApplicationAsync();
        JsonNode secret = await this.AddSecretAsync(applicationId, revokeExisting: false);
        Guid credentialId = secret["credentialId"]?.GetValue<Guid>() ?? throw new InvalidOperationException("Credential ID not returned.");

        HttpResponseMessage response = await this.SendRequestAsync(
            HttpMethod.Delete,
            $"/api/admin/applications/{applicationId}/credentials/{credentialId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        JsonNode credentials = await this.ListCredentialsAsync(applicationId);
        JsonNode? revoked = credentials.AsArray()
            .SingleOrDefault(item => item?["id"]?.GetValue<Guid>() == credentialId);
        revoked.ShouldNotBeNull();
        revoked!["revokedAt"].ShouldNotBeNull();
    }

    [Fact]
    public async Task AddSecret_WithPublicApplication_ReturnsBadRequest()
    {
        Guid applicationId = await this.CreatePublicApplicationAsync();
        var request = new
        {
            Description = "Invalid public secret",
            ExpiresAt = (DateTimeOffset?)null,
            RevokeExisting = false
        };

        HttpResponseMessage response = await this.SendRequestAsync(
            HttpMethod.Post,
            $"/api/admin/applications/{applicationId}/credentials/client-secrets",
            request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
    }

    private async Task AuthenticateAsync()
    {
        string clientId = $"application-credentials-workflow-{Guid.NewGuid():N}";
        const string clientSecret = "test-secret-123";

        await this.fixture.CreateServiceAccountAsync(clientId, clientSecret);
        this.accessToken = await this.fixture.GetAccessTokenAsync(clientId, clientSecret);
    }

    private async Task<JsonNode> AddSecretAsync(Guid applicationId, bool revokeExisting)
    {
        var request = new
        {
            Description = "Secret",
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
            RevokeExisting = revokeExisting
        };

        HttpResponseMessage response = await this.SendRequestAsync(
            HttpMethod.Post,
            $"/api/admin/applications/{applicationId}/credentials/client-secrets",
            request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await ReadJsonAsync(response);
    }

    private async Task<JsonNode> ListCredentialsAsync(Guid applicationId)
    {
        HttpResponseMessage response = await this.SendRequestAsync(
            HttpMethod.Get,
            $"/api/admin/applications/{applicationId}/credentials");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await ReadJsonAsync(response);
    }

    private async Task<Guid> CreateMachineToMachineApplicationAsync()
    {
        var request = new
        {
            ClientId = $"m2m-{Guid.NewGuid():N}",
            DisplayName = "Worker Application",
            Description = "Worker application",
            Type = "MachineToMachine",
            ClientType = "Confidential",
            AllowedGrantTypes = new[] { "client_credentials" },
            AllowedScopes = new[] { "api" },
            RedirectUris = Array.Empty<string>(),
            PostLogoutRedirectUris = Array.Empty<string>(),
            RequirePkce = false,
            RequireConsent = false
        };

        return await this.CreateApplicationAsync(request);
    }

    private async Task<Guid> CreatePublicApplicationAsync()
    {
        var request = new
        {
            ClientId = $"spa-{Guid.NewGuid():N}",
            DisplayName = "Public SPA",
            Description = "Public application",
            Type = "SinglePage",
            ClientType = "Public",
            AllowedGrantTypes = new[] { "authorization_code" },
            AllowedScopes = new[] { "openid", "api" },
            RedirectUris = new[] { "https://spa.example.com/callback" },
            PostLogoutRedirectUris = Array.Empty<string>(),
            RequirePkce = true,
            RequireConsent = false
        };

        return await this.CreateApplicationAsync(request);
    }

    private async Task<Guid> CreateApplicationAsync(object request)
    {
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/applications", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        JsonNode json = await ReadJsonAsync(response);
        return json["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("Application ID not returned.");
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

    private static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonNode>()
        ?? throw new InvalidOperationException("JSON response not returned.");
}
