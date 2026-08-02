using System.Text.Json;
using OpenIdentityStack.Conformance.Runner;

// Drives an OIDF conformance plan end to end against a self-hosted suite,
// performing the browser half with Playwright. See
// docs/certification/run-oidf-conformance-suite.md.

var options = RunnerOptions.FromArgs(args);

string configJson = await File.ReadAllTextAsync(options.ConfigPath);

// The OP the plan config points at is the only origin the browser may type the
// certification password into. Deriving it from the config rather than a flag
// keeps it impossible to run against one OP while trusting another.
string discoveryUrl = JsonDocument.Parse(configJson)
    .RootElement.GetProperty("server").GetProperty("discoveryUrl").GetString()
    ?? throw new InvalidOperationException($"{options.ConfigPath} has no server.discoveryUrl.");

options = options with
{
    OpOrigin = new Uri(discoveryUrl, UriKind.Absolute).GetLeftPart(UriPartial.Authority),
};

Console.WriteLine($"suite  : {options.SuiteBaseUrl}");
Console.WriteLine($"plan   : {options.PlanName}");
Console.WriteLine($"config : {options.ConfigPath}");
Console.WriteLine($"op     : {options.OpOrigin}");

using var suite = new SuiteClient(new Uri(options.SuiteBaseUrl));
await using BrowserDriver browser = await BrowserDriver.CreateAsync(options);

string planId = await suite.CreatePlanAsync(options.PlanName, options.VariantJson, configJson, CancellationToken.None);
Console.WriteLine($"planId : {planId}");

IReadOnlyList<string> modules = await suite.GetPlanModulesAsync(planId, CancellationToken.None);
if (options.Only.Count > 0)
{
    // A mistyped name would otherwise be discarded in silence: the run would
    // report success having produced no evidence for the module the operator
    // actually asked about.
    string[] unknown = options.Only.Except(modules, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    if (unknown.Length > 0)
    {
        throw new InvalidOperationException(
            $"--only names {unknown.Length} module(s) that plan '{options.PlanName}' does not contain: "
            + string.Join(", ", unknown));
    }

    // Naming a module explicitly is an operator decision; it overrides the
    // manual-review exclusion below.
    modules = modules.Where(options.Only.Contains).ToList();
}
else if (!options.IncludeManualReview)
{
    string[] excluded = modules.Where(RunnerOptions.ManualReviewModules.Contains).ToArray();
    if (excluded.Length > 0)
    {
        modules = modules.Except(excluded, StringComparer.Ordinal).ToList();
        Console.WriteLine(
            $"skipping {excluded.Length} manual-review module(s); they pause for an operator "
            + "and would stall the sweep. Drive them individually with --only, or pass "
            + "--include-manual-review. See docs/certification/run-oidf-conformance-suite.md.");
        foreach (string module in excluded)
        {
            Console.WriteLine($"  - {module}");
        }
    }
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
            // A visit can park for minutes on the login rate limiter, which is
            // dead time rather than a slow test. Charging it to the deadline
            // would abandon a flow that is about to succeed.
            TimeSpan throttledBefore = BrowserDriver.ThrottleWait;
            string outcome = await browser.VisitAsync(url, CancellationToken.None);
            deadline += BrowserDriver.ThrottleWait - throttledBefore;

            notes.Add(outcome);
            Console.WriteLine($"{prefix}: browser -> {outcome}");
        }

        await Task.Delay(1000);
    }

    // The loop can exit on the deadline moments after the browser finished, so
    // read the state once more before calling it stalled.
    if (!info.IsTerminal)
    {
        info = await suite.GetTestInfoAsync(testId, CancellationToken.None);
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

// Anything that did not reach a verdict is missing evidence, not a pass. A test
// that never started, or that the suite terminated as INTERRUPTED, has no
// `result` at all — reporting success there invites an operator to accept a run
// with holes in it.
TestResult[] incomplete = results
    .Where(r => r.Status == "START-ERROR" || r.Status == "INTERRUPTED" || r.Result is null)
    .ToArray();

if (incomplete.Length > 0)
{
    Console.WriteLine($"INCOMPLETE: {incomplete.Length} module(s) produced no result.");
    foreach (TestResult result in incomplete)
    {
        Console.WriteLine($"  - {result.Module}: {result.Status}");
    }

    return 3;
}

return results.Any(r => r.Result == "FAILED") ? 1 : 0;

internal sealed record TestResult(
    string Module,
    string? TestId,
    string? Status,
    string? Result,
    IReadOnlyList<string> BrowserNotes);
