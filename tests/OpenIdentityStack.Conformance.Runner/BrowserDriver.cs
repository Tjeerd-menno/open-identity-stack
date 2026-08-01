using Microsoft.Playwright;

namespace OpenIdentityStack.Conformance.Runner;

/// <summary>
/// Performs the browser half of a conformance test: visits the URL the suite is
/// waiting on and drives whatever the OP presents.
/// </summary>
/// <remarks>
/// This exists because the suite's built-in scripted browser matches pages by
/// URL pattern, and no single pattern set covers the whole Basic OP plan. A test
/// showing a login form, a test reusing an existing session, and a negative test
/// that renders an error page all need different handling — with URL matching,
/// a config strict enough for the first breaks the other two.
///
/// Driving the browser externally removes the problem: rather than predicting
/// which page will appear, look at what actually rendered and respond. Each test
/// gets a fresh browser context, so no session leaks between tests.
/// </remarks>
internal sealed class BrowserDriver : IAsyncDisposable
{
    private readonly IPlaywright playwright;
    private readonly IBrowser browser;
    private readonly RunnerOptions options;
    private IBrowserContext? context;

    private BrowserDriver(IPlaywright playwright, IBrowser browser, RunnerOptions options)
    {
        this.playwright = playwright;
        this.browser = browser;
        this.options = options;
    }

    public static async Task<BrowserDriver> CreateAsync(RunnerOptions options)
    {
        IPlaywright playwright = await Playwright.CreateAsync();
        IBrowser browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = options.Headless,
        });

        return new BrowserDriver(playwright, browser, options);
    }

    public async ValueTask DisposeAsync()
    {
        if (this.context is not null)
        {
            await this.context.DisposeAsync();
        }

        await this.browser.DisposeAsync();
        this.playwright.Dispose();
    }

    /// <summary>
    /// Discards the current browser context so the next test starts without any
    /// OP session. Call between tests, never between visits of one test: tests
    /// like prompt-none-logged-in and max-age rely on the session from their
    /// first authorization still existing at their second.
    /// </summary>
    public async Task ResetSessionAsync()
    {
        if (this.context is not null)
        {
            await this.context.DisposeAsync();
            this.context = null;
        }
    }

    /// <summary>
    /// Visits one pending interaction URL and returns a short description of what happened.
    /// </summary>
    public async Task<string> VisitAsync(string url, CancellationToken ct)
    {
        // IgnoreHTTPSErrors covers the provider's self-signed certificate.
        this.context ??= await this.browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
        });

        IPage page = await this.context.NewPageAsync();

        try
        {
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = this.options.NavigationTimeoutMs,
            });

            bool loggedIn = await this.TryLoginAsync(page, ct);
            await this.TryConsentAsync(page);

            // The flow is done once the browser lands back on the suite, which
            // is the callback host. Negative tests never get there: the OP shows
            // an error page instead, which is the correct outcome and simply
            // means there is nothing further to drive.
            string landed = await WaitForSettleAsync(page, this.options.SuiteHost, this.options.SettleTimeoutMs);

            string note = $"login={(loggedIn ? "yes" : "not-required")} landed={landed} url={page.Url}";
            if (landed != "suite-callback")
            {
                string body = (await page.InnerTextAsync("body")).ReplaceLineEndings(" ");
                note += $" body=[{body[..Math.Min(body.Length, 300)]}]";
            }

            return note;
        }
        catch (TimeoutException)
        {
            return $"timeout at {page.Url}";
        }
        catch (PlaywrightException ex)
        {
            return $"browser-error: {ex.Message.Split('\n')[0]}";
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>Fills and submits the login form if one is present.</summary>
    private async Task<bool> TryLoginAsync(IPage page, CancellationToken ct)
    {
        ILocator email = page.Locator("#Email");
        if (await email.CountAsync() == 0)
        {
            // No form: either an existing session satisfied the request, or the
            // OP rejected it outright. Both are legitimate.
            return false;
        }

        // A submit that races page hydration posts empty fields, and the OP
        // re-renders the login form. Retry while the form is still there.
        for (int attempt = 0; attempt < 3 && await email.CountAsync() > 0; attempt++)
        {
            await email.FillAsync(this.options.Username);
            await page.Locator("#Password").FillAsync(this.options.Password);

            ILocator submit = page.Locator("form button[type=submit]");
            await submit.First.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions
            {
                Timeout = this.options.NavigationTimeoutMs,
            });

            ct.ThrowIfCancellationRequested();
        }

        return true;
    }

    /// <summary>Accepts a consent screen if the OP renders one.</summary>
    private async Task TryConsentAsync(IPage page)
    {
        // Clients seeded for certification use implicit consent, so this is
        // normally a no-op — but it costs nothing and makes the runner robust
        // to a client whose ConsentType is changed later.
        ILocator consent = page.Locator("form button[type=submit]");
        if (await consent.CountAsync() == 0 || await page.Locator("#Email").CountAsync() > 0)
        {
            return;
        }

        try
        {
            await consent.First.ClickAsync(new LocatorClickOptions { Timeout = 3000 });
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions
            {
                Timeout = this.options.NavigationTimeoutMs,
            });
        }
        catch (TimeoutException)
        {
            // Nothing to consent to.
        }
    }

    /// <summary>
    /// Waits until the browser reaches the suite's callback host, or until the
    /// page stops changing. Returns a short description of where it ended up.
    /// </summary>
    private static async Task<string> WaitForSettleAsync(IPage page, string suiteHost, int timeoutMs)
    {
        int waited = 0;
        const int Interval = 250;

        while (waited < timeoutMs)
        {
            if (page.Url.Contains(suiteHost, StringComparison.OrdinalIgnoreCase))
            {
                // The suite's callback page runs JavaScript that submits the
                // response parameters back to the suite. Closing the page
                // before that request fires leaves the test WAITING forever.
                try
                {
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
                    {
                        Timeout = timeoutMs,
                    });
                }
                catch (TimeoutException)
                {
                    // The page kept polling; the submission has long since fired.
                }

                return "suite-callback";
            }

            await Task.Delay(Interval);
            waited += Interval;
        }

        return page.Url.Contains("/connect/authorize", StringComparison.OrdinalIgnoreCase)
            ? "provider-error-or-stalled"
            : "provider-page";
    }
}
