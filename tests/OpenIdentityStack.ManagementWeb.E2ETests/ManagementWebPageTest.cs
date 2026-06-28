using System.Text.Json;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>
/// Base class for ManagementWeb E2E page tests. Boots a Playwright page against the
/// Aspire-hosted app (authenticated via the mock auth provider) and provides helpers
/// to stub the admin API at the network layer so screens render deterministic data.
/// </summary>
public abstract class ManagementWebPageTest : IAsyncLifetime
{
    protected ManagementWebAppHostFixture Fixture { get; }
    protected IBrowserContext Context { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;

    protected ManagementWebPageTest(ManagementWebAppHostFixture fixture)
    {
        Fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        Context = await Fixture.CreateBrowserContextAsync();
        Page = await Context.NewPageAsync();
        Page.SetDefaultTimeout(60_000);
        Page.SetDefaultNavigationTimeout(60_000);
    }

    public async ValueTask DisposeAsync()
    {
        await Page.CloseAsync();
        await Context.CloseAsync();
    }

    protected string BaseUrl =>
        Fixture.ManagementWebUrl ?? throw new InvalidOperationException("ManagementWeb URL was not initialized.");

    protected Task GotoAsync(string path) => Page.GotoAsync(new Uri(new Uri(BaseUrl), path).ToString());

    protected static Task FulfillJsonAsync(IRoute route, object body) =>
        route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 200,
            ContentType = "application/json",
            Body = JsonSerializer.Serialize(body),
        });

    protected static Task NoContentAsync(IRoute route) => route.FulfillAsync(new RouteFulfillOptions { Status = 204 });

    protected static object Paged(params object[] items) =>
        new { items, totalCount = items.Length, page = 1, pageSize = 25, totalPages = 1 };
}
