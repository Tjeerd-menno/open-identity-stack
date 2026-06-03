using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>
/// Smoke coverage for the ManagementWeb Overview route and navigation surface.
/// </summary>
public sealed class OverviewSmokeTests : IAsyncLifetime
{
    private readonly ManagementWebAppHostFixture fixture;
    private IBrowserContext? context;
    private IPage? page;

    public OverviewSmokeTests(ManagementWebAppHostFixture fixture)
    {
        this.fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        context = await fixture.CreateBrowserContextAsync();
        page = await context.NewPageAsync();
        page.SetDefaultTimeout(60_000);
        page.SetDefaultNavigationTimeout(60_000);
    }

    public async ValueTask DisposeAsync()
    {
        if (page is not null) await page.CloseAsync();
        if (context is not null) await context.CloseAsync();
    }

    [Fact]
    public async Task OperatorCanOpenOverviewAndNavigateToRetainedSections()
    {
        string baseUrl = fixture.ManagementWebUrl ?? throw new InvalidOperationException("ManagementWeb URL was not initialized.");

        await page!.GotoAsync(new Uri(new Uri(baseUrl), "/").ToString());
        await page.GetByRole(AriaRole.Heading, new() { Name = "Overview", Exact = true }).WaitForAsync();

        ILocator quickLinks = page.GetByRole(AriaRole.Navigation, new() { NameRegex = new Regex("overview quick links", RegexOptions.IgnoreCase) });
        await quickLinks.GetByRole(AriaRole.Link, new() { NameRegex = new Regex("Applications", RegexOptions.IgnoreCase) }).WaitForAsync();
        await quickLinks.GetByRole(AriaRole.Link, new() { NameRegex = new Regex("Audit", RegexOptions.IgnoreCase) }).WaitForAsync();

        await quickLinks.GetByRole(AriaRole.Link, new() { NameRegex = new Regex("Applications", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/applications/?$", RegexOptions.IgnoreCase));

        await page.GetByRole(AriaRole.Link, new() { Name = "Clients", Exact = true }).CountAsync().ContinueWith(task => task.Result.ShouldBe(0));
        await page.GetByRole(AriaRole.Link, new() { Name = "Service Accounts", Exact = true }).CountAsync().ContinueWith(task => task.Result.ShouldBe(0));
    }
}
