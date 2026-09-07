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
        ILocator statusFilter = Page.GetByLabel("Status");
        await statusFilter.WaitForAsync();
        (await statusFilter.IsVisibleAsync()).ShouldBeTrue();
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

        ILocator toast = Page.GetByText(new Regex("Session revoked", RegexOptions.IgnoreCase));
        await toast.WaitForAsync();
        (await toast.IsVisibleAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task StatusFilterDrivesTheSessionsQuery()
    {
        await GotoAsync("/sessions");
        await Page.GetByText(_primaryIp).WaitForAsync();

        // The seeded sessions are Active; filtering to Revoked removes them from the list.
        await Page.GetByLabel("Status").SelectOptionAsync("Revoked");

        ILocator primaryRow = Page.GetByText(_primaryIp);
        await primaryRow.WaitForAsync(new() { State = WaitForSelectorState.Detached });
        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear Status", Exact = true }).WaitForAsync();
        (await primaryRow.IsVisibleAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task SearchDrivesTheSessionsQuery()
    {
        await GotoAsync("/sessions");
        await Page.GetByText(_primaryIp).WaitForAsync();

        // Wait for the concrete search response so temporary loading rows cannot satisfy the assertion.
        IResponse matchingResponse = await Page.RunAndWaitForResponseAsync(
            () => Page.GetByLabel("Search sessions").FillAsync(_primaryIp),
            candidate => candidate.Request.Method == "GET"
                && new Uri(candidate.Url).AbsolutePath == "/api/admin/sessions"
                && System.Web.HttpUtility.ParseQueryString(new Uri(candidate.Url).Query)["search"] == _primaryIp);
        matchingResponse.Status.ShouldBe(200);
        System.Text.Json.Nodes.JsonNode matchingPayload = System.Text.Json.Nodes.JsonNode.Parse(await matchingResponse.TextAsync())!;
        matchingPayload["items"]!.AsArray().Count.ShouldBe(1);
        matchingPayload["items"]![0]!["ipAddress"]!.GetValue<string>().ShouldBe(_primaryIp);
        matchingPayload["totalCount"]!.GetValue<int>().ShouldBe(1);
        await Assertions.Expect(Page.GetByText(_primaryIp)).ToBeVisibleAsync();
        await Assertions.Expect(Page.GetByText(_secondaryIp)).ToHaveCountAsync(0);

        string search = $"no-such-session-{Unique}";
        IResponse response = await Page.RunAndWaitForResponseAsync(
            () => Page.GetByLabel("Search sessions").FillAsync(search),
            candidate => candidate.Request.Method == "GET"
                && new Uri(candidate.Url).AbsolutePath == "/api/admin/sessions"
                && System.Web.HttpUtility.ParseQueryString(new Uri(candidate.Url).Query)["search"] == search);
        response.Status.ShouldBe(200);
        System.Text.Json.Nodes.JsonNode payload = System.Text.Json.Nodes.JsonNode.Parse(await response.TextAsync())!;
        payload["items"]!.AsArray().Count.ShouldBe(0);
        payload["totalCount"]!.GetValue<int>().ShouldBe(0);
        await Assertions.Expect(Page.GetByText("No sessions", new() { Exact = true })).ToBeVisibleAsync();
        ILocator primaryRow = Page.GetByText(_primaryIp);
        await Assertions.Expect(primaryRow).ToHaveCountAsync(0);
    }
}
