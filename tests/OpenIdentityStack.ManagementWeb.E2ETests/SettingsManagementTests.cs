using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>E2E coverage for the ManagementWeb Authentication settings screen.</summary>
public sealed class SettingsManagementTests : ManagementWebPageTest
{
    public SettingsManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task OperatorCanViewAuthenticationSettings()
    {
        await StubAsync();

        await GotoAsync("/providers/settings");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Authentication settings", Exact = true }).WaitForAsync();
        await Page.GetByText("Default sign-in", new() { Exact = true }).WaitForAsync();
        await Page.GetByLabel(new Regex("Local password fallback", RegexOptions.IgnoreCase)).WaitForAsync();
        await Page.GetByText("Current configuration", new() { Exact = true }).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanToggleLocalFallback()
    {
        await StubAsync();

        await GotoAsync("/providers/settings");
        await Page.GetByText("Default sign-in", new() { Exact = true }).WaitForAsync();
        // The Mantine Switch input is visually hidden; force the click.
        await Page.GetByLabel(new Regex("Local password fallback", RegexOptions.IgnoreCase))
            .ClickAsync(new LocatorClickOptions { Force = true });

        await Page.GetByText(new Regex("Local fallback updated", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    private Task StubAsync() =>
        Page.RouteAsync("**/api/admin/**", async route =>
        {
            string path = new Uri(route.Request.Url).AbsolutePath;
            string method = route.Request.Method;

            if (path.StartsWith("/api/admin/authentication-settings/providers", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, new[]
                {
                    new { id = "local", name = "local", displayName = "Local Accounts", type = "local", isActive = true },
                });
                return;
            }
            if (path.StartsWith("/api/admin/authentication-settings", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, new
                {
                    defaultProviderId = "local",
                    isLocalDefault = true,
                    localFallbackEnabled = true,
                    updatedAt = "2026-06-01T00:00:00Z",
                });
                return;
            }
            await NoContentAsync(route);
        });
}
