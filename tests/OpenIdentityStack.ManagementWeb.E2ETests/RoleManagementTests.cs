using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>E2E coverage for the ManagementWeb Roles list and role detail.</summary>
public sealed class RoleManagementTests : ManagementWebPageTest
{
    private const string RoleId = "role-test-1";

    public RoleManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task OperatorCanBrowseRolesAndOpenDetail()
    {
        await StubAsync();

        await GotoAsync("/roles");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Roles", Exact = true }).WaitForAsync();
        await Page.GetByText("Operator", new() { Exact = true }).First.WaitForAsync();

        // Open the role detail. (List-row navigation is covered by the other slices;
        // here we navigate directly to keep the detail assertions deterministic.)
        await GotoAsync($"/roles/{RoleId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Operator", Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Permissions", RegexOptions.IgnoreCase) }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Settings", Exact = true }).WaitForAsync();
        // The Permissions tab groups grants by family ("users") with an action badge ("read").
        await Page.GetByText("users", new() { Exact = true }).First.WaitForAsync();
        await Page.GetByText("read", new() { Exact = true }).First.WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanCreateARole()
    {
        await StubAsync();

        await GotoAsync("/roles");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create role", Exact = true }).ClickAsync();

        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByLabel("Role name").FillAsync("ops-viewer");
        await dialog.GetByLabel("Display name").FillAsync("Ops viewer");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create role", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Created Ops viewer", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanDeleteARole()
    {
        await StubAsync();

        await GotoAsync("/roles");
        await Page.GetByText("Operator", new() { Exact = true }).First.WaitForAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Row actions" }).First.ClickAsync();
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Delete role", Exact = true }).ClickAsync();
        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Delete role", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Role deleted", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    private Task StubAsync() =>
        Page.RouteAsync("**/api/admin/**", async route =>
        {
            string path = new Uri(route.Request.Url).AbsolutePath;
            string method = route.Request.Method;

            if (path.StartsWith("/api/admin/permissions/platform", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, new
                {
                    items = new[]
                    {
                        new { permission = "users:write", resource = "users", action = "write", kind = "concrete", displayName = "Write users", assignable = true },
                    },
                });
                return;
            }
            if (Regex.IsMatch(path, @"/api/admin/roles/[^/]+$", RegexOptions.IgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, MockRole(detail: true));
                return;
            }
            if (path == "/api/admin/roles" && method == "POST")
            {
                await FulfillJsonAsync(route, new
                {
                    id = "role-new-1",
                    name = "ops-viewer",
                    displayName = "Ops viewer",
                    isSystemRole = false,
                    isActive = true,
                    description = (string?)null,
                    permissions = Array.Empty<string>(),
                });
                return;
            }
            if (path.StartsWith("/api/admin/roles", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, Paged(MockRole(detail: false)));
                return;
            }
            await NoContentAsync(route);
        });

    private static object MockRole(bool detail) => new
    {
        id = RoleId,
        name = "operator",
        displayName = "Operator",
        isSystemRole = false,
        isActive = true,
        description = "System operator",
        permissions = detail ? new[] { "users:read" } : Array.Empty<string>(),
    };
}
