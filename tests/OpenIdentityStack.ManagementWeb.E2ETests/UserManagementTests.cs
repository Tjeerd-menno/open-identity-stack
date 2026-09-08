using System.Text.RegularExpressions;
using System.Net;
using System.Net.Http.Json;
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
    private string _auditorRoleName = "";

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
        // Display name must be unique: the AppHost database is shared across every test in this
        // class, so a constant "Auditor" name would surface one option per seeded test run and
        // make the role Select's exact-name lookup ambiguous (strict-mode violation).
        _auditorRoleName = $"Auditor {Unique}";
        await SeedRoleAsync($"auditor-{Unique}", _auditorRoleName, "audit-logs:read");
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
        await Page.GetByRole(AriaRole.Option, new() { Name = _auditorRoleName, Exact = true }).ClickAsync();
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
        ILocator toast = Page.GetByText(new Regex("User status updated", RegexOptions.IgnoreCase));
        await toast.WaitForAsync();
        (await toast.IsVisibleAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task OperatorCanUnassignARole()
    {
        await GotoAsync($"/users/{_adaId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = _adaName, Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Roles", RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.GetByText("Operator", new() { Exact = true }).First.WaitForAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Remove Operator", Exact = true }).ClickAsync();
        ILocator toast = Page.GetByText(new Regex("Role removed", RegexOptions.IgnoreCase));
        await toast.WaitForAsync();
        (await toast.IsVisibleAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task OperatorCannotLinkAnExistingAccountUsingRawIdentifiers()
    {
        await GotoAsync($"/users/{_adaId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = _adaName, Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Upstream identities", RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.GetByText("Linking an existing account requires proof of account ownership. This workflow is not yet available.", new() { Exact = true }).WaitForAsync();
        (await Page.GetByPlaceholder("Select a provider").CountAsync()).ShouldBe(0);
        (await Page.GetByLabel("Subject", new() { Exact = true }).CountAsync()).ShouldBe(0);
        (await Page.GetByRole(AriaRole.Button, new() { Name = "Link", Exact = true }).CountAsync()).ShouldBe(0);
        HttpResponseMessage denied = await Api.PostAsJsonAsync($"/api/admin/users/{_adaId}/upstream-identities", new
        {
            providerId = _providerId, subjectId = "unproven-subject"
        });
        denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await denied.Content.ReadAsStringAsync()).ShouldContain("Forbidden.UpstreamIdentity.ProofRequired");
    }
    [Fact]
    public async Task OperatorCanEnableADisabledUser()
    {
        await GotoAsync($"/users/{_disabledId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = _alanName, Exact = true }).WaitForAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Enable user", Exact = true }).ClickAsync();
        ILocator toast = Page.GetByText(new Regex("User status updated", RegexOptions.IgnoreCase));
        await toast.WaitForAsync();
        (await toast.IsVisibleAsync()).ShouldBeTrue();
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
        ILocator adaRow = Page.GetByText(_adaName);
        await adaRow.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        (await adaRow.IsVisibleAsync()).ShouldBeFalse();
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

        ILocator toast = Page.GetByText(new Regex($"Created {Regex.Escape(name)}", RegexOptions.IgnoreCase));
        await toast.WaitForAsync();
        (await toast.IsVisibleAsync()).ShouldBeTrue();
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
        ILocator graceRow = Page.GetByText(_graceName);
        await graceRow.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        (await graceRow.IsVisibleAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task OperatorCanInspectQuarantineButCannotEraseAssociationEvidence()
    {
        await Fixture.SeedLegacyIdentityAsync(_adaId, _providerId, $"google-{Unique}", "legacy-quarantined-subject");
        await GotoAsync($"/users/{_adaId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = _adaName, Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Upstream identities", RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.GetByText("Quarantined — authentication and migration blocked", new() { Exact = true }).WaitForAsync();
        (await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Unlink ", RegexOptions.IgnoreCase) }).CountAsync()).ShouldBe(0);
        JsonNode identities = await ApiGetAsync($"/api/admin/users/{_adaId}/upstream-identities");
        identities.ToJsonString().ShouldContain("legacy-quarantined-subject");
        identities.ToJsonString().ShouldContain("\"isQuarantined\":true");

        await GotoAsync("/users");
        await Page.GetByLabel("Search users").FillAsync(_adaName);
        ILocator row = Page.Locator("tbody tr", new() { Has = Page.GetByText(_adaName) }).First;
        await row.WaitForAsync();
        int deletionRequests = 0;
        Page.Request += (_, request) =>
        {
            if (request.Method is "DELETE" or "POST" && new Uri(request.Url).AbsolutePath == $"/api/admin/users/{_adaId}")
            {
                Interlocked.Increment(ref deletionRequests);
            }
        };
        await row.GetByRole(AriaRole.Button, new() { Name = "Row actions" }).ClickAsync();
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Delete user", Exact = true }).ClickAsync();
        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByText(new Regex("Quarantined identity evidence must be retained", RegexOptions.IgnoreCase)).WaitForAsync();
        await Assertions.Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Delete user", Exact = true })).ToBeDisabledAsync();
        string screenshotDirectory = Path.Combine(Path.GetTempPath(), "ois-quarantine-retention-e2e");
        Directory.CreateDirectory(screenshotDirectory);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(screenshotDirectory, "quarantined-user-deletion-blocked.png"), FullPage = true, Animations = ScreenshotAnimations.Disabled });
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        deletionRequests.ShouldBe(0);
        JsonNode retainedIdentities = await ApiGetAsync($"/api/admin/users/{_adaId}/upstream-identities");
        retainedIdentities.ToJsonString().ShouldBe(identities.ToJsonString());
    }
    private static readonly string[] OpenidScope = ["openid"];
}
