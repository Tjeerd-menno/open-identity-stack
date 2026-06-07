using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>
/// E2E coverage for the ManagementWeb Application Permissions slice.
/// API responses are route-mocked so this verifies the frontend contract and routing surface.
/// </summary>
public class ApplicationPermissionsManagementTests : IAsyncLifetime
{
    private readonly ManagementWebAppHostFixture fixture;
    private readonly List<string> adminRequestUrls = [];
    private IBrowserContext? context;
    private IPage? page;
    private string status = "Active";
    private string ownerId = "owner-1";
    private string ownerType = "User";

    public ApplicationPermissionsManagementTests(ManagementWebAppHostFixture fixture)
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

        await SetupApplicationPermissionsApiMocksAsync(page);
    }

    public async ValueTask DisposeAsync()
    {
        if (page is not null) await page.CloseAsync();
        if (context is not null) await context.CloseAsync();
    }

    [Fact]
    public async Task OperatorCanManageApplicationPermissionRegistry()
    {
        string baseUrl = fixture.ManagementWebUrl ?? throw new InvalidOperationException("ManagementWeb URL was not initialized.");

        await page!.GotoAsync(new Uri(new Uri(baseUrl), "/application-permissions").ToString());
        await page.GetByRole(AriaRole.Heading, new() { Name = "Permissions", Exact = true }).WaitForAsync();
        await page.GetByText("Patient API").WaitForAsync();

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Add Application", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.WaitForURLAsync("**/application-permissions/new");
        await page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex("Add Application", RegexOptions.IgnoreCase) }).WaitForAsync();

        await page.GetByLabel(new Regex("Well-known permissions endpoint", RegexOptions.IgnoreCase)).FillAsync("https://patient.example/.well-known/permissions");
        Task<IRequest> importRequest = page.WaitForRequestAsync(new Regex(@"/api/admin/application-permissions/applications/import$", RegexOptions.IgnoreCase));
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Import Endpoint", RegexOptions.IgnoreCase) }).ClickAsync();
        await importRequest;

        await page.GotoAsync(new Uri(new Uri(baseUrl), "/application-permissions/application-1").ToString());
        await page.GetByRole(AriaRole.Heading, new() { Name = "Patient API", Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Ownership", Exact = true }).WaitForAsync();

        await page.GetByRole(AriaRole.Tab, new() { Name = "Maintainers", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Maintainers", Exact = true }).WaitForAsync();

        await page.GetByRole(AriaRole.Tab, new() { Name = "Permissions", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Catalog", Exact = true }).WaitForAsync();

        await page.GetByRole(AriaRole.Tab, new() { Name = "History", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "History", Exact = true }).WaitForAsync();

        await page.GetByRole(AriaRole.Tab, new() { Name = "Diagnostics", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Diagnostics", Exact = true }).WaitForAsync();

        await page.GetByRole(AriaRole.Tab, new() { Name = "Overview", Exact = true }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true }).ClickAsync();

        await page.GetByRole(AriaRole.Tab, new() { Name = "Permissions", Exact = true }).ClickAsync();
        Task<IRequest> lifecycleRequest = page.WaitForRequestAsync(new Regex(@"/api/admin/application-permissions/applications/application-1/lifecycle$", RegexOptions.IgnoreCase));
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Disable", RegexOptions.IgnoreCase) }).ClickAsync();
        await lifecycleRequest;
        status.ShouldBe("Disabled");

        await page.GetByRole(AriaRole.Tab, new() { Name = "Overview", Exact = true }).ClickAsync();
        await page.GetByLabel(new Regex("^New owner$", RegexOptions.IgnoreCase)).FillAsync("operations");
        await page.GetByRole(AriaRole.Option, new() { Name = "Operations Group", Exact = true }).ClickAsync();
        Task<IRequest> ownershipRequest = page.WaitForRequestAsync(new Regex(@"/api/admin/application-permissions/applications/application-1/ownership$", RegexOptions.IgnoreCase));
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Transfer Ownership", RegexOptions.IgnoreCase) }).ClickAsync();
        await ownershipRequest;
        ownerId.ShouldBe("group-1");
        ownerType.ShouldBe("Group");

        adminRequestUrls.Any((url) => url.Contains("/api/admin/application-permissions", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/service-accounts", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/clients", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
    }

    private async Task SetupApplicationPermissionsApiMocksAsync(IPage testPage)
    {
        await testPage.RouteAsync("**/api/admin/**", async route =>
        {
            Uri uri = new(route.Request.Url);
            string path = uri.AbsolutePath;
            string method = route.Request.Method;

            if (method == "GET" && path.Equals("/api/admin/application-permissions/applications", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, new
                {
                    items = new[] { ApplicationListItem() },
                    totalCount = 1,
                    page = 1,
                    pageSize = 20,
                    totalPages = 1
                });
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/application-permissions/applications/application-1", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, ApplicationDetail());
                return;
            }

            if (method == "POST" && path.Equals("/api/admin/application-permissions/applications/import", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, new { applicationId = "application-1" });
                return;
            }

            if (method == "POST" && path.Equals("/api/admin/application-permissions/applications/application-1/lifecycle", StringComparison.OrdinalIgnoreCase))
            {
                JsonElement body = JsonDocument.Parse(route.Request.PostData ?? "{}").RootElement;
                status = body.GetProperty("status").GetString() ?? "Active";
                await FulfillJsonAsync(route, ApplicationDetail());
                return;
            }

            if (method == "POST" && path.Equals("/api/admin/application-permissions/applications/application-1/ownership", StringComparison.OrdinalIgnoreCase))
            {
                JsonElement body = JsonDocument.Parse(route.Request.PostData ?? "{}").RootElement;
                ownerId = body.GetProperty("ownerId").GetString() ?? "owner-1";
                ownerType = body.GetProperty("ownerType").GetString() ?? "User";
                await FulfillJsonAsync(route, ApplicationDetail());
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/application-permissions/catalog", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, new
                {
                    items = new[] { new { fullPermissionKey = "patient:read", displayName = "Read patients", applicationIdentifier = "patient-api" } },
                    totalCount = 1,
                    page = 1,
                    pageSize = 50,
                    totalPages = 1
                });
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/application-permissions/history", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, new { deletedApplications = Array.Empty<object>(), removedPermissions = new[] { new { fullPermissionKey = "patient:legacy", removedAt = "2026-01-01T00:00:00Z" } } });
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/application-permissions/diagnostics", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, new { issues = new[] { new { fullPermissionKey = "patient:legacy", roleName = "Legacy Role", issueType = "MissingPermission" } } });
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/users", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, new
                {
                    items = Array.Empty<object>(),
                    totalCount = 0,
                    page = 1,
                    pageSize = 20,
                    totalPages = 0
                });
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/users/owner-1", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, new
                {
                    id = "owner-1",
                    email = "owner@example.com",
                    displayName = "Owner User",
                    status = "Active",
                    createdAt = "2026-01-01T00:00:00Z",
                    mfaEnabled = false,
                    lastLoginAt = (string?)null,
                    modifiedAt = (string?)null,
                    profile = new { }
                });
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/groups", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, new
                {
                    items = new[]
                    {
                        new
                        {
                            id = "group-1",
                            name = "Operations Group",
                            description = "Operations administrators",
                            parentGroupId = (string?)null,
                            memberCount = 2,
                            mappingCount = 1,
                            createdAt = "2026-01-01T00:00:00Z",
                            modifiedAt = (string?)null
                        }
                    },
                    totalCount = 1,
                    page = 1,
                    pageSize = 20,
                    totalPages = 1
                });
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/groups/group-1", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, new
                {
                    id = "group-1",
                    name = "Operations Group",
                    description = "Operations administrators",
                    parentGroupId = (string?)null,
                    memberCount = 2,
                    mappingCount = 1,
                    createdAt = "2026-01-01T00:00:00Z",
                    modifiedAt = (string?)null
                });
                return;
            }

            await route.FulfillAsync(new() { Status = 404, Body = $"Unexpected E2E route: {method} {path}" });
        });
    }

    private object ApplicationListItem()
    {
        return new
        {
            id = "application-1",
            applicationIdentifier = "patient-api",
            displayName = "Patient API",
            ownerId,
            ownerType,
            permissionCount = 1,
            status,
            manifestVersion = "1.0.0",
            updatedAt = "2026-01-01T00:00:00Z"
        };
    }

    private object ApplicationDetail()
    {
        return new
        {
            id = "application-1",
            applicationIdentifier = "patient-api",
            displayName = "Patient API",
            description = "Patient records",
            ownerId,
            ownerType,
            permissionCount = 1,
            status,
            manifestVersion = "1.0.0",
            schemaVersion = "1.0.0",
            manifestBaseUrl = (string?)null,
            concurrencyToken = 7,
            permissions = new[]
            {
                new
                {
                    id = "permission-1",
                    key = "patient:read",
                    fullPermissionKey = "patient:read",
                    displayName = "Read patients",
                    description = "Allows reading patient data",
                    category = "Patients",
                    status = "Active"
                }
            },
            maintainers = new[]
            {
                new { id = "maintainer-1", principalId = "owner@example.com", principalType = "User" }
            }
        };
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
