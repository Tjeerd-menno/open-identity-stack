using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>E2E coverage for the ManagementWeb Sessions list against the real admin API.</summary>
public sealed class SessionManagementTests : ManagementWebPageTest
{
    private string _primaryIp = "";
    private string _secondaryIp = "";

    public SessionManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    protected override async Task SeedAsync()
    {
        // Per-instance IP addresses (derived from the unique suffix) keep rows distinct on the shared list.
        int a = Convert.ToInt32(Unique[..2], 16);
        int b = Convert.ToInt32(Unique.Substring(2, 2), 16);
        _primaryIp = $"198.51.{a}.{b}";
        _secondaryIp = $"203.0.{a}.{b}";

        Guid userId = await Fixture.SeedUserAsync($"session.{Unique}@northwind.io", "Session User", "Password123!@456");
        await Fixture.SeedSessionAsync(userId, _primaryIp, "Mozilla/5.0 (Macintosh) Chrome/124.0 Safari/537.36");
        await Fixture.SeedSessionAsync(userId, _secondaryIp, "Mozilla/5.0 (Windows NT 10.0) Firefox/126.0");
    }

    [Fact]
    public async Task OperatorCanBrowseSessions()
    {
        await GotoAsync("/sessions");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Sessions", Exact = true }).WaitForAsync();
        await Page.GetByText(_primaryIp).WaitForAsync();
        await Page.GetByLabel("Status").WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanRevokeASession()
    {
        await GotoAsync("/sessions");
        await Page.GetByText(_primaryIp).WaitForAsync();

        ILocator row = Page.Locator("tbody tr", new() { Has = Page.GetByText(_primaryIp) }).First;
        await row.GetByRole(AriaRole.Button, new() { Name = "Row actions" }).ClickAsync();
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Revoke session", Exact = true }).ClickAsync();
        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Revoke", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Session revoked", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task StatusFilterDrivesTheSessionsQuery()
    {
        await GotoAsync("/sessions");
        await Page.GetByText(_primaryIp).WaitForAsync();

        // The seeded sessions are Active; filtering to Revoked removes them from the list.
        await Page.GetByLabel("Status").SelectOptionAsync("Revoked");

        await Page.GetByText(_primaryIp).WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear Status", Exact = true }).WaitForAsync();
    }

    [Fact]
    public async Task SearchDrivesTheSessionsQuery()
    {
        await GotoAsync("/sessions");
        await Page.GetByText(_primaryIp).WaitForAsync();

        // A search term that matches nothing exercises the query round-trip: the row drops out.
        await Page.GetByLabel("Search sessions").FillAsync($"no-such-session-{Unique}");
        await Page.GetByText(_primaryIp).WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }
}
