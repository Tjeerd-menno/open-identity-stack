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

    private static readonly Regex SignatureAssertion = SignatureAssertionPattern();

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

    [Fact]
    public void ValidationCallWithoutAResultAssertion_DoesNotCount()
    {
        const string production = """
            public sealed class LogoutTokenFactory
            {
                public string CreateLogoutToken(SessionId sessionId, string clientId) => "token";
            }
            """;
        const string tests = """
            public sealed class LogoutTokenFactoryTests
            {
                [Fact]
                public async Task CreateLogoutToken_ProducesTokenThatValidatesAgainstThePublicKey()
                {
                    LogoutTokenFactory factory = this.CreateFactory();
                    string token = factory.CreateLogoutToken(SessionId.Create(), "client-1");
                    TokenValidationResult result = await new JsonWebTokenHandler().ValidateTokenAsync(
                        token,
                        new TokenValidationParameters
                        {
                            IssuerSigningKey = new RsaSecurityKey(this._rsa.ExportParameters(false)),
                            ValidateIssuerSigningKey = true,
                        });
                }
            }
            """;

        List<string> problems = FindProblems(
            [("src/OpenIdentityStack.Infrastructure/Identity/LogoutTokenFactory.cs", production)],
            [("tests/OpenIdentityStack.Infrastructure.Tests/Identity/LogoutTokenFactoryTests.cs", tests)]);

        problems.ShouldNotBeEmpty();
        string text = string.Join(Environment.NewLine, problems);
        text.ShouldContain("LogoutTokenFactory");
        text.ShouldContain("algorithm or the validation result");
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
                    $"{path}:{line}: logout token tests do not assert a signature. LogoutTokenFactory tests must assert the algorithm or the validation result.");
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

                if (SignatureAssertion.IsMatch(method.Body))
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

    [GeneratedRegex(
        @"\.Alg\s*\.\s*Should|\.IsValid\s*\.\s*Should|\.Exception\s*\.\s*Should|\bAssert\s*\.\s*\w+\s*\([^;]*\.(?:Alg|IsValid|Exception)\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex SignatureAssertionPattern();
}
