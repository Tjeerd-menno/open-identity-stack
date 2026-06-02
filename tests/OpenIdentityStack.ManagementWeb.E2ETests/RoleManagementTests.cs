using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>
/// E2E coverage for the ManagementWeb Roles slice.
/// API responses are route-mocked so this verifies the frontend contract and routing surface.
/// </summary>
public class RoleManagementTests : IAsyncLifetime
{
    private readonly ManagementWebAppHostFixture fixture;
    private readonly List<string> adminRequestUrls = [];
    private IBrowserContext? context;
    private IPage? page;
    private string customDisplayName = "Support Operator";
    private string[] customPermissions = ["users:read"];
    private bool customRoleDeleted;

    public RoleManagementTests(ManagementWebAppHostFixture fixture)
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

        await SetupRolesApiMocksAsync(page);
    }

    public async ValueTask DisposeAsync()
    {
        if (page is not null) await page.CloseAsync();
        if (context is not null) await context.CloseAsync();
    }

    [Fact]
    public async Task OperatorCanManageCustomRolesAndInspectSystemRoles()
    {
        string baseUrl = fixture.ManagementWebUrl ?? throw new InvalidOperationException("ManagementWeb URL was not initialized.");

        await page!.GotoAsync(new Uri(new Uri(baseUrl), "/roles").ToString());
        await page.GetByRole(AriaRole.Heading, new() { Name = "Roles", Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Cell, new() { Name = "Support Operator", Exact = true }).WaitForAsync();

        await page.GetByLabel(new Regex("Search roles", RegexOptions.IgnoreCase)).FillAsync("support");
        await page.WaitForRequestAsync(new Regex(@"/api/admin/roles\?.*search=support", RegexOptions.IgnoreCase));

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("view support operator", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Support Operator", Exact = true }).WaitForAsync();
        await page.GetByText("users:read").WaitForAsync();

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("edit role", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByLabel("Display name").FillAsync("Support Lead");
        await page.GetByRole(AriaRole.Checkbox, new() { NameRegex = new Regex("assign roles", RegexOptions.IgnoreCase) }).CheckAsync();
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("save role", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Support Lead", Exact = true }).WaitForAsync();
        await page.GetByText("roles:assign").WaitForAsync();

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^delete role$", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("delete support lead", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/roles/?$"));
        customRoleDeleted.ShouldBeTrue();

        await page.GotoAsync(new Uri(new Uri(baseUrl), "/roles/role-system").ToString());
        await page.GetByRole(AriaRole.Heading, new() { Name = "Administrator", Exact = true }).WaitForAsync();
        await page.GetByText(new Regex("system role and cannot be deleted", RegexOptions.IgnoreCase)).WaitForAsync();
        (await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^delete role$", RegexOptions.IgnoreCase) }).CountAsync())
            .ShouldBe(0);

        adminRequestUrls.Any((url) => url.Contains("/api/admin/roles", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/permissions/platform", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/service-accounts", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/clients", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
    }

    private async Task SetupRolesApiMocksAsync(IPage testPage)
    {
        await testPage.RouteAsync("**/api/admin/**", async route =>
        {
            Uri uri = new(route.Request.Url);
            string path = uri.AbsolutePath;
            string method = route.Request.Method;

            if (method == "GET" && path.Equals("/api/admin/permissions/platform", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, PlatformPermissionCatalog());
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/roles", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, RolesList());
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/roles/role-custom", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, CustomRole());
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/roles/role-system", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, SystemRole());
                return;
            }

            if (method == "PUT" && path.Equals("/api/admin/roles/role-custom", StringComparison.OrdinalIgnoreCase))
            {
                RoleUpdateRequest? request = JsonSerializer.Deserialize<RoleUpdateRequest>(
                    route.Request.PostData ?? "{}",
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                customDisplayName = request?.DisplayName ?? customDisplayName;
                customPermissions = request?.Permissions ?? customPermissions;
                await FulfillJsonAsync(route, CustomRole());
                return;
            }

            if (method == "DELETE" && path.Equals("/api/admin/roles/role-custom", StringComparison.OrdinalIgnoreCase))
            {
                customRoleDeleted = true;
                await route.FulfillAsync(new() { Status = 204 });
                return;
            }

            await route.FulfillAsync(new() { Status = 404, Body = $"Unexpected E2E route: {method} {path}" });
        });
    }

    private object RolesList()
    {
        object[] items = customRoleDeleted ? [SystemRole()] : [SystemRole(), CustomRole()];

        return new
        {
            items,
            totalCount = items.Length,
            page = 1,
            pageSize = 20,
            totalPages = 1
        };
    }

    private object CustomRole()
    {
        return new
        {
            id = "role-custom",
            name = "support",
            displayName = customDisplayName,
            description = "Support desk",
            isSystemRole = false,
            isActive = true,
            permissions = customPermissions
        };
    }

    private static object SystemRole()
    {
        return new
        {
            id = "role-system",
            name = "admin",
            displayName = "Administrator",
            description = "System administrator",
            isSystemRole = true,
            isActive = true,
            permissions = new[] { "*" }
        };
    }

    private static object PlatformPermissionCatalog()
    {
        return new
        {
            items = new[]
            {
                new
                {
                    permission = "users:read",
                    resource = "users",
                    action = "read",
                    kind = "concrete",
                    displayName = "Read Users",
                    assignable = true
                },
                new
                {
                    permission = "roles:assign",
                    resource = "roles",
                    action = "assign",
                    kind = "concrete",
                    displayName = "Assign Roles",
                    assignable = true
                },
                new
                {
                    permission = "users:*",
                    resource = "users",
                    action = "*",
                    kind = "wildcard",
                    displayName = "Users All",
                    assignable = true
                }
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

    private sealed record RoleUpdateRequest(string DisplayName, string? Description, string[] Permissions, bool AcknowledgeWildcardGrant);
}
