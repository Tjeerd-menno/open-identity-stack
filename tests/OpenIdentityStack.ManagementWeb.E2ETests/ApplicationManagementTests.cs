using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>
/// E2E coverage for the ManagementWeb Applications slice.
/// API responses are route-mocked so this verifies the frontend contract and routing surface.
/// </summary>
public class ApplicationManagementTests : IAsyncLifetime
{
    private readonly ManagementWebAppHostFixture fixture;
    private readonly List<string> adminRequestUrls = [];
    private IBrowserContext? context;
    private IPage? page;
    private string applicationId = "app-test-1";
    private string clientId = "orders-web";
    private string displayName = "Orders Web";
    private string profile = "Web";
    private string clientType = "Confidential";
    private string status = "Active";
    private bool credentialRevoked;

    public ApplicationManagementTests(ManagementWebAppHostFixture fixture)
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

        await SetupApplicationsApiMocksAsync(page);
    }

    public async ValueTask DisposeAsync()
    {
        if (page is not null) await page.CloseAsync();
        if (context is not null) await context.CloseAsync();
    }

    [Fact]
    public async Task ApplicationsWorkflow_ShouldUseConsolidatedApplicationsApi()
    {
        string baseUrl = fixture.ManagementWebUrl ?? throw new InvalidOperationException("ManagementWeb URL was not initialized.");

        await page!.GotoAsync(new Uri(new Uri(baseUrl), "/applications").ToString());
        await page.GetByRole(AriaRole.Heading, new() { Name = "Applications", Exact = true }).WaitForAsync();

        (await page.GetByRole(AriaRole.Link, new() { NameRegex = new Regex("service accounts", RegexOptions.IgnoreCase) }).CountAsync())
            .ShouldBe(0);
        (await page.GetByRole(AriaRole.Link, new() { NameRegex = new Regex("clients", RegexOptions.IgnoreCase) }).CountAsync())
            .ShouldBe(0);

        await page.GetByPlaceholder("Search by client ID or display name...").FillAsync("orders");
        await page.GetByLabel("Profile").SelectOptionAsync(["Web"]);
        await page.GetByLabel("Status").SelectOptionAsync(["Active"]);
        await page.GetByLabel("Client type").SelectOptionAsync(["Confidential"]);
        await page.GetByRole(AriaRole.Cell, new() { Name = "orders-web", Exact = true }).WaitForAsync();

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("new application", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex("create application", RegexOptions.IgnoreCase) }).WaitForAsync();
        await page.GetByLabel("Application profile").SelectOptionAsync(["MachineToMachine"]);
        await page.GetByText(new Regex("machine-to-machine applications use only the client credentials grant", RegexOptions.IgnoreCase))
            .WaitForAsync();
        await page.GetByLabel("Client ID").FillAsync("orders-worker");
        await page.GetByLabel("Display name").FillAsync("Orders Worker");
        await page.GetByRole(AriaRole.Checkbox, new() { Name = "api", Exact = true }).CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("create application", RegexOptions.IgnoreCase) }).ClickAsync();

        await page.GetByRole(AriaRole.Heading, new() { Name = "Orders Worker", Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("add secret", RegexOptions.IgnoreCase) }).ClickAsync();
        ILocator addSecretDialog = page.GetByRole(AriaRole.Dialog, new() { NameRegex = new Regex("add secret", RegexOptions.IgnoreCase) });
        await addSecretDialog.GetByLabel("Description").FillAsync("Rotated secret");
        await addSecretDialog.GetByLabel("Revoke existing secrets").CheckAsync();
        await addSecretDialog.GetByRole(AriaRole.Button, new() { Name = "Add secret", Exact = true }).ClickAsync();
        await addSecretDialog.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("show client secret", RegexOptions.IgnoreCase) }).ClickAsync();
        await addSecretDialog.GetByText("new-secret").WaitForAsync();
        await addSecretDialog.GetByRole(AriaRole.Button, new() { Name = "Done", Exact = true }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("revoke primary secret", RegexOptions.IgnoreCase) }).ClickAsync();
        credentialRevoked.ShouldBeTrue();

        await page.GetByRole(AriaRole.Button, new() { Name = "Disable", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Enable", Exact = true }).WaitForAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Delete application", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/applications/?$"));

        adminRequestUrls.Any((url) => url.Contains("/api/admin/applications", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/service-accounts", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/clients", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
    }

    private async Task SetupApplicationsApiMocksAsync(IPage testPage)
    {
        await testPage.RouteAsync("**/api/admin/**", async route =>
        {
            Uri uri = new(route.Request.Url);
            string path = uri.AbsolutePath;
            string method = route.Request.Method;

            if (method == "GET" && path.Equals("/api/admin/applications/policies/profiles", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, ProfilePolicies());
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/applications", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, ApplicationsList());
                return;
            }

            if (method == "POST" && path.Equals("/api/admin/applications", StringComparison.OrdinalIgnoreCase))
            {
                applicationId = "app-created-1";
                clientId = "orders-worker";
                displayName = "Orders Worker";
                profile = "MachineToMachine";
                clientType = "Confidential";
                status = "Active";

                await FulfillJsonAsync(route, new
                {
                    id = applicationId,
                    clientId,
                    displayName,
                    profile,
                    clientType,
                    status,
                    initialSecret = "initial-secret",
                    createdAt = "2026-01-01T00:00:00Z"
                }, 201);
                return;
            }

            if (method == "GET" && path.Equals($"/api/admin/applications/{applicationId}", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, ApplicationDetail());
                return;
            }

            if (method == "GET" && path.Equals($"/api/admin/applications/{applicationId}/credentials", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, Credentials());
                return;
            }

            if (method == "POST" && path.Equals($"/api/admin/applications/{applicationId}/credentials/client-secrets", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, new { credentialId = "cred-test-2", clientSecret = "new-secret" }, 201);
                return;
            }

            if (method == "DELETE" && path.Equals($"/api/admin/applications/{applicationId}/credentials/cred-test-1", StringComparison.OrdinalIgnoreCase))
            {
                credentialRevoked = true;
                await route.FulfillAsync(new() { Status = 204 });
                return;
            }

            if (method == "POST" && path.Equals($"/api/admin/applications/{applicationId}/disable", StringComparison.OrdinalIgnoreCase))
            {
                status = "Disabled";
                await FulfillJsonAsync(route, ApplicationDetail());
                return;
            }

            if (method == "DELETE" && path.Equals($"/api/admin/applications/{applicationId}", StringComparison.OrdinalIgnoreCase))
            {
                await route.FulfillAsync(new() { Status = 204 });
                return;
            }

            await route.FulfillAsync(new() { Status = 404, Body = $"Unexpected E2E route: {method} {path}" });
        });
    }

    private object ApplicationsList()
    {
        return new
        {
            items = new[]
            {
                new
                {
                    id = applicationId,
                    clientId,
                    displayName,
                    profile,
                    clientType,
                    status,
                    allowedGrantTypes = new[] { "authorization_code", "refresh_token" },
                    credentialCount = 1,
                    createdAt = "2026-01-01T00:00:00Z",
                    modifiedAt = (string?)null
                }
            },
            totalCount = 1,
            page = 1,
            pageSize = 20
        };
    }

    private object ApplicationDetail()
    {
        return new
        {
            id = applicationId,
            clientId,
            displayName,
            description = "Orders application",
            profile,
            clientType,
            status,
            redirectUris = profile == "MachineToMachine" ? [] : new[] { "https://orders.example.com/callback" },
            postLogoutRedirectUris = profile == "MachineToMachine" ? [] : new[] { "https://orders.example.com/" },
            allowedScopes = new[] { "openid", "profile", "api" },
            allowedGrantTypes = profile == "MachineToMachine" ? new[] { "client_credentials" } : new[] { "authorization_code", "refresh_token" },
            requirePkce = profile != "MachineToMachine",
            requireConsent = false,
            credentialCount = 1,
            certificateCount = 0,
            requiresMigrationReview = false,
            migrationSource = (string?)null,
            createdAt = "2026-01-01T00:00:00Z",
            modifiedAt = (string?)null
        };
    }

    private object[] Credentials()
    {
        return
        [
            new
            {
                id = "cred-test-1",
                applicationId,
                type = "ClientSecret",
                thumbprint = (string?)null,
                subject = (string?)null,
                description = "Primary secret",
                expiresAt = (string?)null,
                createdAt = "2026-01-01T00:00:00Z",
                lastUsedAt = (string?)null,
                revokedAt = credentialRevoked ? "2026-01-02T00:00:00Z" : null
            }
        ];
    }

    private static object[] ProfilePolicies()
    {
        return
        [
            new
            {
                applicationProfile = "Web",
                isSelectable = true,
                unavailabilityReason = (string?)null,
                defaultClientProfile = "Confidential",
                allowedClientProfiles = new[] { "Confidential" },
                allowedGrantTypes = new[] { "authorization_code", "refresh_token" },
                defaultGrantTypes = new[] { "authorization_code", "refresh_token" },
                options = new Dictionary<string, string>
                {
                    ["redirectUris"] = "Available",
                    ["postLogoutRedirectUris"] = "Available",
                    ["allowedScopes"] = "Available",
                    ["requirePkce"] = "ReadOnly",
                    ["requireConsent"] = "Available"
                },
                requirePkce = true,
                defaultRequirePkce = true,
                defaultRequireConsent = false,
                requiresRedirectUris = true
            },
            new
            {
                applicationProfile = "MachineToMachine",
                isSelectable = true,
                unavailabilityReason = (string?)null,
                defaultClientProfile = "Confidential",
                allowedClientProfiles = new[] { "Confidential" },
                allowedGrantTypes = new[] { "client_credentials" },
                defaultGrantTypes = new[] { "client_credentials" },
                options = new Dictionary<string, string>
                {
                    ["redirectUris"] = "Hidden",
                    ["postLogoutRedirectUris"] = "Hidden",
                    ["allowedScopes"] = "Available",
                    ["requirePkce"] = "Hidden",
                    ["requireConsent"] = "Hidden"
                },
                requirePkce = false,
                defaultRequirePkce = false,
                defaultRequireConsent = false,
                requiresRedirectUris = false
            }
        ];
    }

    private static Task FulfillJsonAsync(IRoute route, object body, int statusCode = 200)
    {
        return route.FulfillAsync(new()
        {
            Status = statusCode,
            ContentType = "application/json",
            Body = JsonSerializer.Serialize(body)
        });
    }
}
