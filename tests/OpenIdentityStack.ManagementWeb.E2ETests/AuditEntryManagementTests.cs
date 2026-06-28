using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>
/// E2E coverage for the ManagementWeb Audit list. Audit rows are produced as a side effect
/// of real admin actions, so the test performs seeding actions and reads the real trail.
/// </summary>
public sealed class AuditEntryManagementTests : ManagementWebPageTest
{
    public AuditEntryManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    protected override async Task SeedAsync()
    {
        // Create a user through the REAL API (not the in-process seeder) so a User-entity
        // audit entry is written and appears in the trail.
        await ApiPostAsync("/api/admin/users", new
        {
            email = $"audited.{Unique}@northwind.io",
            displayName = "Audited User",
            password = "Password123!@456",
        });
    }

    [Fact]
    public async Task OperatorCanBrowseAuditEntries()
    {
        await GotoAsync("/audit-entries");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Audit", Exact = true }).WaitForAsync();
        await Page.GetByLabel("Entity").WaitForAsync();

        // The real trail has entries (sign-in, seeding, …). At least one User-entity row shows.
        await Page.Locator("tbody tr").First.WaitForAsync();
        await Page.GetByText("User", new() { Exact = true }).First.WaitForAsync();
    }

    [Fact]
    public async Task EntityFilterDrivesTheAuditQuery()
    {
        await GotoAsync("/audit-entries");
        await Page.Locator("tbody tr").First.WaitForAsync();

        // Selecting an entity sends entityType=User and surfaces the applied-filter chip.
        await Page.GetByLabel("Entity").SelectOptionAsync("User");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear Entity", Exact = true }).WaitForAsync();
        await Page.GetByText("User", new() { Exact = true }).First.WaitForAsync();
    }
}
