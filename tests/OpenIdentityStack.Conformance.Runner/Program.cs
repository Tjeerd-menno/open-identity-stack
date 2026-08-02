using System.Text.Json;
using OpenIdentityStack.Conformance.Runner;

// Drives an OIDF conformance plan end to end against a self-hosted suite,
// performing the browser half with Playwright. See
// docs/certification/run-oidf-conformance-suite.md.

var options = RunnerOptions.FromArgs(args);

Console.WriteLine($"suite  : {options.SuiteBaseUrl}");
Console.WriteLine($"plan   : {options.PlanName}");
Console.WriteLine($"config : {options.ConfigPath}");

string configJson = await File.ReadAllTextAsync(options.ConfigPath);

using var suite = new SuiteClient(new Uri(options.SuiteBaseUrl));
await using BrowserDriver browser = await BrowserDriver.CreateAsync(options);

string planId = await suite.CreatePlanAsync(options.PlanName, options.VariantJson, configJson, CancellationToken.None);
Console.WriteLine($"planId : {planId}");

IReadOnlyList<string> modules = await suite.GetPlanModulesAsync(planId, CancellationToken.None);
if (options.Only.Count > 0)
{
    modules = modules.Where(options.Only.Contains).ToList();
}

Console.WriteLine($"modules: {modules.Count}");
Console.WriteLine();

var results = new List<TestResult>();

for (int i = 0; i < modules.Count; i++)
{
    string module = modules[i];
    string prefix = $"[{i + 1}/{modules.Count}] {module}";

    // Each test starts without an OP session; visits within one test share one.
    await browser.ResetSessionAsync();

    string testId;
    try
    {
        testId = await suite.StartTestAsync(module, planId, CancellationToken.None);
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"{prefix}: START-ERROR {ex.Message}");
        results.Add(new TestResult(module, null, "START-ERROR", ex.Message, []));
        continue;
    }

    var visited = new HashSet<string>(StringComparer.Ordinal);
    var notes = new List<string>();
    TestInfo info = new(null, null, []);

    DateTime deadline = DateTime.UtcNow.AddSeconds(options.PerTestTimeoutSeconds);

    while (DateTime.UtcNow < deadline)
    {
        info = await suite.GetTestInfoAsync(testId, CancellationToken.None);

        if (info.IsTerminal)
        {
            break;
        }

        // Any URL the suite is waiting on that has not been driven yet.
        foreach (string url in info.BrowserUrls.Where(u => visited.Add(u)))
        {
            string outcome = await browser.VisitAsync(url, CancellationToken.None);
            notes.Add(outcome);
            Console.WriteLine($"{prefix}: browser -> {outcome}");
        }

        await Task.Delay(1000);
    }

    // A test still holding the alias when the next one starts is killed by the
    // suite with an "alias conflict", which silently corrupts every later
    // result. Report the stall loudly instead of moving on quietly.
    string status = info.IsTerminal ? info.Status! : $"STALLED({info.Status})";
    results.Add(new TestResult(module, testId, status, info.Result, notes));
    Console.WriteLine($"{prefix}: {status} {info.Result}");
}

string json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
await File.WriteAllTextAsync(options.OutputPath, json);

Console.WriteLine();
Console.WriteLine("=== SUMMARY ===");
foreach (IGrouping<string, TestResult> group in results
    .GroupBy(r => r.Result ?? r.Status ?? "UNKNOWN")
    .OrderBy(g => g.Key, StringComparer.Ordinal))
{
    Console.WriteLine($"  {group.Key}: {group.Count()}");
}

Console.WriteLine($"\nresults written to {options.OutputPath}");

// A stalled test means the run is untrustworthy, not merely imperfect.
int stalled = results.Count(r => r.Status?.StartsWith("STALLED", StringComparison.Ordinal) == true);
if (stalled > 0)
{
    Console.WriteLine($"WARNING: {stalled} test(s) stalled — later results may be corrupted by alias conflicts.");
    return 2;
}

return results.Any(r => r.Result == "FAILED") ? 1 : 0;

internal sealed record TestResult(
    string Module,
    string? TestId,
    string? Status,
    string? Result,
    IReadOnlyList<string> BrowserNotes);
