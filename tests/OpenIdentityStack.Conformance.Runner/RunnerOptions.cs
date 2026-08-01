namespace OpenIdentityStack.Conformance.Runner;

/// <summary>Command-line configuration for the conformance runner.</summary>
internal sealed class RunnerOptions
{
    public string SuiteBaseUrl { get; init; } = "https://localhost.emobix.co.uk:8443";

    public string PlanName { get; init; } = "oidcc-basic-certification-test-plan";

    public string VariantJson { get; init; } =
        """{"server_metadata":"discovery","client_registration":"static_client"}""";

    public string ConfigPath { get; init; } = "plan-config.json";

    public string OutputPath { get; init; } = "conformance-results.json";

    public string Username { get; init; } = "alice@example.test";

    public string Password { get; init; } = string.Empty;

    public bool Headless { get; init; } = true;

    public int PerTestTimeoutSeconds { get; init; } = 180;

    public float NavigationTimeoutMs { get; init; } = 30_000;

    public int SettleTimeoutMs { get; init; } = 8_000;

    /// <summary>Host that indicates the browser has returned to the suite.</summary>
    public string SuiteHost { get; init; } = "localhost.emobix.co.uk";

    /// <summary>When non-empty, only these modules are run.</summary>
    public IReadOnlySet<string> Only { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public static RunnerOptions FromArgs(string[] args)
    {
        Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                map[args[i][2..]] = args[i + 1];
            }
        }

        var defaults = new RunnerOptions();

        string password = map.GetValueOrDefault("password")
            ?? Environment.GetEnvironmentVariable("CONFORMANCE_PASSWORD")
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "A password is required. Pass --password or set CONFORMANCE_PASSWORD.");
        }

        return new RunnerOptions
        {
            SuiteBaseUrl = map.GetValueOrDefault("suite") ?? defaults.SuiteBaseUrl,
            PlanName = map.GetValueOrDefault("plan") ?? defaults.PlanName,
            VariantJson = map.GetValueOrDefault("variant") ?? defaults.VariantJson,
            ConfigPath = map.GetValueOrDefault("config") ?? defaults.ConfigPath,
            OutputPath = map.GetValueOrDefault("out") ?? defaults.OutputPath,
            Username = map.GetValueOrDefault("username") ?? defaults.Username,
            Password = password,
            Headless = !map.ContainsKey("headed"),
            PerTestTimeoutSeconds = int.TryParse(map.GetValueOrDefault("timeout"), out int t)
                ? t
                : defaults.PerTestTimeoutSeconds,
            SuiteHost = map.GetValueOrDefault("suite-host") ?? defaults.SuiteHost,
            Only = (map.GetValueOrDefault("only") ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.Ordinal),
        };
    }
}
