using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>
/// Base class for ManagementWeb E2E page tests. Boots a Playwright page against the
/// Aspire-hosted app, signs in through the REAL OIDC login flow as the seeded platform
/// admin, and exposes an admin-authenticated <see cref="HttpClient"/> for seeding domain
/// data through the REAL admin API. These tests never stub the network — every screen
/// renders data produced by the real API on the ephemeral database.
/// </summary>
public abstract class ManagementWebPageTest : IAsyncLifetime
{
    protected ManagementWebAppHostFixture Fixture { get; }
    protected IBrowserContext Context { get; private set; } = null!;
    protected IPage Page { get; private set; } = null!;

    /// <summary>Admin-authenticated HTTP client for seeding data via the real /api/admin endpoints.</summary>
    protected HttpClient Api { get; private set; } = null!;

    /// <summary>Per-test unique suffix, used to keep seeded entity names distinct on the shared database.</summary>
    protected string Unique { get; } = Guid.NewGuid().ToString("N")[..8];

    private readonly List<string> _apiResponses = new();

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
        Page.Response += (_, response) =>
        {
            if (response.Url.Contains("/api/admin/", StringComparison.OrdinalIgnoreCase))
            {
                _apiResponses.Add($"{response.Status} {response.Request.Method} {new Uri(response.Url).AbsolutePath}");
            }
        };

        // Sign in through the real OIDC flow, then seed via the admin user's own token so the
        // actor is a real user (required by surfaces like application-permission registration).
        await Fixture.SignInAsync(Page);
        Api = await Fixture.CreateUserApiClientAsync(Page);
        await SeedAsync();
    }

    /// <summary>
    /// Override to seed data via <see cref="Api"/> (the real admin API) before sign-in.
    /// Runs once per test, against the shared ephemeral database.
    /// </summary>
    protected virtual Task SeedAsync() => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        Api?.Dispose();
        await Page.CloseAsync();
        await Context.CloseAsync();
    }

    protected string BaseUrl =>
        Fixture.ManagementWebUrl ?? throw new InvalidOperationException("ManagementWeb URL was not initialized.");

    protected Task GotoAsync(string path) => Page.GotoAsync(new Uri(new Uri(BaseUrl), path).ToString());

    /// <summary>Waits for text matching <paramref name="pattern"/>; on timeout dumps page text to aid diagnosis.</summary>
    protected async Task ExpectTextAsync(string pattern)
    {
        try
        {
            await Page.GetByText(new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                .First.WaitForAsync(new LocatorWaitForOptions { Timeout = 30_000 });
        }
        catch (TimeoutException)
        {
            string shot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"oisfail-{Unique}.png");
            await Page.ScreenshotAsync(new PageScreenshotOptions { Path = shot, FullPage = true });
            int menuItems = await Page.GetByRole(AriaRole.Menuitem).CountAsync();
            string body = await Page.InnerTextAsync("body");
            string snippet = body.Length > 2500 ? body[..2500] : body;
            string apiLog = string.Join("\n  ", _apiResponses.TakeLast(15));
            throw new InvalidOperationException($"Expected text /{pattern}/ not visible. openMenuItems={menuItems} screenshot={shot}\n--- last /api/admin responses ---\n  {apiLog}\n--- body innerText ---\n{snippet}");
        }
    }

    /// <summary>POSTs JSON to the real admin API as the seeding admin and returns the parsed body.</summary>
    protected async Task<JsonNode> ApiPostAsync(string path, object body)
    {
        HttpResponseMessage response = await Api.PostAsJsonAsync(path, body);
        await EnsureSuccessAsync(response, "POST", path);
        return await response.Content.ReadFromJsonAsync<JsonNode>() ?? new JsonObject();
    }

    /// <summary>GETs JSON from the real admin API as the seeding admin and returns the parsed body.</summary>
    protected async Task<JsonNode> ApiGetAsync(string path)
    {
        HttpResponseMessage response = await Api.GetAsync(path);
        await EnsureSuccessAsync(response, "GET", path);
        return await response.Content.ReadFromJsonAsync<JsonNode>() ?? new JsonObject();
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string method, string path)
    {
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"{method} {path} failed: {(int)response.StatusCode} {response.StatusCode} - {body}");
        }
    }
}
