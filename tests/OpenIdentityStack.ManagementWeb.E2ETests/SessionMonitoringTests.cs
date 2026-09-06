using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

public sealed class SessionMonitoringTests(ManagementWebAppHostFixture fixture) : ManagementWebPageTest(fixture)
{
    [Fact]
    public async Task RetainedCheckSessionIframeObservesPersistedSessionRejection()
    {
        string opOrigin = Api.BaseAddress!.GetLeftPart(UriPartial.Authority);
        string rpOrigin = new Uri(Page.Url).GetLeftPart(UriPartial.Authority);
        // Use the actual session_state returned by authorization and retained by oidc-client-ts.
        string state = await Page.EvaluateAsync<string>("""
            () => {
                for (const key of Object.keys(sessionStorage)) {
                    if (key.startsWith('oidc.user:')) { return JSON.parse(sessionStorage.getItem(key)).session_state; }
                }
                return null;
            }
            """);
        state.ShouldNotBeNullOrEmpty();
        IFrame monitor = await AddMonitorAsync(Page, opOrigin);
        int pollRequests = 0;
        Page.Request += (_, request) =>
        {
            if (request.Url.EndsWith("/connect/check_session", StringComparison.OrdinalIgnoreCase)
                && request.Headers.ContainsKey("x-ois-session-poll"))
            {
                Interlocked.Increment(ref pollRequests);
            }
        };
        string[] burst = await CheckBurstAsync(Page, opOrigin, state, count: 20);
        burst.ShouldAllBe(result => result == "unchanged");
        pollRequests.ShouldBeLessThanOrEqualTo(1);
        string[] repeatedBurst = await CheckBurstAsync(Page, opOrigin, state, count: 20);
        repeatedBurst.ShouldAllBe(result => result == "unchanged");
        pollRequests.ShouldBeLessThanOrEqualTo(2);
        (await CheckAsync(Page, opOrigin, state, "other-client")).ShouldBe("changed");
        IPage otherOrigin = await Context.NewPageAsync();
        await otherOrigin.GotoAsync(opOrigin + "/Account/Login");
        new Uri(otherOrigin.Url).GetLeftPart(UriPartial.Authority).ShouldNotBe(rpOrigin);
        await AddMonitorAsync(otherOrigin, opOrigin);
        (await CheckAsync(otherOrigin, opOrigin, state)).ShouldBe("changed");
        await otherOrigin.CloseAsync();

        JsonNode users = await ApiGetAsync("/api/admin/users?search=admin%40test.com");
        string userId = users["items"]!.AsArray().Single(user => user!["email"]!.GetValue<string>() == ManagementWebAppHostFixture.AdminEmail)!["id"]!.GetValue<string>();
        (await Api.DeleteAsync($"/api/admin/users/{userId}/sessions")).StatusCode.ShouldBe(HttpStatusCode.OK);
        // Keep the original OP document and RP state. A standard postMessage poll must
        // revalidate server-linked state without the RP issuing an extra OP request.
        (await CheckAsync(Page, opOrigin, state)).ShouldBe("changed");
        (await Context.CookiesAsync(opOrigin)).ShouldNotContain(value => value.Name == "op_session");
    }

    private static async Task<IFrame> AddMonitorAsync(IPage page, string origin)
    {
        await page.EvaluateAsync("""
            origin => {
                const iframe = document.createElement('iframe');
                iframe.id = 'op-session-monitor';
                iframe.name = 'op-session-monitor';
                iframe.src = origin + '/connect/check_session';
                document.body.appendChild(iframe);
            }
            """, origin);
        await page.FrameLocator("#op-session-monitor").Locator("title").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        IFrame monitor = page.Frame("op-session-monitor")!;
        await monitor.WaitForLoadStateAsync(LoadState.Load);
        return monitor;
    }

    private static async Task<string> CheckAsync(IPage page, string origin, string state, string clientId = "management-web-client")
    {
        await page.EvaluateAsync("""
            ({ origin, state, clientId }) => {
                window.sessionMonitorResult = null;
                const monitor = document.getElementById('op-session-monitor').contentWindow;
                const receive = event => {
                    if (event.source === monitor && event.origin === origin &&
                        ['changed', 'unchanged', 'error'].includes(event.data)) {
                        window.removeEventListener('message', receive);
                        window.sessionMonitorResult = event.data;
                    }
                };
                window.addEventListener('message', receive);
                monitor.postMessage(clientId + ' ' + state, origin);
            }
            """, new { origin, state, clientId });
        await page.WaitForFunctionAsync("() => typeof window.sessionMonitorResult === 'string'");
        return await page.EvaluateAsync<string>("() => window.sessionMonitorResult");
    }

    private static async Task<string[]> CheckBurstAsync(IPage page, string origin, string state, int count)
    {
        await page.EvaluateAsync("""
            ({ origin, state, count }) => {
                window.sessionMonitorResults = [];
                const monitor = document.getElementById('op-session-monitor').contentWindow;
                const receive = event => {
                    if (event.source === monitor && event.origin === origin &&
                        ['changed', 'unchanged', 'error'].includes(event.data)) {
                        window.sessionMonitorResults.push(event.data);
                        if (window.sessionMonitorResults.length === count) {
                            window.removeEventListener('message', receive);
                        }
                    }
                };
                window.addEventListener('message', receive);
                for (let index = 0; index < count; index++) {
                    monitor.postMessage('management-web-client ' + state, origin);
                }
            }
            """, new { origin, state, count });
        await page.WaitForFunctionAsync("count => window.sessionMonitorResults?.length === count", count);
        return await page.EvaluateAsync<string[]>("() => window.sessionMonitorResults");
    }
}
