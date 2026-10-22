using System.Diagnostics;

namespace OpenIdentityStack.Architecture.Tests;

/// <summary>
/// The coverage gate rejects a module report that has no measured lines.
/// A line-rate of 1 with an empty class list used to pass the per-file floor and add
/// nothing to the overall rate, so that module was ignored.
/// </summary>
public sealed class CoverageGateTests
{
    [Fact]
    public void EmptyModuleReport_FailsTheCoverageGate()
    {
        string repoRoot = FindRepositoryRoot();
        string resultsDir = Path.Combine(
            repoRoot,
            "tests",
            "OpenIdentityStack.Architecture.Tests",
            "Fixtures",
            "coverage-gate",
            "empty-module");
        (int exitCode, string output) = RunGate(repoRoot, resultsDir);

        exitCode.ShouldNotBe(0, output);
        output.ShouldContain("Coverage report has no measured lines: empty-module.cobertura.xml");
    }

    [Fact]
    public void MeasuredModuleReport_PassesTheCoverageGate()
    {
        string repoRoot = FindRepositoryRoot();
        string resultsDir = Path.Combine(
            repoRoot,
            "tests",
            "OpenIdentityStack.Architecture.Tests",
            "Fixtures",
            "coverage-gate",
            "healthy");
        (int exitCode, string output) = RunGate(repoRoot, resultsDir);

        exitCode.ShouldBe(0, output);
    }

    private static (int ExitCode, string Output) RunGate(string repoRoot, string resultsDir)
    {
        string script = Path.Combine(repoRoot, "scripts", "ci", "verify-coverage.sh");
        var start = new ProcessStartInfo
        {
            FileName = "bash",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("--results-dir");
        start.ArgumentList.Add(resultsDir);
        start.ArgumentList.Add("--minimum-line-rate");
        start.ArgumentList.Add("0.10");
        start.ArgumentList.Add("--minimum-overall-line-rate");
        start.ArgumentList.Add("0.80");
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start verify-coverage.sh.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output + error);
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
