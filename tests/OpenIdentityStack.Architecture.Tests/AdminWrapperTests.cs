using System.Text.RegularExpressions;

namespace OpenIdentityStack.Architecture.Tests;

/// <summary>
/// Use cases stay public and are Api's entry point.
/// At 1edd28f2b87805b26861c1f8094af6f238584b91 (closed PR #552) IUsersAdmin only forwarded.
/// IApplicationsAdminWorkflow already existed and stays. A new I*Admin or IInteractiveSignIn does not.
/// </summary>
public sealed partial class AdminWrapperTests
{
    private static readonly Regex WrapperName = WrapperNamePattern();

    [Fact]
    public void Source_DoesNotAddAnAdminWrapper()
    {
        string repoRoot = SourceText.RepositoryRoot();
        var hits = new List<string>();
        foreach (string file in Directory.EnumerateFiles(Path.Combine(repoRoot, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (SourceText.IsUnderSkippedDirectory(repoRoot, file))
            {
                continue;
            }

            hits.AddRange(FindWrappers(SourceText.Relative(repoRoot, file), File.ReadAllText(file)));
        }

        hits.Sort(StringComparer.Ordinal);
        hits.ShouldBeEmpty(
            "Api calls the public use case or query handler. Do not add I*Admin or IInteractiveSignIn."
            + Environment.NewLine
            + string.Join(Environment.NewLine, hits));
    }

    [Fact]
    public void HistoricalUsersAdmin_IsAPassThrough()
    {
        string source = File.ReadAllText(Path.Combine(
            SourceText.RepositoryRoot(),
            "tests",
            "OpenIdentityStack.Architecture.Tests",
            "Fixtures",
            "historical-users-admin.cs"));
        List<string> hits = FindWrappers("src/OpenIdentityStack.Application/Users/IUsersAdmin.cs", source);

        hits.ShouldContain(hit => hit.Contains("IUsersAdmin", StringComparison.Ordinal));
        string.Join(Environment.NewLine, hits).ShouldContain("pass-through wrapper");
    }

    internal static List<string> FindWrappers(string relativePath, string source)
    {
        string[] lines = source.Split('\n');
        var hits = new List<string>();
        for (int index = 0; index < lines.Length; index++)
        {
            foreach (Match match in WrapperName.Matches(lines[index]))
            {
                hits.Add(
                    $"{relativePath}:{index + 1}: {match.Value} is a pass-through wrapper. Api calls the public use case or query handler. Do not add I*Admin or IInteractiveSignIn.");
            }
        }

        return hits;
    }

    [GeneratedRegex(@"\b(I[A-Z][A-Za-z0-9]*Admin|IInteractiveSignIn)\b", RegexOptions.CultureInvariant)]
    private static partial Regex WrapperNamePattern();
}
