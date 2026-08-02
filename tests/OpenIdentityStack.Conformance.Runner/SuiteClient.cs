using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

namespace OpenIdentityStack.Conformance.Runner;

/// <summary>
/// Minimal client for the OpenID Foundation conformance suite REST API.
/// </summary>
/// <remarks>
/// The self-hosted suite runs with a self-signed certificate and, under the
/// <c>dev</c> Spring profile, no authentication at all — so no credentials are
/// handled here and certificate validation is deliberately bypassed. Never point
/// this at the hosted suite at certification.openid.net.
/// </remarks>
internal sealed class SuiteClient : IDisposable
{
    private readonly HttpClient http;

    public SuiteClient(Uri baseAddress)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };

        this.http = new HttpClient(handler)
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(90),
        };
    }

    public void Dispose() => this.http.Dispose();

    /// <summary>Creates a test plan and returns its identifier.</summary>
    public async Task<string> CreatePlanAsync(string planName, string variantJson, string configJson, CancellationToken ct)
    {
        string url = $"/api/plan?planName={Uri.EscapeDataString(planName)}&variant={Uri.EscapeDataString(variantJson)}";
        using var content = new StringContent(configJson, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await this.http.PostAsync(url, content, ct);
        response.EnsureSuccessStatusCode();

        JsonNode node = await ReadJsonAsync(response, ct);
        return node["id"]!.GetValue<string>();
    }

    /// <summary>Returns the module names a plan contains, in plan order.</summary>
    public async Task<IReadOnlyList<string>> GetPlanModulesAsync(string planId, CancellationToken ct)
    {
        JsonNode node = await this.GetJsonAsync($"/api/plan/{planId}", ct);
        return node["modules"]!.AsArray()
            .Select(m => m!["testModule"]!.GetValue<string>())
            .ToList();
    }

    /// <summary>Starts a single test module within a plan and returns the test instance id.</summary>
    public async Task<string> StartTestAsync(string module, string planId, CancellationToken ct)
    {
        string url = $"/api/runner?test={Uri.EscapeDataString(module)}&plan={Uri.EscapeDataString(planId)}";
        using HttpResponseMessage response = await this.http.PostAsync(url, content: null, ct);
        response.EnsureSuccessStatusCode();

        JsonNode node = await ReadJsonAsync(response, ct);
        return node["id"]!.GetValue<string>();
    }

    public async Task<TestInfo> GetTestInfoAsync(string testId, CancellationToken ct)
    {
        JsonNode node = await this.GetJsonAsync($"/api/info/{testId}", ct);

        var urls = new List<string>();

        // Suite 5.x serves pending interaction URLs from the runner resource,
        // not from /api/info. Keep reading /api/info's `browser` too, so an
        // older suite still works. The runner resource disappears once the
        // test finishes; treat 404 as "no pending work".
        JsonNode? browser = node["browser"];
        if (browser is null)
        {
            using HttpResponseMessage runnerResponse = await this.http.GetAsync($"/api/runner/{testId}", ct);
            if (runnerResponse.IsSuccessStatusCode)
            {
                JsonNode runnerNode = await ReadJsonAsync(runnerResponse, ct);
                browser = runnerNode["browser"];
            }
        }

        if (browser is not null)
        {
            // The suite exposes pending interactions as `urls`; older/newer
            // builds also carry `urlsWithMethod`. Read both and de-duplicate,
            // so a schema difference degrades to "no work" rather than a crash.
            if (browser["urls"] is JsonArray plain)
            {
                urls.AddRange(plain.Where(u => u is not null).Select(u => u!.GetValue<string>()));
            }

            if (browser["urlsWithMethod"] is JsonArray withMethod)
            {
                urls.AddRange(withMethod
                    .Where(u => u?["url"] is not null)
                    .Select(u => u!["url"]!.GetValue<string>()));
            }
        }

        return new TestInfo(
            Status: node["status"]?.GetValue<string>(),
            Result: node["result"]?.GetValue<string>(),
            BrowserUrls: urls.Distinct().ToList());
    }

    private async Task<JsonNode> GetJsonAsync(string url, CancellationToken ct)
    {
        using HttpResponseMessage response = await this.http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync(response, ct);
    }

    private static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        string body = await response.Content.ReadAsStringAsync(ct);
        return JsonNode.Parse(body)
            ?? throw new InvalidOperationException($"Conformance suite returned a non-JSON body: {body}");
    }
}

internal sealed record TestInfo(string? Status, string? Result, IReadOnlyList<string> BrowserUrls)
{
    /// <summary>True once the suite will publish no further state for this test.</summary>
    public bool IsTerminal => this.Status is "FINISHED" or "INTERRUPTED";
}
