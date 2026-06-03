using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>
/// E2E coverage for the ManagementWeb Providers slice.
/// API responses are route-mocked so this verifies the frontend contract and routing surface.
/// </summary>
public class ProviderManagementTests : IAsyncLifetime
{
    private static readonly string[] DefaultScopes = ["openid", "profile", "email"];
    private static readonly string[] EmailScopes = ["openid", "email"];
    private static readonly string[] OktaScopes = ["openid", "profile"];
    private readonly ManagementWebAppHostFixture fixture;
    private readonly List<string> adminRequestUrls = [];
    private IBrowserContext? context;
    private IPage? page;
    private string providerStatus = "Active";
    private string providerDisplayName = "Google Login";
    private string providerClientId = "google-client";
    private bool providerDeleted;

    public ProviderManagementTests(ManagementWebAppHostFixture fixture)
    {
        this.fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        context = await fixture.CreateBrowserContextAsync();
        page = await context.NewPageAsync();
        page.SetDefaultTimeout(60_000);
        page.SetDefaultNavigationTimeout(60_000);
        page.Request += (_, request) =>
        {
            if (request.Url.Contains("/api/admin/", StringComparison.OrdinalIgnoreCase))
            {
                adminRequestUrls.Add(request.Url);
            }
        };

        await SetupProvidersApiMocksAsync(page);
    }

    public async ValueTask DisposeAsync()
    {
        if (page is not null) await page.CloseAsync();
        if (context is not null) await context.CloseAsync();
    }

