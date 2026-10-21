using System.Text.RegularExpressions;

namespace OpenIdentityStack.Architecture.Tests;

/// <summary>
/// Logout token tests have to assert a signature.
/// At 0943f58a7e59814996feee5a73ac224ef8ef9a7e (526cae99613eca0c9dee0ca2f2f3045b5327c49f^, PR #398)
/// BackChannelLogoutNotifier assembled an alg:none JWT, and its tests checked counts and URLs.
/// </summary>
public sealed partial class LogoutTokenSignatureTests
{
    private static readonly Regex Producer = ProducerPattern();

    private static readonly string[] SignatureTokens = [".Alg", "ValidateToken", "IssuerSigningKey", "SecurityAlgorithms"];

    [Fact]
    public void LogoutTokenTests_AssertASignature()
    {
        string repoRoot = SourceText.RepositoryRoot();
        List<string> problems = FindProblems(ReadProduction(repoRoot), ReadTests(repoRoot));

        problems.ShouldBeEmpty(
            "LogoutTokenFactory tests must assert the signature."
            + Environment.NewLine
            + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void HistoricalLogoutTokenTests_DoNotAssertASignature()
    {
        string fixtures = Path.Combine(
            SourceText.RepositoryRoot(),
            "tests",
            "OpenIdentityStack.Architecture.Tests",
            "Fixtures");
        List<string> problems = FindProblems(
            [
                (
                    "src/OpenIdentityStack.Infrastructure/Identity/BackChannelLogoutNotifier.cs",
                    File.ReadAllText(Path.Combine(fixtures, "historical-unsigned-logout-token.cs"))),
            ],
            [
                (
                    "tests/OpenIdentityStack.Infrastructure.Tests/Identity/BackChannelLogoutNotifierTests.cs",
                    File.ReadAllText(Path.Combine(fixtures, "historical-logout-notifier-tests.cs"))),
            ]);

        problems.ShouldNotBeEmpty();
        string text = string.Join(Environment.NewLine, problems);
        text.ShouldContain("LogoutTokenFactory");
        text.ShouldContain("do not assert a signature");
        text.ShouldContain("BackChannelLogoutNotifier.cs");
    }

    internal static List<string> FindProblems(
        IReadOnlyList<(string Path, string Source)> production,
        IReadOnlyList<(string Path, string Source)> tests)
    {
        if (TestsAssertSignature(tests))
        {
            return [];
        }

        var problems = new List<string>();
        foreach ((string path, string source) in production)
        {
            string stripped = SourceText.StripComments(source);
            foreach (Match match in Producer.Matches(stripped))
            {
                int line = SourceText.LineAt(source, match.Index);
                problems.Add(
                    $"{path}:{line}: logout token tests do not assert a signature. LogoutTokenFactory tests must assert Alg, ValidateToken, or IssuerSigningKey.");
            }
        }

        problems.Sort(StringComparer.Ordinal);
        return problems;
    }

    private static bool TestsAssertSignature(IReadOnlyList<(string Path, string Source)> tests)
    {
        foreach ((string _, string source) in tests)
        {
            if (!source.Contains("LogoutTokenFactory", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (SourceText.TestMethod method in SourceText.TestMethods(source))
            {
                if (!method.Body.Contains("LogoutTokenFactory", StringComparison.Ordinal))
                {
                    continue;
                }

                if (SignatureTokens.Any(token => method.Body.Contains(token, StringComparison.Ordinal)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<(string Path, string Source)> ReadProduction(string repoRoot)
    {
        var files = new List<(string Path, string Source)>();
        foreach (string file in Directory.EnumerateFiles(Path.Combine(repoRoot, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (SourceText.IsUnderSkippedDirectory(repoRoot, file))
            {
                continue;
            }

            files.Add((SourceText.Relative(repoRoot, file), File.ReadAllText(file)));
        }

        return files;
    }

    private static List<(string Path, string Source)> ReadTests(string repoRoot)
    {
        var files = new List<(string Path, string Source)>();
        foreach (string file in Directory.EnumerateFiles(Path.Combine(repoRoot, "tests"), "*.cs", SearchOption.AllDirectories))
        {
            if (SourceText.IsUnderSkippedDirectory(repoRoot, file) || IsArchitectureTest(repoRoot, file))
            {
                continue;
            }

            files.Add((SourceText.Relative(repoRoot, file), File.ReadAllText(file)));
        }

        return files;
    }

    private static bool IsArchitectureTest(string repoRoot, string file)
    {
        string relative = SourceText.Relative(repoRoot, file);
        return relative.StartsWith("tests/OpenIdentityStack.Architecture.Tests/", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"\bstring\s+CreateLogoutToken\s*\(|\bclass\s+LogoutTokenFactory\b", RegexOptions.CultureInvariant)]
    private static partial Regex ProducerPattern();
}
