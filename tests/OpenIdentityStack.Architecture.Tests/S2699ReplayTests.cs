using System.Text.RegularExpressions;

namespace OpenIdentityStack.Architecture.Tests;

/// <summary>
/// Replays one test Sonar S2699 rejected.
/// At 918be4aba673cde2ea06cc838e10a161b0e0cb47 (9120ea8bee3804b6c06850840323d75fae4b5569^, PR #427)
/// OverviewSurfacesLiveStatsAndRecentActivity only called WaitForAsync.
/// </summary>
public sealed partial class S2699ReplayTests
{
    private static readonly Regex Assertion = AssertionPattern();

    [Fact]
    public void OverviewSmokeTests_AssertTheOutcome()
    {
        string repoRoot = SourceText.RepositoryRoot();
        string path = Path.Combine(
            repoRoot,
            "tests",
            "OpenIdentityStack.ManagementWeb.E2ETests",
            "OverviewSmokeTests.cs");
        List<string> problems = MissingAssertions(SourceText.Relative(repoRoot, path), File.ReadAllText(path));

        problems.ShouldBeEmpty(
            "OverviewSurfacesLiveStatsAndRecentActivity must assert an outcome S2699 accepts."
            + Environment.NewLine
            + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void HistoricalOverviewSmokeTest_HasNoAssertionS2699Accepts()
    {
        string source = File.ReadAllText(Path.Combine(
            SourceText.RepositoryRoot(),
            "tests",
            "OpenIdentityStack.Architecture.Tests",
            "Fixtures",
            "historical-s2699-overview-smoke.cs"));
        List<string> problems = MissingAssertions(
            "tests/OpenIdentityStack.ManagementWeb.E2ETests/OverviewSmokeTests.cs",
            source);

        problems.ShouldContain(problem =>
            problem.Contains("OverviewSurfacesLiveStatsAndRecentActivity", StringComparison.Ordinal)
            && problem.Contains("S2699", StringComparison.Ordinal));
        problems.ShouldNotContain(problem =>
            problem.Contains("OperatorCanOpenOverviewAndNavigateViaTheSidebar", StringComparison.Ordinal));
    }

    internal static List<string> MissingAssertions(string relativePath, string source)
    {
        var problems = new List<string>();
        foreach (SourceText.TestMethod method in SourceText.TestMethods(source))
        {
            if (Assertion.IsMatch(method.Body))
            {
                continue;
            }

            problems.Add($"{relativePath}:{method.Line}: {method.Name} has no assertion S2699 accepts.");
        }

        return problems;
    }

    [GeneratedRegex(
        @"\bAssert\s*\.|\bShould\s*\.|\.Should[A-Z]|\bexpect\s*\(|\.toBeInTheDocument\s*\(|\.Received\s*\(",
        RegexOptions.CultureInvariant)]
    private static partial Regex AssertionPattern();
}
