using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>E2E coverage for the ManagementWeb Sessions list.</summary>
public sealed class SessionManagementTests : ManagementWebPageTest
{
    public SessionManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task OperatorCanBrowseSessions()
    {
        await StubAsync();

        await GotoAsync("/sessions");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Sessions", Exact = true }).WaitForAsync();
        await Page.GetByText("192.0.2.14").WaitForAsync();
        await Page.GetByText("Browser").First.WaitForAsync();
        // Status filter from the toolbar.
        await Page.GetByLabel("Status").WaitForAsync();
    }

    private Task StubAsync() =>
        Page.RouteAsync("**/api/admin/**", async route =>
        {
            string path = new Uri(route.Request.Url).AbsolutePath;
            if (path.StartsWith("/api/admin/sessions", StringComparison.OrdinalIgnoreCase) && route.Request.Method == "GET")
            {
                await FulfillJsonAsync(route, Paged(new
                {
                    id = "s1",
                    userId = "u1",
                    ipAddress = "192.0.2.14",
                    userAgent = "Mozilla/5.0 (Macintosh) Chrome/124.0 Safari/537.36",
                    status = "Active",
                    clientCount = 1,
                    lastActivityAt = "2026-06-28T08:00:00Z",
                    expiresAt = "2026-07-05T08:00:00Z",
                    createdAt = "2026-06-28T07:00:00Z",
                }));
                return;
            }
            await NoContentAsync(route);
        });
}
