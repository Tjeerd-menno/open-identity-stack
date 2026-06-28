using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>E2E coverage for the ManagementWeb Applications list and detail.</summary>
public sealed class ApplicationManagementTests : ManagementWebPageTest
{
    private const string AppId = "app-test-1";

    public ApplicationManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task OperatorCanBrowseApplicationsAndOpenDetail()
    {
        await StubAsync();

        await GotoAsync("/applications");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Applications", Exact = true }).WaitForAsync();
        await Page.GetByText("Northwind Web").WaitForAsync();
        await Page.GetByText("northwind-web").First.WaitForAsync();

        await Page.GetByText("Northwind Web").ClickAsync();
        await Page.WaitForURLAsync(new Regex($"/applications/{AppId}$"));
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Northwind Web", Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Configuration", Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Credentials", RegexOptions.IgnoreCase) }).WaitForAsync();
    }

    private Task StubAsync() =>
        Page.RouteAsync("**/api/admin/**", async route =>
        {
            string path = new Uri(route.Request.Url).AbsolutePath;
            string method = route.Request.Method;

            if (Regex.IsMatch(path, @"/api/admin/applications/[^/]+/credentials$", RegexOptions.IgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, Array.Empty<object>());
                return;
            }
            if (Regex.IsMatch(path, @"/api/admin/applications/[^/]+$", RegexOptions.IgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, MockApp(detail: true));
                return;
            }
            if (path.StartsWith("/api/admin/applications", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, Paged(MockApp(detail: false)));
                return;
            }
            await NoContentAsync(route);
        });

    private static object MockApp(bool detail) => detail
        ? new
        {
            id = AppId,
            clientId = "northwind-web",
            displayName = "Northwind Web",
            description = (string?)null,
            profile = "Web",
            clientType = "Confidential",
            status = "Active",
            redirectUris = new[] { "https://app.northwind.io/callback" },
            postLogoutRedirectUris = Array.Empty<string>(),
            allowedScopes = new[] { "openid", "profile" },
            allowedGrantTypes = new[] { "authorization_code" },
            requirePkce = true,
            requireConsent = false,
            credentialCount = 0,
            certificateCount = 0,
            requiresMigrationReview = false,
            migrationSource = (string?)null,
            createdAt = "2026-06-01T00:00:00Z",
            modifiedAt = (string?)null,
        }
        : new
        {
            id = AppId,
            clientId = "northwind-web",
            displayName = "Northwind Web",
            profile = "Web",
            clientType = "Confidential",
            status = "Active",
            allowedGrantTypes = new[] { "authorization_code" },
            credentialCount = 0,
            createdAt = "2026-06-01T00:00:00Z",
            modifiedAt = (string?)null,
        };
}
