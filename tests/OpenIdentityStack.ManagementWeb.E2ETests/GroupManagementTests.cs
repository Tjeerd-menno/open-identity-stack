using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>
/// E2E coverage for the ManagementWeb Groups slice.
/// API responses are route-mocked so this verifies the frontend contract and routing surface.
/// </summary>
public class GroupManagementTests : IAsyncLifetime
{
    private readonly ManagementWebAppHostFixture fixture;
    private readonly List<string> adminRequestUrls = [];
    private IBrowserContext? context;
    private IPage? page;
    private string groupName = "Engineering";
    private string groupDescription = "Engineering team";
    private bool groupDeleted;

    public GroupManagementTests(ManagementWebAppHostFixture fixture)
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

        await SetupGroupsApiMocksAsync(page);
    }

    public async ValueTask DisposeAsync()
    {
        if (page is not null) await page.CloseAsync();
        if (context is not null) await context.CloseAsync();
    }

    [Fact]
    public async Task OperatorCanManageGroupsMembersAndMappings()
    {
        string baseUrl = fixture.ManagementWebUrl ?? throw new InvalidOperationException("ManagementWeb URL was not initialized.");

        await page!.GotoAsync(new Uri(new Uri(baseUrl), "/groups").ToString());
        await page.GetByRole(AriaRole.Heading, new() { Name = "Groups", Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Cell, new() { Name = "Engineering", Exact = true }).WaitForAsync();

        await page.GetByLabel(new Regex("Search groups", RegexOptions.IgnoreCase)).FillAsync("eng");
        await page.WaitForRequestAsync(new Regex(@"/api/admin/groups\?.*search=eng", RegexOptions.IgnoreCase));

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("view engineering", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Engineering", Exact = true }).WaitForAsync();
        await page.GetByText("Ada Lovelace").WaitForAsync();
        await page.GetByText("department:engineering").WaitForAsync();

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("add member", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByLabel(new Regex("Select user", RegexOptions.IgnoreCase)).SelectOptionAsync(["user-2"]);
        await page.GetByRole(AriaRole.Dialog, new() { NameRegex = new Regex("add member", RegexOptions.IgnoreCase) })
            .GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^add member$", RegexOptions.IgnoreCase) })
            .ClickAsync();
        await page.GetByRole(AriaRole.Dialog, new() { NameRegex = new Regex("add member", RegexOptions.IgnoreCase) })
            .WaitForAsync(new() { State = WaitForSelectorState.Hidden });

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("remove member ada lovelace", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog, new() { NameRegex = new Regex("remove member", RegexOptions.IgnoreCase) })
            .GetByRole(AriaRole.Button, new() { NameRegex = new Regex("remove member", RegexOptions.IgnoreCase) })
            .ClickAsync();

        await page.GetByRole(AriaRole.Region, new() { NameRegex = new Regex("group mappings", RegexOptions.IgnoreCase) })
            .GetByRole(AriaRole.Button, new() { NameRegex = new Regex("add mapping", RegexOptions.IgnoreCase) })
            .ClickAsync();
        await page.GetByLabel(new Regex("Mapping type", RegexOptions.IgnoreCase)).SelectOptionAsync(["Claim"]);
        await page.GetByLabel(new Regex("Mapping value", RegexOptions.IgnoreCase)).FillAsync("tier:gold");
        await page.GetByRole(AriaRole.Dialog, new() { NameRegex = new Regex("add mapping", RegexOptions.IgnoreCase) })
            .GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^add mapping$", RegexOptions.IgnoreCase) })
            .ClickAsync();
        await page.GetByRole(AriaRole.Dialog, new() { NameRegex = new Regex("add mapping", RegexOptions.IgnoreCase) })
            .WaitForAsync(new() { State = WaitForSelectorState.Hidden });

        await page.GetByRole(AriaRole.Region, new() { NameRegex = new Regex("group mappings", RegexOptions.IgnoreCase) })
            .GetByRole(AriaRole.Button, new() { NameRegex = new Regex("add mapping", RegexOptions.IgnoreCase) })
            .ClickAsync();
        await page.GetByLabel(new Regex("Mapping type", RegexOptions.IgnoreCase)).SelectOptionAsync(["Role"]);
        await page.GetByLabel(new Regex("Select role", RegexOptions.IgnoreCase)).SelectOptionAsync(["role-1"]);
        await page.GetByRole(AriaRole.Dialog, new() { NameRegex = new Regex("add mapping", RegexOptions.IgnoreCase) })
            .GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^add mapping$", RegexOptions.IgnoreCase) })
            .ClickAsync();
        await page.GetByRole(AriaRole.Dialog, new() { NameRegex = new Regex("add mapping", RegexOptions.IgnoreCase) })
            .WaitForAsync(new() { State = WaitForSelectorState.Hidden });

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("remove mapping department:engineering", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Dialog, new() { NameRegex = new Regex("remove mapping", RegexOptions.IgnoreCase) })
            .GetByRole(AriaRole.Button, new() { NameRegex = new Regex("remove mapping", RegexOptions.IgnoreCase) })
            .ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("edit group", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByLabel(new Regex("Group name", RegexOptions.IgnoreCase)).FillAsync("Platform Engineering");
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("save group", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Heading, new() { Name = "Platform Engineering", Exact = true }).WaitForAsync();

        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^delete group$", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("delete platform engineering", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/groups/?$"));
        groupDeleted.ShouldBeTrue();

        adminRequestUrls.Any((url) => url.Contains("/api/admin/groups", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/users", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/roles", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/service-accounts", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
        adminRequestUrls.Any((url) => url.Contains("/api/admin/clients", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();
    }

    private async Task SetupGroupsApiMocksAsync(IPage testPage)
    {
        await testPage.RouteAsync("**/api/admin/**", async route =>
        {
            Uri uri = new(route.Request.Url);
            string path = uri.AbsolutePath;
            string method = route.Request.Method;

            if (method == "GET" && path.Equals("/api/admin/groups", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, GroupsList());
                return;
            }

            if (method == "POST" && path.Equals("/api/admin/groups", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, Group(), 201);
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/groups/group-1", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, Group());
                return;
            }

            if (method == "PATCH" && path.Equals("/api/admin/groups/group-1", StringComparison.OrdinalIgnoreCase))
            {
                GroupUpdateRequest? request = JsonSerializer.Deserialize<GroupUpdateRequest>(
                    route.Request.PostData ?? "{}",
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                groupName = request?.Name ?? groupName;
                groupDescription = request?.Description ?? groupDescription;
                await FulfillJsonAsync(route, Group());
                return;
            }

            if (method == "DELETE" && path.Equals("/api/admin/groups/group-1", StringComparison.OrdinalIgnoreCase))
            {
                groupDeleted = true;
                await route.FulfillAsync(new() { Status = 204 });
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/groups/group-1/members", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, GroupMembers());
                return;
            }

            if (method == "POST" && path.Equals("/api/admin/groups/group-1/members/user-2", StringComparison.OrdinalIgnoreCase))
            {
                await route.FulfillAsync(new() { Status = 201 });
                return;
            }

            if (method == "DELETE" && path.Equals("/api/admin/groups/group-1/members/user-1", StringComparison.OrdinalIgnoreCase))
            {
                await route.FulfillAsync(new() { Status = 204 });
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/groups/group-1/mappings", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, GroupMappings());
                return;
            }

            if (method == "POST" && path.Equals("/api/admin/groups/group-1/mappings", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, new
                {
                    id = "mapping-added",
                    type = "Claim",
                    value = "tier:gold",
                    createdAt = "2026-01-02T00:00:00Z"
                }, 201);
                return;
            }

            if (method == "DELETE" && path.Equals("/api/admin/groups/group-1/mappings/mapping-1", StringComparison.OrdinalIgnoreCase))
            {
                await route.FulfillAsync(new() { Status = 204 });
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/users", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, UsersList());
                return;
            }

            if (method == "GET" && path.Equals("/api/admin/roles", StringComparison.OrdinalIgnoreCase))
            {
                await FulfillJsonAsync(route, RolesList());
                return;
            }

            await route.FulfillAsync(new() { Status = 404, Body = $"Unexpected E2E route: {method} {path}" });
        });
    }

    private object GroupsList()
    {
        object[] items = groupDeleted ? [] : [Group()];

        return new
        {
            items,
            totalCount = items.Length,
            page = 1,
            pageSize = 20,
            totalPages = 1
        };
    }

    private object Group()
    {
        return new
        {
            id = "group-1",
            name = groupName,
            description = groupDescription,
            parentGroupId = (string?)null,
            memberCount = 1,
            mappingCount = 1,
            createdAt = "2026-01-01T00:00:00Z",
            modifiedAt = (string?)null
        };
    }

    private static object GroupMembers()
    {
        return new
        {
            items = new[]
            {
                new
                {
                    id = "member-1",
                    userId = "user-1",
                    email = "ada@example.com",
                    displayName = "Ada Lovelace"
                }
            },
            totalCount = 1,
            page = 1,
            pageSize = 20,
            totalPages = 1
        };
    }

    private static object GroupMappings()
    {
        return new
        {
            items = new[]
            {
                new
                {
                    id = "mapping-1",
                    type = "Claim",
                    value = "department:engineering",
                    createdAt = "2026-01-01T00:00:00Z"
                }
            }
        };
    }

    private static object UsersList()
    {
        return new
        {
            items = new[]
            {
                new
                {
                    id = "user-2",
                    email = "grace@example.com",
                    displayName = "Grace Hopper",
                    status = "Active",
                    createdAt = "2026-01-02T00:00:00Z"
                }
            },
            totalCount = 1,
            page = 1,
            pageSize = 100,
            totalPages = 1
        };
    }

    private static object RolesList()
    {
        return new
        {
            items = new[]
            {
                new
                {
                    id = "role-1",
                    name = "operator",
                    displayName = "Operator",
                    description = (string?)null,
                    isSystemRole = false,
                    isActive = true,
                    permissions = new[] { "users:read" }
                }
            },
            totalCount = 1,
            page = 1,
            pageSize = 100,
            totalPages = 1
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

    private sealed record GroupUpdateRequest(string Name, string? Description);
}
