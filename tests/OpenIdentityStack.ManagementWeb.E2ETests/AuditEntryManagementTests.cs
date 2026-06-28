using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>E2E coverage for the ManagementWeb Audit list.</summary>
public sealed class AuditEntryManagementTests : ManagementWebPageTest
{
    public AuditEntryManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task OperatorCanBrowseAuditEntries()
    {
        await StubAsync();

        await GotoAsync("/audit-entries");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Audit", Exact = true }).WaitForAsync();
        await Page.GetByText("user.signin").WaitForAsync();
        await Page.GetByText("margaret.hamilton").WaitForAsync();
        await Page.GetByLabel("Entity").WaitForAsync();
    }

    private Task StubAsync() =>
        Page.RouteAsync("**/api/admin/**", async route =>
        {
            string path = new Uri(route.Request.Url).AbsolutePath;
            if (path.StartsWith("/api/admin/audit-entries", StringComparison.OrdinalIgnoreCase) && route.Request.Method == "GET")
            {
                await FulfillJsonAsync(route, Paged(new
                {
                    id = "audit-1",
                    timestamp = "2026-06-28T14:32:00Z",
                    userId = "margaret.hamilton",
                    action = "user.signin",
                    entityType = "User",
                    entityId = "usr_8fa21c",
                    details = (string?)null,
                    beforeState = (string?)null,
                    afterState = (string?)null,
                }));
                return;
            }
            await NoContentAsync(route);
        });
}
