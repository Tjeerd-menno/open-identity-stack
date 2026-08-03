namespace OpenIdentityStack.Conformance.Runner;

/// <summary>Command-line configuration for the conformance runner.</summary>
internal sealed record RunnerOptions
{
    /// <summary>
    /// Modules that pause for a human reviewer and can never reach a machine
    /// verdict above <c>REVIEW</c>. The runner has no way to acknowledge the
    /// review, so each one sits in <c>WAITING</c> until the per-test deadline
    /// expires — and a test still holding the alias when the next one starts is
    /// killed with an alias conflict, silently corrupting every later result.
    /// They are therefore excluded from automated sweeps by default.
    /// </summary>
    public static readonly IReadOnlySet<string> ManualReviewModules =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "oidcc-ensure-registered-redirect-uri",
            "oidcc-ensure-request-object-with-redirect-uri",
            "oidcc-max-age-1",
            "oidcc-prompt-login",
            "oidcc-response-type-missing",
        };

    /// <summary>Options that take no value.</summary>
    private static readonly IReadOnlySet<string> FlagOptions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "headed",
            "include-manual-review",
        };

    /// <summary>Options that take a value.</summary>
    private static readonly IReadOnlySet<string> ValueOptions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "suite",
            "plan",
            "variant",
            "config",
            "out",
            "username",
            "timeout",
            "suite-host",
            "only",

            // Recognised so it can be rejected with an explanation rather than
            // dismissed as a typo.
            "password",
        };

    public string SuiteBaseUrl { get; init; } = "https://localhost.emobix.co.uk:8443";

    public string PlanName { get; init; } = "oidcc-basic-certification-test-plan";

    public string VariantJson { get; init; } =
        """{"server_metadata":"discovery","client_registration":"static_client"}""";

    public string ConfigPath { get; init; } = "plan-config.json";

    public string OutputPath { get; init; } = "conformance-results.json";

    public string Username { get; init; } = "alice@example.test";

    public string Password { get; init; } = string.Empty;

    public bool Headless { get; init; } = true;

    /// <summary>When true, manual-review modules are run despite always stalling.</summary>
    public bool IncludeManualReview { get; init; }

    /// <summary>
    /// When true, the driver stops at the OP's login form and waits for the
    /// operator before submitting. Set automatically for the manual-review
    /// modules, whose evidence *is* the fresh login form: submitting it
    /// immediately leaves nothing to screenshot.
    /// </summary>
    public bool PauseAtLoginForm { get; init; }

    public int PerTestTimeoutSeconds { get; init; } = 180;

    public float NavigationTimeoutMs { get; init; } = 30_000;

    public int SettleTimeoutMs { get; init; } = 30_000;

    /// <summary>Host that indicates the browser has returned to the suite.</summary>
    public string SuiteHost { get; init; } = "localhost.emobix.co.uk";

    /// <summary>
    /// Scheme and authority of the OP under test, derived from the plan config's
    /// discovery URL. Credentials are only ever typed into a page served from
    /// here: the suite chooses the interaction URL, so without this check a
    /// spoofed or misconfigured suite could collect the certification password —
    /// and certificate validation is deliberately disabled, so TLS will not
    /// catch it either.
    /// </summary>
    public string OpOrigin { get; init; } = string.Empty;

    /// <summary>When non-empty, only these modules are run.</summary>
    public IReadOnlySet<string> Only { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public static RunnerOptions FromArgs(string[] args)
    {
        Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> flags = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unexpected argument '{args[i]}'; options are written as --name value.");
            }

            string name = args[i][2..];

            // A mistyped --suiet would otherwise be stored and ignored, and the
            // run would quietly produce evidence against the default suite.
            if (!FlagOptions.Contains(name) && !ValueOptions.Contains(name))
            {
                throw new InvalidOperationException($"Unknown option '--{name}'.");
            }

            if (FlagOptions.Contains(name))
            {
                flags.Add(name);
                continue;
            }

            if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Option '--{name}' requires a value.");
            }

            map[name] = args[++i];
        }

        var defaults = new RunnerOptions();

        // Deliberately not accepted on the command line: argv is readable by other
        // processes for the tens of minutes a sweep runs, and shells persist it to
        // history.
        if (map.ContainsKey("password"))
        {
            throw new InvalidOperationException(
                "--password is not accepted; the credential would persist in the process "
                + "command line and in shell history. Set CONFORMANCE_PASSWORD instead.");
        }

        string password = Environment.GetEnvironmentVariable("CONFORMANCE_PASSWORD") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException("A password is required. Set CONFORMANCE_PASSWORD.");
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
            Headless = !flags.Contains("headed"),
            IncludeManualReview = flags.Contains("include-manual-review"),
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
