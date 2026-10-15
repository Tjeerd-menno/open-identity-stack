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
            static line => line.Contains("IResult ToErrorResult", StringComparison.Ordinal)
                || line.Contains("title: error.Code", StringComparison.Ordinal));

        hits.ShouldBeEmpty(
            "Map HTTP errors through ErrorResultMapper.ToErrorResult. A second mapper, including a forbidden Problem Details body whose title is the error code, is a second response body."
            + Environment.NewLine
            + string.Join(Environment.NewLine, hits));
    }

    [Fact]
    public void ApplicationsOpenApi_LivesOnlyUnderContracts()
    {
        string repoRoot = FindRepositoryRoot();
        var copies = Directory
            .EnumerateFiles(repoRoot, "applications.openapi.yaml", SearchOption.AllDirectories)
            .Where(path => !IsUnderSkippedDirectory(repoRoot, path))
            .Select(path => Path.GetRelativePath(repoRoot, path).Replace('\\', '/'))
            .Where(path => !path.StartsWith("contracts/openapi/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        copies.ShouldBeEmpty(
            "Contract tests read contracts/openapi only. A second applications.openapi.yaml is a second document."
            + Environment.NewLine
            + string.Join(Environment.NewLine, copies));
    }

    [Fact]
    public void ManagementWeb_ImportsGeneratedPermissionConstants()
    {
        string repoRoot = FindRepositoryRoot();
        string[] permissionValues = ReadPermissionConstantValues(repoRoot)
            .Where(value => value.Contains(':', StringComparison.Ordinal))
            .ToArray();
        List<string> hits = FindQuotedPermissionValues(repoRoot, permissionValues);

        hits.ShouldBeEmpty(
            "Routes and buttons import the module generated from Permissions.cs. A quoted permission string is a hand-synced list."
            + Environment.NewLine
            + string.Join(Environment.NewLine, hits));

        GeneratedPermissionModuleIsFresh(repoRoot);
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

    private static IEnumerable<string> ReadPermissionConstantValues(string repoRoot)
    {
        string source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "OpenIdentityStack.Application",
            "Authorization",
            "Permissions.cs"));
        var values = new List<string>();
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
            source,
            """public const string \w+ = "([^"]*)";"""))
        {
            values.Add(match.Groups[1].Value);
        }

        return values;
    }

    private static List<string> FindQuotedPermissionValues(string repoRoot, IReadOnlyList<string> permissionValues)
    {
        string scanRoot = Path.Combine(repoRoot, "src", "OpenIdentityStack.ManagementWeb", "src");
        var hits = new List<string>();
        foreach (string file in Directory.EnumerateFiles(scanRoot, "*", SearchOption.AllDirectories))
        {
            if (IsUnderSkippedDirectory(repoRoot, file) || !IsHandSyncedPermissionScan(file))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(file);
            for (int index = 0; index < lines.Length; index++)
            {
                if (!permissionValues.Any(value => LineQuotesPermission(lines[index], value)))
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

    private static bool IsHandSyncedPermissionScan(string path)
    {
        if (path.EndsWith($"{Path.DirectorySeparatorChar}generated-permissions.ts", StringComparison.Ordinal))
        {
            return false;
        }

        return !path.EndsWith(".test.ts", StringComparison.Ordinal)
            && !path.EndsWith(".test.tsx", StringComparison.Ordinal)
            && (path.EndsWith(".ts", StringComparison.Ordinal) || path.EndsWith(".tsx", StringComparison.Ordinal));
    }

    private static bool LineQuotesPermission(string line, string permission)
    {
        return line.Contains($"'{permission}'", StringComparison.Ordinal)
            || line.Contains($"\"{permission}\"", StringComparison.Ordinal);
    }

    private static void GeneratedPermissionModuleIsFresh(string repoRoot)
    {
        string script = Path.Combine(repoRoot, "scripts", "ci", "generate-permission-constants.py");
        var start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "python3",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("--check");
        using System.Diagnostics.Process process = System.Diagnostics.Process.Start(start)
            ?? throw new InvalidOperationException("Could not start the permission generator.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, output + error);
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
