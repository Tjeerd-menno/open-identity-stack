using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>E2E coverage for the ManagementWeb Groups list and group detail.</summary>
public sealed class GroupManagementTests : ManagementWebPageTest
{
    private const string GroupId = "group-test-1";

    public GroupManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task OperatorCanBrowseGroupsAndOpenDetail()
    {
        await StubAsync();

        await GotoAsync("/groups");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Groups", Exact = true }).WaitForAsync();
        await Page.GetByText("Engineering", new() { Exact = true }).First.WaitForAsync();

        await Page.Locator("tbody tr").First.ClickAsync();
        await Page.WaitForURLAsync(new Regex($"/groups/{GroupId}$"));
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Engineering", Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Members", RegexOptions.IgnoreCase) }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Mappings", RegexOptions.IgnoreCase) }).WaitForAsync();
        await Page.GetByText("Ada Lovelace").WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanCreateAGroup()
    {
        await StubAsync();

        await GotoAsync("/groups");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create group", Exact = true }).ClickAsync();

        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByLabel("Group name").FillAsync("Engineering");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create group", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Created Engineering", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanAddAMemberToAGroup()
    {
        await StubAsync();

        await GotoAsync($"/groups/{GroupId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Engineering", Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add members", Exact = true }).ClickAsync();

        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByText("Grace Hopper").WaitForAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Add", Exact = true }).First.ClickAsync();

        await Page.GetByText(new Regex("Member added", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    private Task StubAsync() =>
        Page.RouteAsync("**/api/admin/**", async route =>
        {
            string path = new Uri(route.Request.Url).AbsolutePath;
            string method = route.Request.Method;

            if (Regex.IsMatch(path, @"/api/admin/groups/[^/]+/members$", RegexOptions.IgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, Paged(new { userId = "u1", email = "ada@northwind.io", displayName = "Ada Lovelace", addedAt = "2026-06-01T00:00:00Z" }));
                return;
            }
            if (Regex.IsMatch(path, @"/api/admin/groups/[^/]+/mappings$", RegexOptions.IgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, new { items = Array.Empty<object>() });
                return;
            }
            if (Regex.IsMatch(path, @"/api/admin/groups/[^/]+$", RegexOptions.IgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, MockGroup());
                return;
            }
            if (path == "/api/admin/groups" && method == "POST")
            {
                await FulfillJsonAsync(route, MockGroup());
                return;
            }
            if (path.StartsWith("/api/admin/groups", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, Paged(MockGroup()));
                return;
            }
            if (path.StartsWith("/api/admin/users", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, Paged(new
                {
                    id = "u-pick-1",
                    email = "grace.hopper@northwind.io",
                    displayName = "Grace Hopper",
                    status = "Active",
                    createdAt = "2026-06-01T00:00:00Z",
                }));
                return;
            }
            await NoContentAsync(route);
        });

    private static object MockGroup() => new
    {
        id = GroupId,
        name = "Engineering",
        description = "Platform engineering",
        parentGroupId = (string?)null,
        memberCount = 1,
        mappingCount = 0,
        createdAt = "2026-06-01T00:00:00Z",
        modifiedAt = (string?)null,
    };
}
