using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>
/// Smoke coverage for the ManagementWeb Overview route and navigation shell, rendered
/// after a real OIDC sign-in against the live API.
/// </summary>
public sealed class OverviewSmokeTests : ManagementWebPageTest
{
    public OverviewSmokeTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task OperatorCanOpenOverviewAndNavigateViaTheSidebar()
    {
        // The base class already signed in and landed on Overview.
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Overview", Exact = true }).WaitForAsync();

        ILocator nav = Page.GetByRole(AriaRole.Navigation, new() { Name = "Management navigation", Exact = true });
        await nav.GetByRole(AriaRole.Link, new() { Name = "Users", Exact = true }).WaitForAsync();
        await nav.GetByRole(AriaRole.Link, new() { Name = "Applications", Exact = true }).WaitForAsync();
        await nav.GetByRole(AriaRole.Link, new() { Name = "Audit", Exact = true }).WaitForAsync();

        // Retired surfaces must not reappear.
        (await Page.GetByRole(AriaRole.Link, new() { Name = "Clients", Exact = true }).CountAsync()).ShouldBe(0);
        (await Page.GetByRole(AriaRole.Link, new() { Name = "Service Accounts", Exact = true }).CountAsync()).ShouldBe(0);

        await nav.GetByRole(AriaRole.Link, new() { Name = "Applications", Exact = true }).ClickAsync();
        await Page.WaitForURLAsync(new Regex(@"/applications/?$", RegexOptions.IgnoreCase));
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Applications", Exact = true }).WaitForAsync();
    }
}
