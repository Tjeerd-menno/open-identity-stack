using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>
/// E2E coverage for the ManagementWeb Users list and user detail (tabs, reset
/// password, disable, role assignment) against a stubbed admin API.
/// </summary>
public sealed class UserManagementTests : ManagementWebPageTest
{
    private const string UserId = "user-test-1";

    public UserManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task OperatorCanBrowseUsersAndManageAUser()
    {
        await StubUsersApiAsync();

        await GotoAsync("/users");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Users", Exact = true }).WaitForAsync();
        await Page.GetByText(new Regex("Accounts, status, roles", RegexOptions.IgnoreCase)).WaitForAsync();
        await Page.GetByText("Ada Lovelace").WaitForAsync();

        // Open the detail page.
        await Page.GetByText("Ada Lovelace").ClickAsync();
        await Page.WaitForURLAsync(new Regex($"/users/{UserId}$"));
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Ada Lovelace", Exact = true }).WaitForAsync();

        // Roles tab shows the assigned role.
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Roles", RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.GetByText("Operator", new() { Exact = true }).First.WaitForAsync();

        // Assign another role.
        await Page.GetByPlaceholder("Select a role").ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "Auditor", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Assign", Exact = true }).ClickAsync();
        await Page.GetByText(new Regex("Role assigned", RegexOptions.IgnoreCase)).WaitForAsync();

        // Reset password.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Reset password", Exact = true }).ClickAsync();
        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByLabel(new Regex("New temporary password", RegexOptions.IgnoreCase)).FillAsync("Temp1234!Temp");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Reset password", Exact = true }).ClickAsync();
        await Page.GetByText(new Regex("Password reset", RegexOptions.IgnoreCase)).WaitForAsync();

        // Disable the user.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Disable user", Exact = true }).ClickAsync();
        await Page.GetByText(new Regex("User status updated", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanCreateAUser()
    {
        await StubUsersApiAsync();

        await GotoAsync("/users");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add user", Exact = true }).ClickAsync();

        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByLabel("Full name").FillAsync("Grace Hopper");
        await dialog.GetByLabel("Email").FillAsync("grace.hopper@northwind.io");
        await dialog.GetByLabel(new Regex("Temporary password", RegexOptions.IgnoreCase)).FillAsync("Temp1234!Temp");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create user", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Created Grace Hopper", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    private Task StubUsersApiAsync() =>
        Page.RouteAsync("**/api/admin/**", async route =>
        {
            string path = new Uri(route.Request.Url).AbsolutePath;
            string method = route.Request.Method;

            if (Regex.IsMatch(path, @"/api/admin/users/[^/]+/roles/[^/]+$", RegexOptions.IgnoreCase))
            {
                await NoContentAsync(route);
                return;
            }
            if (Regex.IsMatch(path, @"/api/admin/users/[^/]+/roles$", RegexOptions.IgnoreCase))
            {
                await FulfillJsonAsync(route, new { userId = UserId, roles = new[] { Role("operator", "Operator") } });
                return;
            }
            if (Regex.IsMatch(path, @"/api/admin/users/[^/]+/groups$", RegexOptions.IgnoreCase))
            {
                await FulfillJsonAsync(route, new[] { new { id = "g1", name = "engineering", description = "Engineering", memberCount = 3 } });
                return;
            }
            if (Regex.IsMatch(path, @"/api/admin/users/[^/]+/upstream-identities", RegexOptions.IgnoreCase))
            {
                if (method == "GET")
                {
                    await FulfillJsonAsync(route, new { items = Array.Empty<object>() });
                    return;
                }
                await NoContentAsync(route);
                return;
            }
            if (Regex.IsMatch(path, @"/api/admin/users/[^/]+/reset-password$", RegexOptions.IgnoreCase))
            {
                await FulfillJsonAsync(route, new { userId = UserId, temporaryPassword = "Temp1234!Temp" });
                return;
            }
            if (Regex.IsMatch(path, @"/api/admin/users/[^/]+/disable$", RegexOptions.IgnoreCase))
            {
                await FulfillJsonAsync(route, new { userId = UserId, status = "Disabled" });
                return;
            }
            if (Regex.IsMatch(path, @"/api/admin/users/[^/]+$", RegexOptions.IgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, MockUser());
                return;
            }
            if (path.StartsWith("/api/admin/roles", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, Paged(Role("operator", "Operator"), Role("auditor", "Auditor")));
                return;
            }
            if (path.StartsWith("/api/admin/providers", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, Array.Empty<object>());
                return;
            }
            if (path == "/api/admin/users" && method == "POST")
            {
                await FulfillJsonAsync(route, new
                {
                    id = "user-new-1",
                    email = "grace.hopper@northwind.io",
                    displayName = "Grace Hopper",
                    status = "Active",
                    createdAt = "2026-06-28T00:00:00Z",
                    mfaEnabled = false,
                    lastLoginAt = (string?)null,
                    modifiedAt = (string?)null,
                    profile = new { },
                });
                return;
            }
            if (path.StartsWith("/api/admin/users", StringComparison.OrdinalIgnoreCase) && method == "GET")
            {
                await FulfillJsonAsync(route, Paged(MockUser()));
                return;
            }

            await NoContentAsync(route);
        });

    private static object MockUser() => new
    {
        id = UserId,
        email = "ada@northwind.io",
        displayName = "Ada Lovelace",
        status = "Active",
        createdAt = "2026-06-01T00:00:00Z",
        mfaEnabled = false,
        lastLoginAt = (string?)null,
        modifiedAt = (string?)null,
        profile = new { },
    };

    private static object Role(string name, string displayName) => new
    {
        id = name,
        name,
        displayName,
        isSystemRole = false,
        isActive = true,
        permissions = Array.Empty<string>(),
    };
}
