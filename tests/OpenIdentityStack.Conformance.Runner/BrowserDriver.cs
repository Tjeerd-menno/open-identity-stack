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

        // Evidence for visits that end somewhere unexpected: every network-level
        // failure and every error response, in order.
        var anomalies = new List<string>();
        page.RequestFailed += (_, request) =>
            anomalies.Add($"{request.Method} {Truncate(request.Url)} !{request.Failure}");
        page.Response += (_, response) =>
        {
            if (response.Status >= 400)
            {
                anomalies.Add($"{response.Request.Method} {Truncate(response.Url)} ={response.Status}");
            }
        };

        try
        {
            // A navigation that dies at the network level leaves Chromium's
            // error page: the target URL with an empty body. That is never a
            // real OP page (even OP error pages carry text), so restart the
            // whole flow from the interaction URL.
            for (int attempt = 1; ; attempt++)
            {
                // Reserve the rate-limited login slot BEFORE starting the flow:
                // several tests start a strict timer (e.g. 30s from the POST to
                // the authorization endpoint until the redirect must arrive),
                // and pausing mid-flow makes the suite give up. If no login
                // turns out to be needed, the slot is released below.
                await ThrottleLoginAsync(ct);

                await page.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = this.options.NavigationTimeoutMs,
                });

                bool loggedIn = await this.TryLoginAsync(page, ct);
                if (!loggedIn)
                {
                    ReleaseLoginSlot();
                }

                await this.TryConsentAsync(page);

                // The flow is done once the browser lands back on the suite,
                // which is the callback host. Negative tests never get there:
                // the OP shows an error page instead, which is the correct
                // outcome and simply means there is nothing further to drive.
                string landed = await WaitForSettleAsync(page, this.options.SuiteHost, this.options.SettleTimeoutMs);

                string note = $"login={(loggedIn ? "yes" : "not-required")} landed={landed} url={Redact(page.Url)}";
                if (landed == "suite-callback")
                {
                    return note;
                }

                string body = (await page.InnerTextAsync("body")).ReplaceLineEndings(" ");
                if (body.Length > 0 || attempt >= 3)
                {
                    return $"{note} body=[{body[..Math.Min(body.Length, 300)]}] anomalies=[{string.Join("; ", anomalies)}]";
                }
            }
        }
        catch (TimeoutException)
        {
            return $"timeout at {Redact(page.Url)}";
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

    /// <summary>Scheme and authority of <paramref name="url"/>, or the input if unparseable.</summary>
    private static string OriginOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed)
            ? parsed.GetLeftPart(UriPartial.Authority)
            : url;

    private bool IsOpOrigin(string url) =>
        !string.IsNullOrEmpty(this.options.OpOrigin)
        && string.Equals(OriginOf(url), this.options.OpOrigin, StringComparison.OrdinalIgnoreCase);

    private static readonly List<DateTime> LoginSubmits = [];

    /// <summary>
    /// Total time spent parked on the login rate limiter. The caller adds this to
    /// its per-test deadline: a throttle wait can exceed the whole deadline, and
    /// a test abandoned mid-flow keeps the alias and corrupts what follows.
    /// </summary>
    public static TimeSpan ThrottleWait { get; private set; }

    /// <summary>
    /// Delays until submitting a login form stays within the OP's fixed window
    /// of 5 interactive logins per 5 minutes per client IP, then reserves a
    /// slot. A slightly longer window is used because the OP's window boundary
    /// is not observable.
    /// </summary>
    private static async Task ThrottleLoginAsync(CancellationToken ct)
    {
        var window = TimeSpan.FromMinutes(5.5);
        const int Limit = 5;

        LoginSubmits.RemoveAll(t => DateTime.UtcNow - t > window);

        if (LoginSubmits.Count >= Limit)
        {
            TimeSpan wait = window - (DateTime.UtcNow - LoginSubmits[0]);
            Console.WriteLine($"        (login rate limit: waiting {wait.TotalSeconds:F0}s)");
            await Task.Delay(wait, ct);
            ThrottleWait += wait;
            LoginSubmits.RemoveAt(0);
        }

        LoginSubmits.Add(DateTime.UtcNow);
    }

    /// <summary>Returns an unused reservation made by <see cref="ThrottleLoginAsync"/>.</summary>
    private static void ReleaseLoginSlot()
    {
        if (LoginSubmits.Count > 0)
        {
            LoginSubmits.RemoveAt(LoginSubmits.Count - 1);
        }
    }

    private static string Truncate(string url)
    {
        int query = url.IndexOf('?', StringComparison.Ordinal);
        return query < 0 ? url : url[..query];
    }

    /// <summary>
    /// Query parameters whose values must never reach the results file. A
    /// successful authorization lands on the suite callback carrying a live
    /// <c>code</c>, and notes are serialized to disk verbatim.
    /// </summary>
    private static readonly IReadOnlySet<string> SecretParameters =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "code",
            "id_token",
            "access_token",
            "refresh_token",
            "token",
            "code_verifier",
            "client_secret",
        };

    /// <summary>
    /// Replaces the values of credential-bearing query parameters with a marker,
    /// keeping parameter names and the diagnostic ones (<c>error</c>,
    /// <c>state</c>, …) intact — those are what makes a note worth reading.
    /// </summary>
    internal static string Redact(string url)
    {
        int split = url.IndexOf('?', StringComparison.Ordinal);
        if (split < 0)
        {
            return url;
        }

        string[] pairs = url[(split + 1)..].Split('&');
        for (int i = 0; i < pairs.Length; i++)
        {
            int eq = pairs[i].IndexOf('=', StringComparison.Ordinal);
            string name = eq < 0 ? pairs[i] : pairs[i][..eq];

            if (eq >= 0 && SecretParameters.Contains(Uri.UnescapeDataString(name)))
            {
                pairs[i] = $"{name}=[redacted]";
            }
        }

        return $"{url[..split]}?{string.Join('&', pairs)}";
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

        // The suite chooses the URL this flow started from, and certificate
        // validation is off, so a login form is not by itself proof of who is
        // asking. Type the credential only into the OP named by the plan config.
        if (!this.IsOpOrigin(page.Url))
        {
            throw new InvalidOperationException(
                $"Refusing to enter credentials: a login form was served from '{OriginOf(page.Url)}', "
                + $"but the OP under test is '{this.options.OpOrigin}'.");
        }

        // A failed submit re-renders the login form; retry while it is there.
        for (int attempt = 0; attempt < 3 && await email.CountAsync() > 0; attempt++)
        {
            if (attempt > 0)
            {
                // The first submit rides the slot reserved before the visit
                // started; a retry is a further rate-limited POST.
                await ThrottleLoginAsync(ct);
            }

            await email.FillAsync(this.options.Username);
            await page.Locator("#Password").FillAsync(this.options.Password);

            ILocator submit = page.Locator("form button[type=submit]");
            await submit.First.ClickAsync();

            // WaitForLoadState(DOMContentLoaded) is useless after a click: the
            // current document already satisfies it, so it returns before the
            // form POST navigates — and the retry then double-submits into the
            // half-navigated page. Wait until the URL leaves the login page.
            try
            {
                await page.WaitForURLAsync(
                    url => !url.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase),
                    new PageWaitForURLOptions { Timeout = this.options.NavigationTimeoutMs });
            }
            catch (TimeoutException)
            {
                // Still on the login page: a genuine re-render. Let the loop retry.
            }

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
