using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>E2E coverage for the ManagementWeb Identity providers list and detail.</summary>
public sealed class ProviderManagementTests : ManagementWebPageTest
{
    private const string ProviderId = "provider-test-1";

    public ProviderManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task OperatorCanBrowseProvidersAndOpenDetail()
    {
        await StubAsync();

        await GotoAsync("/providers");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Identity providers", Exact = true }).WaitForAsync();
        await Page.GetByText("Google Workspace").WaitForAsync();

        await Page.Locator("tbody tr").First.ClickAsync();
        await Page.WaitForURLAsync(new Regex($"/providers/{ProviderId}$"));
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Google Workspace", Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Connection", Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Settings", Exact = true }).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanAddAProvider()
    {
        await StubAsync();

        await GotoAsync("/providers");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add provider", Exact = true }).ClickAsync();

        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        // "Name" (required) renders a "*"; target it by placeholder instead.
        await dialog.GetByPlaceholder("google-workspace").FillAsync("google-workspace");
        await dialog.GetByLabel("Display name").FillAsync("Google Workspace");
        await dialog.GetByLabel(new Regex("Issuer", RegexOptions.IgnoreCase)).FillAsync("https://accounts.google.com");
        await dialog.GetByLabel("Client ID").FillAsync("northwind.apps.googleusercontent.com");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Add provider", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Added Google Workspace", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanDisableAProviderFromTheRowMenu()
    {
        await StubAsync();

        await GotoAsync("/providers");
        await Page.GetByText("Google Workspace").WaitForAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Row actions" }).First.ClickAsync();
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Disable provider", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Provider updated", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanDeleteAProviderFromTheDetailPage()
    {
        await StubAsync();

        await GotoAsync($"/providers/{ProviderId}");
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Settings", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Delete provider", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Provider deleted", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    private Task StubAsync() =>
        Page.RouteAsync("**/api/admin/**", async route =>
        {
            string path = new Uri(route.Request.Url).AbsolutePath;
            string method = route.Request.Method;

            if (Regex.IsMatch(path, @"/api/admin/providers/[^/]+/(enable|disable)$", RegexOptions.IgnoreCase) && method == "POST")
            {
                await FulfillJsonAsync(route, MockProvider());
                return;
            }
            if (path == "/api/admin/providers" && method == "POST")
            {
                await FulfillJsonAsync(route, MockProvider());
                return;
            }
            if (Regex.IsMatch(path, @"/api/admin/providers/[^/]+$", RegexOptions.IgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, MockProvider());
                return;
            }
            if (path.StartsWith("/api/admin/providers", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, new[] { MockProvider() });
                return;
            }
            await NoContentAsync(route);
        });

    private static object MockProvider() => new
    {
        id = ProviderId,
        name = "google-workspace",
        displayName = "Google Workspace",
        authority = "https://accounts.google.com",
        clientId = "northwind.apps.googleusercontent.com",
        scopes = new[] { "openid", "email", "profile" },
        jitProvisioningEnabled = true,
        status = "Active",
        createdAt = "2026-06-01T00:00:00Z",
    };
}
