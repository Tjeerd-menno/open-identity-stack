using System.Net;
using OpenIdentityStack.Api.Tests.Fixtures;

namespace OpenIdentityStack.Api.Tests.Admin;

public sealed class RemovedCredentialCutoverTests(AppHostFixture fixture)
{
    [Theory]
    [InlineData("GET", "/api/admin/security/cutover-readiness")]
    [InlineData("POST", "/api/admin/security/emergency-access-evidence")]
    [InlineData("PUT", "/api/admin/security/business-resources/11111111-1111-1111-1111-111111111111/token-window-review")]
    [InlineData("POST", "/api/admin/security/credential-cutovers")]
    public async Task CutoverEndpointsAreUnavailable(string method, string path)
    {
        using HttpClient client = await fixture.CreateAuthenticatedClientAsync(
            $"removed-cutover-{Guid.NewGuid():N}", "TestSecret123!");
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        using HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
