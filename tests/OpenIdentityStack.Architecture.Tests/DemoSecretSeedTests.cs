namespace OpenIdentityStack.Architecture.Tests;

/// <summary>
/// Production must not register the source-controlled demo client secret.
/// At 0943f58a7e59814996feee5a73ac224ef8ef9a7e (526cae99613eca0c9dee0ca2f2f3045b5327c49f^, PR #398)
/// ShouldSeedDemoClients honored Seed:DemoClients before it refused Production.
/// </summary>
public sealed class DemoSecretSeedTests
{
    [Fact]
    public void ProductionSeedGuard_RefusesADemoSecret()
    {
        string repoRoot = SourceText.RepositoryRoot();
        string path = Path.Combine(repoRoot, "src", "OpenIdentityStack.DbMigrator", "Program.cs");
        List<string> problems = FindProblems(SourceText.Relative(repoRoot, path), File.ReadAllText(path));

        problems.ShouldBeEmpty(
            "ShouldSeedDemoClients, the Production seed guard, must return false before Seed:DemoClients."
            + Environment.NewLine
            + string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void HistoricalProductionSeed_CanRegisterADemoSecret()
    {
        string source = File.ReadAllText(Path.Combine(
            SourceText.RepositoryRoot(),
            "tests",
            "OpenIdentityStack.Architecture.Tests",
            "Fixtures",
            "historical-production-demo-seed.cs"));
        List<string> problems = FindProblems(
            "src/OpenIdentityStack.DbMigrator/Program.cs",
            source);

        problems.ShouldNotBeEmpty();
        problems[0].ShouldContain("ShouldSeedDemoClients");
        problems[0].ShouldContain("Production seed guard");
        problems[0].ShouldContain("demo secret");
    }

    internal static List<string> FindProblems(string relativePath, string source)
    {
        if (!SourceText.TryMethodBody(source, "ShouldSeedDemoClients", out int line, out string body))
        {
            if (source.Contains("isotopes-api-resource-secret", StringComparison.Ordinal)
                || source.Contains("Seed:DemoClients", StringComparison.Ordinal))
            {
                return
                [
                    $"{relativePath}:1: ShouldSeedDemoClients, the Production seed guard, is missing, so Production can register a demo secret.",
                ];
            }

            return [];
        }

        int production = body.IndexOf("IsProduction()", StringComparison.Ordinal);
        int staging = body.IndexOf("IsStaging()", StringComparison.Ordinal);
        int seedConfig = body.IndexOf("Seed:DemoClients", StringComparison.Ordinal);
        int returnFalse = production < 0 ? -1 : body.IndexOf("return false", production, StringComparison.Ordinal);
        if (production >= 0
            && staging >= 0
            && seedConfig >= 0
            && production < seedConfig
            && staging < seedConfig
            && returnFalse >= 0
            && returnFalse < seedConfig)
        {
            return [];
        }

        int reportLine = line;
        if (seedConfig >= 0)
        {
            int bodyStart = source.IndexOf("bool ShouldSeedDemoClients", StringComparison.Ordinal);
            int open = source.IndexOf('{', bodyStart);
            reportLine = SourceText.LineAt(source, open + 1 + seedConfig);
        }

        return
        [
            $"{relativePath}:{reportLine}: ShouldSeedDemoClients, the Production seed guard, can register a demo secret in Production. Return false when IsProduction() before Seed:DemoClients.",
        ];
    }
}
