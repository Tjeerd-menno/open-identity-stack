using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OpenIdentityStack.Api.Tests.Fixtures;

namespace OpenIdentityStack.Api.Tests.Admin.ApplicationPermissions;

public sealed class ApplicationPermissionsApiTests(AppHostFixture fixture)
{
    private readonly AppHostFixture fixture = fixture;

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        string id = Guid.NewGuid().ToString("N")[..8];
        return await this.fixture.CreateAuthenticatedClientAsync($"application-permissions-admin-{id}", "test-admin-secret");
    }

    [Fact]
    public async Task RegisterApplicationManifest_WhenApplicationIsMissing_ReturnsBadRequest()
    {
        using HttpClient client = await this.CreateAuthenticatedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/admin/application-permissions/applications",
            new
            {
                Permissions = new[]
                {
                    new
                    {
                        Name = "read:patients",
                        Description = "Allows reading patient data",
                        Category = "Patients",
                    },
                },
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json?["error"]?.GetValue<string>().ShouldBe("PermissionManifest.ApplicationRequired");
    }

    [Fact]
    public async Task RegisterApplicationManifest_WhenPermissionsAreMissing_ReturnsBadRequest()
    {
        using HttpClient client = await this.CreateAuthenticatedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/admin/application-permissions/applications",
            new
            {
                Application = new
                {
                    Id = "patient-api",
                    Name = "Patient API",
                    Version = "1.0.0",
                },
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json?["error"]?.GetValue<string>().ShouldBe("PermissionManifest.PermissionsRequired");
    }

    [Fact]
    public async Task ImportApplicationManifest_WhenEndpointIsNotWellKnownPermissionsUrl_ReturnsBadRequest()
    {
        using HttpClient client = await this.CreateAuthenticatedClientAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/admin/application-permissions/applications/import",
            new
            {
                Endpoint = "https://patient.example/permissions",
            });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json?["error"]?.GetValue<string>().ShouldBe("PermissionManifest.EndpointInvalid");
    }
}
