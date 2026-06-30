using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>
/// E2E coverage for the ManagementWeb Audit list. Audit rows are produced as a side effect
/// of real admin actions, so the test performs an audited action and reads the real trail.
/// </summary>
public sealed class AuditEntryManagementTests : ManagementWebPageTest
{
    private static readonly string[] WebGrantTypes = ["authorization_code", "refresh_token"];
    private static readonly string[] WebScopes = ["openid", "profile"];
    private static readonly string[] WebRedirectUris = ["https://app.northwind.io/callback"];
    private static readonly string[] UsersReadPermission = ["users:read"];

    public AuditEntryManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    protected override async Task SeedAsync()
    {
        // Register an application through the REAL API. Application lifecycle commands write an
        // "Application"-entity audit entry, so the trail is guaranteed to contain a freshly-created
        // Application row for the assertions below.
        await ApiPostAsync("/api/admin/applications", new
        {
            clientId = $"audited-web-{Unique}-{Guid.NewGuid():N}",
            displayName = "Audited Application",
            description = (string?)null,
            profile = "Web",
            clientType = "Confidential",
            allowedGrantTypes = WebGrantTypes,
            allowedScopes = WebScopes,
            redirectUris = WebRedirectUris,
            postLogoutRedirectUris = Array.Empty<string>(),
            requirePkce = true,
            requireConsent = false,
        });

        // Create a user through the REAL API. User mutations are now audited ("User.Created"),
        // so the trail also contains a "User"-entity row attributed to the acting admin.
        JsonNode user = await ApiPostAsync("/api/admin/users", new
        {
            email = $"audited.{Unique}@northwind.io",
            displayName = "Audited User",
            password = "Password123!@456",
        });
        var userId = Guid.Parse(user["id"]!.GetValue<string>());

        // Assign a role through the REAL API. Role assignment is audited as "User.RoleAssigned"
        // (entity "User", attributed to the acting admin), so the trail carries that row too.
        JsonNode role = await ApiPostAsync("/api/admin/roles", new
        {
            name = $"audited-role-{Unique}",
            displayName = "Audited Role",
            description = "Role used to assert role-assignment auditing",
            permissions = UsersReadPermission,
            acknowledgeWildcardGrant = false,
        });
        var roleId = Guid.Parse(role["id"]!.GetValue<string>());

        HttpResponseMessage assign = await Api.PostAsync(
            $"/api/admin/users/{userId}/roles/{roleId}", content: null);
        assign.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task OperatorCanBrowseAuditEntries()
    {
        await GotoAsync("/audit-entries");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Audit", Exact = true }).WaitForAsync();
        await Page.GetByLabel("Entity").WaitForAsync();

        // The real trail has the Application row written by SeedAsync. Scope to the table body:
        // the entity-type filter renders a hidden <option>Application</option> that would otherwise
        // be the first (invisible) text match, and the badge is rendered uppercased.
        await Page.Locator("tbody tr").First.WaitForAsync();
        await Page.Locator("tbody").GetByText(new Regex("^Application$", RegexOptions.IgnoreCase)).First.WaitForAsync();
    }

    [Fact]
    public async Task EntityFilterDrivesTheAuditQuery()
    {
        await GotoAsync("/audit-entries");
        await Page.Locator("tbody tr").First.WaitForAsync();

        // Selecting an entity sends entityType=Application and surfaces the applied-filter chip.
        await Page.GetByLabel("Entity").SelectOptionAsync("Application");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear Entity", Exact = true }).WaitForAsync();
        // Scope to the table body so the hidden <option>Application</option> in the select isn't matched.
        await Page.Locator("tbody").GetByText(new Regex("^Application$", RegexOptions.IgnoreCase)).First.WaitForAsync();
    }

    [Fact]
    public async Task UserMutationsAreAuditedWithTheActingAdmin()
    {
        await GotoAsync("/audit-entries");
        await Page.Locator("tbody tr").First.WaitForAsync();

        // The user SeedAsync created is recorded as a "User.Created" action. Searching narrows the
        // trail to that row, which proves user mutations now reach the audit log end-to-end.
        await Page.GetByLabel("Search entries").FillAsync("User.Created");
        ILocator row = Page.Locator("tbody tr").Filter(new() { HasTextRegex = new Regex("User\\.Created") });
        await row.First.WaitForAsync();

        // The row is a User-entity action attributed to a real admin actor, not the "system" fallback.
        await row.First.GetByText(new Regex("^User$", RegexOptions.IgnoreCase)).WaitForAsync();
        await row.First.GetByText(new Regex("^system$", RegexOptions.IgnoreCase))
            .WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    [Fact]
    public async Task RoleAssignmentIsAuditedWithTheActingAdmin()
    {
        await GotoAsync("/audit-entries");
        await Page.Locator("tbody tr").First.WaitForAsync();

        // SeedAsync assigned a role through the real API; that writes a "User.RoleAssigned" row.
        // Searching narrows the trail to it, proving role-assignment auditing reaches the log.
        await Page.GetByLabel("Search entries").FillAsync("User.RoleAssigned");
        ILocator row = Page.Locator("tbody tr").Filter(new() { HasTextRegex = new Regex("User\\.RoleAssigned") });
        await row.First.WaitForAsync();

        // The row is a User-entity action attributed to a real admin actor, not the "system" fallback.
        await row.First.GetByText(new Regex("^User$", RegexOptions.IgnoreCase)).WaitForAsync();
        await row.First.GetByText(new Regex("^system$", RegexOptions.IgnoreCase))
            .WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    [Fact]
    public async Task SearchDrivesTheAuditQuery()
    {
        await GotoAsync("/audit-entries");
        await Page.Locator("tbody tr").First.WaitForAsync();

        // A matching search keeps the Application.Created row in view.
        await Page.GetByLabel("Search entries").FillAsync("Application.Created");
        await Page.Locator("tbody").GetByText(new Regex("Application\\.Created")).First.WaitForAsync();

        // A search that matches nothing collapses the table to the empty state.
        await Page.GetByLabel("Search entries").FillAsync($"no-such-entry-{Unique}");
        await Page.GetByText("No audit entries", new() { Exact = true }).WaitForAsync();
    }
}
