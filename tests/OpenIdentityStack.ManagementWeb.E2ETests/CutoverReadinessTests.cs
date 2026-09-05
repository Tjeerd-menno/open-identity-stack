using System.Text.Json.Nodes;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

public sealed class CutoverReadinessTests(ManagementWebAppHostFixture fixture) : ManagementWebPageTest(fixture)
{
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
