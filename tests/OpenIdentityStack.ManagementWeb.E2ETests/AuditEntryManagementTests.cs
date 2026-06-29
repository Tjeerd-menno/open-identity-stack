using System.Text.RegularExpressions;
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

    public AuditEntryManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    protected override async Task SeedAsync()
    {
        // Register an application through the REAL API. Application lifecycle commands write an
        // "Application"-entity audit entry (unlike user CRUD, which is not audited), so the trail
        // is guaranteed to contain a freshly-created Application row for the assertions below.
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
}
