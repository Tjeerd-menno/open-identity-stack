using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

public class UserManagementTests : IAsyncLifetime
{
    private const string TestUserId = "user-test-1";
    private const string TestRoleId = "role-test-1";
    private readonly ManagementWebAppHostFixture fixture;
    private IBrowserContext? context;
    private IPage? page;

    public UserManagementTests(ManagementWebAppHostFixture fixture)
    {
        this.fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        context = await fixture.CreateBrowserContextAsync();
        page = await context.NewPageAsync();
        page.SetDefaultTimeout(60_000);
        page.SetDefaultNavigationTimeout(60_000);
        await SetupApiMocksAsync(page);
    }

    public async ValueTask DisposeAsync()
    {
        if (page is not null) await page.CloseAsync();
        if (context is not null) await context.CloseAsync();
    }

    [Fact]
    public async Task OperatorCanCompleteUsersWorkflow()
    {
        string baseUrl = fixture.ManagementWebUrl ?? throw new InvalidOperationException("ManagementWeb URL was not initialized.");

        await page!.GotoAsync(new Uri(new Uri(baseUrl), "/users").ToString());

        await page.GetByRole(AriaRole.Heading, new() { Name = "Users", Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Navigation, new() { Name = "Management navigation", Exact = true }).WaitForAsync();
        await page.GetByText(new Regex("Operate user accounts", RegexOptions.IgnoreCase)).WaitForAsync();

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Ada Lovelace", RegexOptions.IgnoreCase) }).First.ClickAsync();
        await page.GetByRole(AriaRole.Region, new() { NameRegex = new Regex("User details", RegexOptions.IgnoreCase) }).WaitForAsync();
        await page.GetByText(new Regex("Engineering", RegexOptions.IgnoreCase)).WaitForAsync();
        await page.GetByText(new Regex("Google: ada-google", RegexOptions.IgnoreCase)).WaitForAsync();
        await page.GetByLabel(new Regex("Display name", RegexOptions.IgnoreCase)).FillAsync("Updated operator");
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Save changes", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Disable user", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByLabel(new Regex("New password", RegexOptions.IgnoreCase)).FillAsync("Temp1234!");
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Reset password", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Unassign Operator", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByLabel(new Regex("Assign role", RegexOptions.IgnoreCase)).SelectOptionAsync([new SelectOptionValue { Index = 1 }]);
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Assign selected role", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByLabel(new Regex("Provider id", RegexOptions.IgnoreCase)).FillAsync("oidc");
        await page.GetByLabel(new Regex("Subject", RegexOptions.IgnoreCase)).FillAsync("ada-oidc");
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Link upstream identity", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Unlink Google", RegexOptions.IgnoreCase) }).ClickAsync();
    }

    private static async Task SetupApiMocksAsync(IPage testPage)
    {
        await testPage.RouteAsync("**/api/admin/**", async route =>
        {
            Uri uri = new(route.Request.Url);
            string path = uri.AbsolutePath;
            string method = route.Request.Method;

            if (Regex.IsMatch(path, @"/api/admin/users/[^/]+/roles/[^/]+$", RegexOptions.IgnoreCase) && method == "POST")
            {
                await route.FulfillAsync(new() { Status = 204 });
                return;
            }

            if (Regex.IsMatch(path, @"/api/admin/users/[^/]+/roles/[^/]+$", RegexOptions.IgnoreCase) && method == "DELETE")
            {
                await route.FulfillAsync(new() { Status = 204 });
                return;
            }

            if (Regex.IsMatch(path, @"/api/admin/users/[^/]+/roles$", RegexOptions.IgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, new { userId = TestUserId, roles = new[] { MockRole() } });
                return;
            }

            if (Regex.IsMatch(path, @"/api/admin/users/[^/]+/groups$", RegexOptions.IgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, new[]
                {
                    new
                    {
                        id = "group-test-1",
                        name = "engineering",
                        displayName = "Engineering",
                        memberCount = 3
                    }
                });
                return;
            }

            if (Regex.IsMatch(path, @"/api/admin/users/[^/]+/upstream-identities$", RegexOptions.IgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, new
                {
                    items = new[]
                    {
                        new
                        {
                            providerId = "google",
                            providerName = "Google",
                            subject = "ada-google",
                            displayName = "Ada Google",
                            linkedAt = "2024-01-02T00:00:00Z"
                        }
                    }
                });
                return;
            }

            if (Regex.IsMatch(path, @"/api/admin/users/[^/]+/upstream-identities$", RegexOptions.IgnoreCase) && method == "POST")
            {
                await FulfillJsonAsync(route, new
                {
                    providerId = "oidc",
                    subject = "ada-oidc"
                });
                return;
            }

            if (Regex.IsMatch(path, @"/api/admin/users/[^/]+/upstream-identities/[^/]+$", RegexOptions.IgnoreCase) && method == "DELETE")
            {
                await route.FulfillAsync(new() { Status = 204 });
                return;
            }

            if (Regex.IsMatch(path, @"/api/admin/users/[^/]+/reset-password$", RegexOptions.IgnoreCase) && method == "POST")
            {
                await route.FulfillAsync(new() { Status = 204 });
                return;
            }

            if (Regex.IsMatch(path, @"/api/admin/users/[^/]+/disable$", RegexOptions.IgnoreCase) && method == "POST")
            {
                await route.FulfillAsync(new() { Status = 204 });
                return;
            }

            if (Regex.IsMatch(path, @"/api/admin/users/[^/]+$", RegexOptions.IgnoreCase) && method == "PUT")
            {
                await route.FulfillAsync(new() { Status = 204 });
                return;
            }

            if (Regex.IsMatch(path, @"/api/admin/users/[^/]+$", RegexOptions.IgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, MockUser());
                return;
            }

            if (path.StartsWith("/api/admin/roles", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, new
                {
                    items = new[] { MockRole() },
                    totalCount = 1,
                    page = 1,
                    pageSize = 100,
                    totalPages = 1
                });
                return;
            }

            if (path.StartsWith("/api/admin/users", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, new
                {
                    items = new[] { MockUser() },
                    totalCount = 1,
                    page = 1,
                    pageSize = 20,
                    totalPages = 1
                });
                return;
            }

            await route.ContinueAsync();
        });
    }

    private static object MockUser()
    {
        return new
        {
            id = TestUserId,
            email = "admin@test.com",
            displayName = "Ada Lovelace",
            status = "Active",
            createdAt = "2024-01-01T00:00:00Z",
            mfaEnabled = false,
            lastLoginAt = (string?)null,
            modifiedAt = (string?)null,
            profile = new { }
        };
    }

    private static object MockRole()
    {
        return new
        {
            id = TestRoleId,
            name = "operator",
            displayName = "Operator",
            isSystemRole = false,
            isActive = true
        };
    }

    private static Task FulfillJsonAsync(IRoute route, object body)
    {
        return route.FulfillAsync(new()
        {
            Status = 200,
            ContentType = "application/json",
            Body = JsonSerializer.Serialize(body)
        });
    }
}
