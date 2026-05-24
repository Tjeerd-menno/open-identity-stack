using OpenIdentityStack.Api.Tests.Fixtures;
using System.Net;
using System.Net.Http.Json;

namespace OpenIdentityStack.Api.Tests.Admin.Applications;

public sealed class ServiceAccountsCompatibilityEndpointTests(AppHostFixture fixture) : IAsyncLifetime
{
    private static readonly string[] AllowedScopes = ["api"];
    private static readonly string[] AllowedGrantTypes = ["client_credentials"];

    private readonly AppHostFixture fixture = fixture;
    private HttpClient client = null!;

    public async ValueTask InitializeAsync()
    {
        string clientId = $"service-accounts-compatibility-{Guid.NewGuid():N}";
        const string clientSecret = "test-secret-123";
        this.client = await this.fixture.CreateAuthenticatedClientAsync(clientId, clientSecret);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Theory]
    [InlineData(HttpMethodName.Get, "/api/admin/service-accounts?page=1&pageSize=5")]
    [InlineData(HttpMethodName.Post, "/api/admin/service-accounts")]
    [InlineData(HttpMethodName.Get, "/api/admin/service-accounts/00000000-0000-0000-0000-000000000001")]
    [InlineData(HttpMethodName.Patch, "/api/admin/service-accounts/00000000-0000-0000-0000-000000000001")]
    [InlineData(HttpMethodName.Delete, "/api/admin/service-accounts/00000000-0000-0000-0000-000000000001")]
    [InlineData(HttpMethodName.Post, "/api/admin/service-accounts/00000000-0000-0000-0000-000000000001/disable")]
    [InlineData(HttpMethodName.Post, "/api/admin/service-accounts/00000000-0000-0000-0000-000000000001/enable")]
    [InlineData(HttpMethodName.Post, "/api/admin/service-accounts/00000000-0000-0000-0000-000000000001/rotate-secret")]
    [InlineData(HttpMethodName.Post, "/api/admin/service-accounts/00000000-0000-0000-0000-000000000001/certificates")]
    public async Task LegacyServiceAccountsRoute_ReturnsNotFound(HttpMethodName method, string requestUri)
    {
        HttpRequestMessage request = new(new HttpMethod(method.ToString()), requestUri);
        if (method is HttpMethodName.Post or HttpMethodName.Patch)
        {
            request.Content = JsonContent.Create(new
            {
                ClientId = $"legacy-service-account-{Guid.NewGuid():N}",
                DisplayName = "Legacy Worker",
                AllowedScopes,
                AllowedGrantTypes
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
