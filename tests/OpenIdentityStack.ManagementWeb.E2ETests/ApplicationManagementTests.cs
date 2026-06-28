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

    [Fact]
    public async Task OperatorCanRegisterAnApplicationAndSeeTheSecret()
    {
        await StubAsync();

        await GotoAsync("/applications");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create application", Exact = true }).ClickAsync();

        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByLabel("Display name").FillAsync("Northwind Web");
        await dialog.GetByLabel("Client ID").FillAsync("northwind-web");
        await dialog.GetByPlaceholder("https://app.example.com/callback").First.FillAsync("https://app.northwind.io/callback");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create application", Exact = true }).ClickAsync();

        // Backend returns an initial secret -> one-time reveal.
        await Page.GetByText(new Regex("Application created", RegexOptions.IgnoreCase)).WaitForAsync();
        await Page.GetByText("s3cr3t-Northwind-Demo").WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanAddAClientSecret()
    {
        await StubAsync();

        await GotoAsync($"/applications/{AppId}");
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Credentials", RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add secret", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Generate secret", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Client secret created", RegexOptions.IgnoreCase)).WaitForAsync();
        await Page.GetByText("s3cr3t-Northwind-Demo").WaitForAsync();
    }

    private Task StubAsync() =>
        Page.RouteAsync("**/api/admin/**", async route =>
        {
            string path = new Uri(route.Request.Url).AbsolutePath;
            string method = route.Request.Method;

            if (path.Contains("/applications/policies/profiles", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, new[] { MockWebProfilePolicy() });
                return;
            }
            if (Regex.IsMatch(path, @"/api/admin/applications/[^/]+/credentials/client-secrets$", RegexOptions.IgnoreCase) && method == "POST")
            {
                await FulfillJsonAsync(route, new { credentialId = "cred-new-1", clientSecret = "s3cr3t-Northwind-Demo" });
                return;
            }
            if (Regex.IsMatch(path, @"/api/admin/applications/[^/]+/credentials$", RegexOptions.IgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, Array.Empty<object>());
                return;
            }
            if (path == "/api/admin/applications" && method == "POST")
            {
                await FulfillJsonAsync(route, new
                {
                    id = AppId,
                    clientId = "northwind-web",
                    displayName = "Northwind Web",
                    profile = "Web",
                    clientType = "Confidential",
                    status = "Active",
                    initialSecret = "s3cr3t-Northwind-Demo",
                    createdAt = "2026-06-28T00:00:00Z",
                });
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

    private static object MockWebProfilePolicy() => new
    {
        applicationProfile = "Web",
        isSelectable = true,
        unavailabilityReason = (string?)null,
        defaultClientProfile = "Confidential",
        allowedClientProfiles = new[] { "Confidential" },
        allowedGrantTypes = new[] { "authorization_code", "refresh_token" },
        defaultGrantTypes = new[] { "authorization_code" },
        options = new { },
        requirePkce = true,
        defaultRequirePkce = true,
        defaultRequireConsent = false,
        requiresRedirectUris = true,
    };

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
