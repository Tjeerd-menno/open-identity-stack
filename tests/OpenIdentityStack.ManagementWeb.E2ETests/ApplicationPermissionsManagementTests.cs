using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>E2E coverage for the ManagementWeb Permissions (registered applications) list and detail.</summary>
public sealed class ApplicationPermissionsManagementTests : ManagementWebPageTest
{
    private const string RegistrationId = "reg-test-1";

    public ApplicationPermissionsManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task OperatorCanBrowseRegisteredApplicationsAndOpenDetail()
    {
        await StubAsync();

        await GotoAsync("/application-permissions");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Permissions", Exact = true }).WaitForAsync();
        await Page.GetByText("Orders API").WaitForAsync();

        await Page.Locator("tbody tr").First.ClickAsync();
        await Page.WaitForURLAsync(new Regex($"/application-permissions/{RegistrationId}$"));
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Orders API", Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Permissions", RegexOptions.IgnoreCase) }).WaitForAsync();
        await Page.GetByText("orders:read").WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanRegisterAManifestManually()
    {
        await StubAsync();

        await GotoAsync("/application-permissions");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register application", Exact = true }).ClickAsync();

        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByText("Register manually").ClickAsync();
        await dialog.GetByLabel("Application ID").FillAsync("orders-api");
        await dialog.GetByLabel("Display name").FillAsync("Orders API");
        await dialog.GetByLabel("Owner ID").FillAsync("platform-engineering");
        await dialog.GetByLabel(new Regex("Permission key 1", RegexOptions.IgnoreCase)).FillAsync("orders:read");
        await dialog.GetByLabel(new Regex("Permission name 1", RegexOptions.IgnoreCase)).FillAsync("Read orders");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Register application", Exact = true }).ClickAsync();

        // Registration navigates to the new application's permission detail page.
        await Page.WaitForURLAsync(new Regex($"/application-permissions/{RegistrationId}$"));
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Orders API", Exact = true }).WaitForAsync();
    }

    private Task StubAsync() =>
        Page.RouteAsync("**/api/admin/**", async route =>
        {
            string path = new Uri(route.Request.Url).AbsolutePath;
            string method = route.Request.Method;

            if (Regex.IsMatch(path, @"/application-permissions/applications/[^/]+$", RegexOptions.IgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, MockApp(detail: true));
                return;
            }
            if (path.EndsWith("/application-permissions/applications", StringComparison.OrdinalIgnoreCase) && method == "POST")
            {
                await FulfillJsonAsync(route, new { id = RegistrationId });
                return;
            }
            if (path.Contains("/application-permissions/applications", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, Paged(MockApp(detail: false)));
                return;
            }
            await NoContentAsync(route);
        });

    private static object MockApp(bool detail) => detail
        ? new
        {
            id = RegistrationId,
            applicationIdentifier = "orders-api",
            displayName = "Orders API",
            ownerId = "platform-engineering",
            ownerType = "Group",
            permissionCount = 1,
            status = "Active",
            manifestVersion = "1.0.0",
            updatedAt = "2026-06-01T00:00:00Z",
            description = "Order management API",
            schemaVersion = "1.0.0",
            manifestBaseUrl = (string?)null,
            concurrencyToken = 1,
            permissions = new[]
            {
                new { id = "p1", fullPermissionKey = "orders:read", displayName = "Read orders", description = "View orders", category = "Orders", status = "Active" },
            },
            maintainers = Array.Empty<object>(),
        }
        : new
        {
            id = RegistrationId,
            applicationIdentifier = "orders-api",
            displayName = "Orders API",
            ownerId = "platform-engineering",
            ownerType = "Group",
            permissionCount = 1,
            status = "Active",
            manifestVersion = "1.0.0",
            updatedAt = "2026-06-01T00:00:00Z",
        };
}
