using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>E2E coverage for the ManagementWeb Roles list and role detail (real admin API).</summary>
public sealed class RoleManagementTests : ManagementWebPageTest
{
    private static readonly string[] UsersReadPermission = ["users:read"];

    private Guid _roleId;

    public RoleManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    protected override async Task SeedAsync()
    {
        JsonNode role = await ApiPostAsync("/api/admin/roles", new
        {
            name = $"platform-operator-{Unique}",
            displayName = "Operator",
            description = "System operator",
            permissions = UsersReadPermission,
            acknowledgeWildcardGrant = false,
        });
        _roleId = Guid.Parse(role["id"]!.GetValue<string>());
    }

    [Fact]
    public async Task OperatorCanBrowseRolesAndOpenDetail()
    {
        await GotoAsync("/roles");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Roles", Exact = true }).WaitForAsync();
        await Page.GetByText("Operator", new() { Exact = true }).First.WaitForAsync();

        await GotoAsync($"/roles/{_roleId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Operator", Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Permissions", RegexOptions.IgnoreCase) }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Settings", Exact = true }).WaitForAsync();
        await Page.GetByText("users", new() { Exact = true }).First.WaitForAsync();
        await Page.GetByText("read", new() { Exact = true }).First.WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanCreateARole()
    {
        await GotoAsync("/roles");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create role", Exact = true }).ClickAsync();

        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByLabel("Role name").FillAsync($"ops-viewer-{Guid.NewGuid():N}");
        await dialog.GetByLabel("Display name").FillAsync("Ops viewer");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create role", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Created Ops viewer", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanDeleteARole()
    {
        // Seed a dedicated, deletable role so the shared 'Operator' role stays intact.
        JsonNode role = await ApiPostAsync("/api/admin/roles", new
        {
            name = $"temp-role-{Unique}",
            displayName = $"Temp Role {Unique}",
            description = "Disposable",
            permissions = Array.Empty<string>(),
            acknowledgeWildcardGrant = false,
        });
        _ = role;

        await GotoAsync("/roles");
        string display = $"Temp Role {Unique}";
        await Page.GetByText(display, new() { Exact = true }).First.WaitForAsync();

        ILocator row = Page.Locator("tbody tr", new() { Has = Page.GetByText(display, new() { Exact = true }) }).First;
        await row.GetByRole(AriaRole.Button, new() { Name = "Row actions" }).ClickAsync();
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Delete role", Exact = true }).ClickAsync();
        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Delete role", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Role deleted", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanAddAPermissionToARole()
    {
        await GotoAsync($"/roles/{_roleId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Operator", Exact = true }).WaitForAsync();
        await Page.GetByText("users", new() { Exact = true }).First.WaitForAsync();

        await Page.GetByPlaceholder("Add a permission").ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { NameRegex = new Regex("users:write", RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Permissions updated", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanEditRoleSettings()
    {
        await GotoAsync($"/roles/{_roleId}");
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Settings", Exact = true }).ClickAsync();

        await Page.GetByLabel("Display name").FillAsync("Senior Operator");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save changes", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Role updated", RegexOptions.IgnoreCase)).WaitForAsync();
    }
}
