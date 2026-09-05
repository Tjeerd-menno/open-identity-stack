using System.Text.Json.Nodes;
using System.Net;
using System.Net.Http.Json;
using OpenIddict.Abstractions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

public sealed class CutoverReadinessTests(ManagementWebAppHostFixture fixture) : ManagementWebPageTest(fixture)
{
    [Fact]
    public async Task IssuedBrowserAccessCredentialAppearsInReadinessInventory()
    {
        IReadOnlyList<TokenMetadataAggregate> metadata = await Fixture.ReadTokenMetadataAsync();
                int expectedAccessTokens = metadata.Where(row => row.Type == OpenIddictConstants.TokenTypeIdentifiers.AccessToken || row.Type == OpenIddictConstants.TokenTypeHints.AccessToken)
            .Sum(row => row.Unexpired + row.UnknownExpiry);
        expectedAccessTokens.ShouldBeGreaterThan(0);
        JsonNode readiness = await ApiGetAsync("/api/admin/security/cutover-readiness");
        string summary = System.Text.Json.JsonSerializer.Serialize(metadata);
        readiness["outstandingAccessTokens"]!.GetValue<long>().ShouldBe(expectedAccessTokens, summary);
        readiness["latestAccessTokenExpiry"].ShouldNotBeNull();
        Guid resourceId = await Fixture.SeedTokenWindowResourceAsync();
        Api.DefaultRequestHeaders.Add("X-OIS-Administrative-Approval", "acknowledge");
        HttpResponseMessage reviewed = await Api.PutAsJsonAsync($"/api/admin/security/business-resources/{resourceId}/token-window-review", new
        {
            Mechanism = "OfflineExpiry", ResidualSeconds = 0, EvidenceReference = "isolated-regression:zero-window-must-be-rejected"
        });
        reviewed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        JsonNode gated = await ApiGetAsync("/api/admin/security/cutover-readiness");
        JsonNode window = gated["businessResources"]!.AsArray().Single(resource => resource!["resourceId"]!.GetValue<Guid>() == resourceId)!;
        window["reviewed"]!.GetValue<bool>().ShouldBeFalse();
        gated["blockers"]!.AsArray().ShouldContain(blocker => blocker!["code"]!.GetValue<string>() == "Resource.TokenWindowUnresolved");
    }
    [Fact]
    public async Task ReadinessLoadsFromPostgreSqlAndBlocksExecutionWithoutIndependentProof()
    {
        int cutoverRequests = 0;
        Page.Request += (_, request) =>
        {
            if (request.Method == "POST" && new Uri(request.Url).AbsolutePath == "/api/admin/security/credential-cutovers")
            {
                cutoverRequests++;
            }
        };
        Task<IResponse> readinessResponse = Page.WaitForResponseAsync(response =>
            new Uri(response.Url).AbsolutePath == "/api/admin/security/cutover-readiness" && response.Request.Method == "GET");
        await GotoAsync("/security/cutover");
        IResponse response = await readinessResponse;
        response.Status.ShouldBe(200);
        JsonNode readiness = JsonNode.Parse(await response.TextAsync())!;
        readiness["ready"]!.GetValue<bool>().ShouldBeFalse();
        readiness["blockers"]!.AsArray().ShouldContain(blocker => blocker!["code"]!.GetValue<string>() == "Emergency.IndependentAccessRequired");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Credential cutover readiness", Exact = true }).WaitForAsync();
        await Page.GetByText("Current independent access has not been verified", new() { Exact = true }).WaitForAsync();
        ILocator execute = Page.GetByRole(AriaRole.Button, new() { Name = "Execute credential cutover", Exact = true });
        await Assertions.Expect(execute).ToBeDisabledAsync();
        await Page.GetByRole(AriaRole.Checkbox, new() { Name = "I accept that all existing sessions and credentials will be invalidated, and accept the reviewed external residual windows.", Exact = true }).CheckAsync();
        await Assertions.Expect(execute).ToBeDisabledAsync();
        cutoverRequests.ShouldBe(0);
        await Page.EvaluateAsync("window.scrollTo(0, 0)");
        string directory = Path.Combine(Path.GetTempPath(), "ois-cutover-readiness-e2e");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, "cutover-blocked.png"), FullPage = true, Animations = ScreenshotAnimations.Disabled });
    }
}
