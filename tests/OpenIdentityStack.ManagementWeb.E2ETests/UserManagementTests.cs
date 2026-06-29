using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>
/// E2E coverage for the ManagementWeb Users list and user detail against the REAL admin
/// API. Data is seeded through the real endpoints / seeder; no network stubbing.
/// </summary>
public sealed class UserManagementTests : ManagementWebPageTest
{
    private Guid _adaId;
    private Guid _disabledId;
    private Guid _providerId;
    private string _adaName = "";
    private string _graceName = "";
    private string _alanName = "";
    private string _providerName = "";

    public UserManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    protected override async Task SeedAsync()
    {
        _adaName = $"Ada Lovelace {Unique}";
        _graceName = $"Grace Hopper {Unique}";
        _alanName = $"Alan Turing {Unique}";
        _providerName = $"Google {Unique}";

        _adaId = await Fixture.SeedUserAsync($"ada.{Unique}@northwind.io", _adaName, "Password123!@456");
        await Fixture.SeedUserAsync($"grace.{Unique}@northwind.io", _graceName, "Password123!@456");
        _disabledId = await Fixture.SeedUserAsync($"alan.{Unique}@northwind.io", _alanName, "Password123!@456", disabled: true);

        Guid operatorRoleId = await SeedRoleAsync($"operator-{Unique}", "Operator", "users:read");
        await SeedRoleAsync($"auditor-{Unique}", "Auditor", "audit-logs:read");
        await Fixture.AssignRoleAsync(_adaId, operatorRoleId);

        JsonNode provider = await ApiPostAsync("/api/admin/providers", new
        {
            name = $"google-{Unique}",
            displayName = _providerName,
            authority = "https://accounts.google.com",
            clientId = "northwind.apps.googleusercontent.com",
            scopes = OpenidScope,
            jitProvisioningEnabled = true,
        });
        _providerId = Guid.Parse(provider["id"]!.GetValue<string>());
    }

    private async Task<Guid> SeedRoleAsync(string name, string displayName, string permission)
    {
        JsonNode role = await ApiPostAsync("/api/admin/roles", new
        {
            name,
            displayName,
            description = displayName,
            permissions = new[] { permission },
            acknowledgeWildcardGrant = false,
        });
        return Guid.Parse(role["id"]!.GetValue<string>());
    }

    [Fact]
    public async Task OperatorCanBrowseUsersAndManageAUser()
    {
        await GotoAsync("/users");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Users", Exact = true }).WaitForAsync();
        await Page.GetByText(new Regex("Accounts, status, roles", RegexOptions.IgnoreCase)).WaitForAsync();

        // Search to isolate the seeded user, then open the detail page.
        await Page.GetByLabel("Search users").FillAsync(_adaName);
        await Page.GetByText(_adaName).ClickAsync();
        await Page.WaitForURLAsync(new Regex($"/users/{_adaId}$"));
        await Page.GetByRole(AriaRole.Heading, new() { Name = _adaName, Exact = true }).WaitForAsync();

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
    public async Task OperatorCanUnassignARole()
    {
        await GotoAsync($"/users/{_adaId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = _adaName, Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Roles", RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.GetByText("Operator", new() { Exact = true }).First.WaitForAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Remove Operator", Exact = true }).ClickAsync();
        await Page.GetByText(new Regex("Role removed", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanLinkAnUpstreamIdentity()
    {
        await GotoAsync($"/users/{_adaId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = _adaName, Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Upstream identities", RegexOptions.IgnoreCase) }).ClickAsync();

        await Page.GetByPlaceholder("Select a provider").ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = _providerName, Exact = true }).ClickAsync();
        await Page.GetByLabel("Subject").FillAsync("google-subject-12345");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Link", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Identity linked", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanEnableADisabledUser()
    {
        await GotoAsync($"/users/{_disabledId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = _alanName, Exact = true }).WaitForAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Enable user", Exact = true }).ClickAsync();
        await Page.GetByText(new Regex("User status updated", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task SearchDrivesTheUsersQuery()
    {
        await GotoAsync("/users");
        await Page.GetByLabel("Search users").FillAsync(_adaName);
        await Page.GetByText(_adaName).WaitForAsync();

        // Narrow to Grace; Ada no longer matches and drops out.
        await Page.GetByLabel("Search users").FillAsync(_graceName);
        await Page.GetByText(_graceName).WaitForAsync();
        await Page.GetByText(_adaName).WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    [Fact]
    public async Task OperatorCanCreateAUser()
    {
        await GotoAsync("/users");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add user", Exact = true }).ClickAsync();

        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        string name = $"Katherine Johnson {Unique}";
        await dialog.GetByLabel("Full name").FillAsync(name);
        await dialog.GetByLabel("Email").FillAsync($"katherine.{Unique}@northwind.io");
        await dialog.GetByLabel(new Regex("Temporary password", RegexOptions.IgnoreCase)).FillAsync("Temp1234!Temp");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create user", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex($"Created {Regex.Escape(name)}", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanDeleteAUser()
    {
        await GotoAsync("/users");
        // Search isolates Grace so the kebab menu acts on a single, known row.
        await Page.GetByLabel("Search users").FillAsync(_graceName);
        ILocator row = Page.Locator("tbody tr", new() { Has = Page.GetByText(_graceName) }).First;
        await row.WaitForAsync();

        await row.GetByRole(AriaRole.Button, new() { Name = "Row actions" }).ClickAsync();
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Delete user", Exact = true }).ClickAsync();

        // Confirm in the destructive-action dialog, then the row drops out of the list.
        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Delete user", Exact = true }).ClickAsync();
        await Page.GetByText(new Regex("User deleted", RegexOptions.IgnoreCase)).WaitForAsync();
        await Page.GetByText(_graceName).WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    [Fact]
    public async Task OperatorCanUnlinkAnUpstreamIdentity()
    {
        // Link an identity through the real API so there is one to unlink through the UI.
        await ApiPostAsync($"/api/admin/users/{_adaId}/upstream-identities", new
        {
            providerId = _providerId,
            subjectId = "google-subject-to-unlink",
        });

        await GotoAsync($"/users/{_adaId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = _adaName, Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Upstream identities", RegexOptions.IgnoreCase) }).ClickAsync();

        // The unlink button is labelled with the provider's stored name; match the single
        // identity row's button by its "Unlink …" prefix rather than the display name.
        await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Unlink ", RegexOptions.IgnoreCase) }).First.ClickAsync();
        await Page.GetByText(new Regex("Identity unlinked", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    private static readonly string[] OpenidScope = ["openid"];
}
