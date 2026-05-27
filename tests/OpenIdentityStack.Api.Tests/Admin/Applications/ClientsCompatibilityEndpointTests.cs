using OpenIdentityStack.Api.Tests.Fixtures;
using System.Net;
using System.Net.Http.Json;

namespace OpenIdentityStack.Api.Tests.Admin.Applications;

public sealed class ClientsCompatibilityEndpointTests(AppHostFixture fixture) : IAsyncLifetime
{
    private static readonly string[] RedirectUris = ["https://legacy.example.com/callback"];
    private static readonly string[] AllowedScopes = ["openid", "api"];
    private static readonly string[] AllowedGrantTypes = ["authorization_code"];

    private readonly AppHostFixture fixture = fixture;
    private HttpClient client = null!;

    public async ValueTask InitializeAsync()
    {
        string clientId = $"clients-compatibility-{Guid.NewGuid():N}";
        const string clientSecret = "test-secret-123";
        this.client = await this.fixture.CreateAuthenticatedClientAsync(clientId, clientSecret);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory]
    [InlineData(HttpMethodName.Get, "/api/admin/clients?page=1&pageSize=5")]
    [InlineData(HttpMethodName.Post, "/api/admin/clients")]
    [InlineData(HttpMethodName.Get, "/api/admin/clients/00000000-0000-0000-0000-000000000001")]
    [InlineData(HttpMethodName.Patch, "/api/admin/clients/00000000-0000-0000-0000-000000000001")]
    [InlineData(HttpMethodName.Delete, "/api/admin/clients/00000000-0000-0000-0000-000000000001")]
    public async Task LegacyClientsRoute_ReturnsNotFound(HttpMethodName method, string requestUri)
    {
        HttpRequestMessage request = new(new HttpMethod(method.ToString()), requestUri);
        if (method is HttpMethodName.Post or HttpMethodName.Patch)
        {
            request.Content = JsonContent.Create(new
            {
                ClientId = $"legacy-client-{Guid.NewGuid():N}",
                DisplayName = "Legacy Web Client",
                ClientType = "Confidential",
                Description = "Removed compatibility endpoint",
                RedirectUris,
                PostLogoutRedirectUris = Array.Empty<string>(),
                AllowedScopes,
                AllowedGrantTypes,
                RequirePkce = false,
                RequireConsent = true
            });
        }

        HttpResponseMessage response = await this.client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync());
        response.Headers.Contains("Deprecation").ShouldBeFalse();
        response.Headers.Contains("X-Replaced-By").ShouldBeFalse();
    }

    public enum HttpMethodName
    {
        Get,
        Post,
        Patch,
        Delete
    }
}
