namespace OpenIdentityStack.Architecture.Tests;

/// <summary>
/// Source checks for mistakes that still compile when an agent copies the nearest file.
/// The architecture project is excluded from every scan: the sleep check names
/// WaitForTimeoutAsync in its own comment, and an earlier spike failed by matching itself.
/// </summary>
public sealed class AgentShapeTests
{
    private static readonly string[] SkippedDirectories =
    [
        ".git",
        "bin",
        "obj",
        "node_modules",
        "dist",
    ];

    [Fact]
    public void E2E_DoesNotCallPlaywrightLocatorCheckAsync()
    {
        List<string> hits = FindLines(
            Path.Combine("tests"),
            path => !IsArchitectureTest(path),
            static line => line.Contains(".CheckAsync(", StringComparison.Ordinal));

        hits.ShouldBeEmpty(
            "Playwright locator CheckAsync bypasses the control. Click the checkbox and assert its state."
            + Environment.NewLine
            + string.Join(Environment.NewLine, hits));
    }

    /// <summary>
    /// Already green on the current tree. At 3abc364264c163cb755deb1c8d14bc03ffe4eb39
    /// (b5f1922^, before PR #200 deleted the Admin Web tests) the same needle hits seven lines:
    /// AuthenticationFlowTests.cs 70 and 131, GroupManagementTests.cs 330 and 516,
    /// ProviderManagementTests.cs 346, SessionManagementTests.cs 238 and 315.
    /// </summary>
    [Fact]
    public void Tests_DoNotWaitForTimeoutAsync()
    {
        List<string> hits = FindLines(
            Path.Combine("tests"),
            path => !IsArchitectureTest(path),
            static line => line.Contains("WaitForTimeoutAsync", StringComparison.Ordinal));

        hits.ShouldBeEmpty(
            "Wait on a concrete UI or network condition. Fixed sleeps were removed with the Admin Web tests in PR #200."
            + Environment.NewLine
            + string.Join(Environment.NewLine, hits));
    }

    [Fact]
    public void Api_HasASingleErrorResultMapper()
    {
        List<string> hits = FindLines(
            Path.Combine("src", "OpenIdentityStack.Api"),
            path => !path.EndsWith($"{Path.DirectorySeparatorChar}ErrorResultMapper.cs", StringComparison.Ordinal),
            static line => line.Contains("IResult ToErrorResult", StringComparison.Ordinal));

        hits.ShouldBeEmpty(
            "Map HTTP errors through ErrorResultMapper.ToErrorResult. A second ToErrorResult is a second response body."
            + Environment.NewLine
            + string.Join(Environment.NewLine, hits));
    }

    [Fact]
    public void AdminMinimalApis_DoNotInjectRepositories()
    {
        List<string> hits = FindLines(
            Path.Combine("src", "OpenIdentityStack.Api"),
            path => path.EndsWith("Api.cs", StringComparison.Ordinal),
            static line => line.Contains("IUserRepository", StringComparison.Ordinal)
                || line.Contains("ISessionRepository", StringComparison.Ordinal));

        hits.ShouldBeEmpty(
            "Minimal API files take a query handler. IUserRepository and ISessionRepository stay in Application."
            + Environment.NewLine
            + string.Join(Environment.NewLine, hits));
    }

    private static List<string> FindLines(string relativeRoot, Func<string, bool> includeFile, Func<string, bool> lineMatches)
    {
        string repoRoot = FindRepositoryRoot();
        string scanRoot = Path.Combine(repoRoot, relativeRoot);
        var hits = new List<string>();

        foreach (string file in Directory.EnumerateFiles(scanRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (IsUnderSkippedDirectory(repoRoot, file) || !includeFile(file))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(file);
            for (int index = 0; index < lines.Length; index++)
            {
                if (!lineMatches(lines[index]))
                {
                    continue;
                }

                string relativePath = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
                hits.Add($"{relativePath}:{index + 1}: {lines[index].Trim()}");
            }
        }

        hits.Sort(StringComparer.Ordinal);
        return hits;
    }

    private static bool IsArchitectureTest(string path) =>
        path.Contains(
            $"{Path.DirectorySeparatorChar}OpenIdentityStack.Architecture.Tests{Path.DirectorySeparatorChar}",
            StringComparison.Ordinal);

    private static bool IsUnderSkippedDirectory(string repoRoot, string path)
    {
        string relativePath = Path.GetRelativePath(repoRoot, path);
        return relativePath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => SkippedDirectories.Contains(part, StringComparer.OrdinalIgnoreCase));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenIdentityStack.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