    [Fact]
    public async Task OperatorCanManageIdentityProviders()
    {
        string baseUrl = fixture.ManagementWebUrl ?? throw new InvalidOperationException("ManagementWeb URL was not initialized.");

        await page!.GotoAsync(new Uri(new Uri(baseUrl), "/providers").ToString());
        await page.GetByRole(AriaRole.Heading, new() { Name = "Identity Providers", Exact = true }).WaitForAsync();
        await page.GetByText("Google Login").WaitForAsync();

        await page.GetByLabel(new Regex("Search providers", RegexOptions.IgnoreCase)).FillAsync("contoso");
        await page.GetByText("Contoso IdP").WaitForAsync();

        await page.GetByLabel(new Regex("Search providers", RegexOptions.IgnoreCase)).FillAsync("");
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("new provider", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex("create provider", RegexOptions.IgnoreCase) }).WaitForAsync();
        await page.GetByLabel(new Regex("^Provider name", RegexOptions.IgnoreCase)).FillAsync("okta");
        await page.GetByLabel(new Regex("Display name", RegexOptions.IgnoreCase)).FillAsync("Okta Login");
        await page.GetByLabel(new Regex("Authority", RegexOptions.IgnoreCase)).FillAsync("https://okta.example.com");
        await page.GetByLabel(new Regex("Client ID", RegexOptions.IgnoreCase)).FillAsync("okta-client");
        await page.GetByLabel(new Regex("Client secret", RegexOptions.IgnoreCase)).FillAsync("okta-secret");
        await page.GetByLabel(new Regex("Scopes", RegexOptions.IgnoreCase)).FillAsync("openid profile");
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("create provider", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Okta Login", Exact = true }).WaitForAsync();

        await page.GotoAsync(new Uri(new Uri(baseUrl), "/providers/provider-1").ToString());
        await page.GetByRole(AriaRole.Heading, new() { Name = providerDisplayName, Exact = true }).WaitForAsync();
        await page.GetByText("Client ID: google-client").WaitForAsync();

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("disable provider", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("enable provider", RegexOptions.IgnoreCase) }).WaitForAsync();
        providerStatus.ShouldBe("Disabled");

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("enable provider", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("disable provider", RegexOptions.IgnoreCase) }).WaitForAsync();
        providerStatus.ShouldBe("Active");

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("edit provider", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex("edit provider", RegexOptions.IgnoreCase) }).WaitForAsync();
        await page.GetByLabel(new Regex("Display name", RegexOptions.IgnoreCase)).FillAsync("Google Workspace");
        await page.GetByLabel(new Regex("Client ID", RegexOptions.IgnoreCase)).FillAsync("workspace-client");
        await page.GetByLabel(new Regex("Scopes", RegexOptions.IgnoreCase)).FillAsync("openid email");
        await page.GetByRole(AriaRole.Checkbox, new() { NameRegex = new Regex("JIT provisioning", RegexOptions.IgnoreCase) }).UncheckAsync();
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("save provider", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Google Workspace", Exact = true }).WaitForAsync();
        providerDisplayName.ShouldBe("Google Workspace");
        providerClientId.ShouldBe("workspace-client");

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("delete provider", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog, new() { NameRegex = new Regex("delete provider", RegexOptions.IgnoreCase) })
            .GetByRole(AriaRole.Button, new() { NameRegex = new Regex("delete google", RegexOptions.IgnoreCase) })
            .ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Identity Providers", Exact = true }).WaitForAsync();
        providerDeleted.ShouldBeTrue();

        adminRequestUrls.Any((url) => url.Contains("/api/admin/providers", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/service-accounts", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/clients", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
    }

    private async Task SetupProvidersApiMocksAsync(IPage testPage)
    {
        await testPage.RouteAsync("**/api/admin/**", async route =>
        {
            Uri uri = new(route.Request.Url);
            string path = uri.AbsolutePath;
            string method = route.Request.Method;

            if (method == "GET" && path.Equals("/api/admin/providers", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, new[] { Provider(), ContosoProvider() });
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/providers/provider-1", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, Provider());
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/providers/provider-3", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, OktaProvider());
                return;
            }

            if (method == "POST" && path.Equals("/api/admin/providers", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, new
                {
                    id = "provider-3",
                    name = "okta",
                    displayName = "Okta Login",
                    authority = "https://okta.example.com",
                    clientId = "okta-client",
                    scopes = OktaScopes,
                    jitProvisioningEnabled = true,
                    status = "Active",
                    createdAt = "2026-01-03T00:00:00Z"
                }, 201);
                return;
            }

            if (method == "PATCH" && path.Equals("/api/admin/providers/provider-1", StringComparison.OrdinalIgnoreCase))
            {
                providerDisplayName = "Google Workspace";
                providerClientId = "workspace-client";
                await FulfillJsonAsync(route, Provider());
                return;
            }

            if (method == "POST" && path.Equals("/api/admin/providers/provider-1/disable", StringComparison.OrdinalIgnoreCase))
            {
                providerStatus = "Disabled";
                await FulfillJsonAsync(route, Provider());
                return;
            }

            if (method == "POST" && path.Equals("/api/admin/providers/provider-1/enable", StringComparison.OrdinalIgnoreCase))
            {
                providerStatus = "Active";
                await FulfillJsonAsync(route, Provider());
                return;
            }

            if (method == "DELETE" && path.Equals("/api/admin/providers/provider-1", StringComparison.OrdinalIgnoreCase))
            {
                providerDeleted = true;
                await route.FulfillAsync(new() { Status = 204 });
                return;
            }

            await route.FulfillAsync(new() { Status = 404, Body = $"Unexpected E2E route: {method} {path}" });
        });
    }

    private object Provider()
    {
        return new
        {
            id = "provider-1",
            name = "google",
            displayName = providerDisplayName,
            authority = "https://accounts.google.com",
            clientId = providerClientId,
            scopes = DefaultScopes,
            jitProvisioningEnabled = true,
            status = providerStatus,
            createdAt = "2026-01-01T00:00:00Z"
        };
    }

    private static object ContosoProvider()
    {
        return new
        {
            id = "provider-2",
            name = "contoso",
            displayName = "Contoso IdP",
            authority = "https://login.contoso.test",
            clientId = "contoso-client",
            scopes = EmailScopes,
            jitProvisioningEnabled = false,
            status = "Disabled",
            createdAt = "2026-01-02T00:00:00Z"
        };
    }

    private static object OktaProvider()
    {
        return new
        {
            id = "provider-3",
            name = "okta",
            displayName = "Okta Login",
            authority = "https://okta.example.com",
            clientId = "okta-client",
            scopes = OktaScopes,
            jitProvisioningEnabled = true,
            status = "Active",
            createdAt = "2026-01-03T00:00:00Z"
        };
    }

    private static Task FulfillJsonAsync(IRoute route, object body, int statusCode = 200)
    {
        return route.FulfillAsync(new()
        {
            Status = statusCode,
            ContentType = "application/json",
            Body = System.Text.Json.JsonSerializer.Serialize(body)
        });
    }
}
