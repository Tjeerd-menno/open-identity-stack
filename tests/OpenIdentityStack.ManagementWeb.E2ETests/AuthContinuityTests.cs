using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>
/// Verifies the ManagementWeb shell stays reachable across reloads (authenticated
/// via the mock auth provider).
/// </summary>
public sealed class AuthContinuityTests : ManagementWebPageTest
{
    public AuthContinuityTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task ManagementWebShellRemainsReachableAcrossReloads()
    {
        await StubEmptyAdminApiAsync();

        await GotoAsync("/");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Overview", Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Navigation, new() { Name = "Management navigation", Exact = true }).WaitForAsync();

        await Page.GotoAsync("about:blank");

        await GotoAsync("/");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Overview", Exact = true }).WaitForAsync();
        await Page.GetByText("OpenIdentity").First.WaitForAsync();
    }

    private Task StubEmptyAdminApiAsync() =>
        Page.RouteAsync("**/api/admin/**", async route =>
        {
            string path = new Uri(route.Request.Url).AbsolutePath;
            if (route.Request.Method != "GET")
            {
                await NoContentAsync(route);
                return;
            }
            if (path.Contains("/providers", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, Array.Empty<object>());
                return;
            }
            await FulfillJsonAsync(route, Paged());
        });
}
