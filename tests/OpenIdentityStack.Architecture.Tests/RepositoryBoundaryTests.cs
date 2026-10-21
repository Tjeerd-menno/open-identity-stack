using System.Text.RegularExpressions;

namespace OpenIdentityStack.Architecture.Tests;

/// <summary>
/// Api and Authentication call use cases and query handlers. Repository ports stay inside Application.
/// </summary>
public sealed class RepositoryBoundaryTests
{
    private static readonly IReadOnlyDictionary<string, string> HandlerToCall = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["IUserRepository"] = "GetUserQueryHandler",
        ["ISessionRepository"] = "GetSessionQueryHandler",
        ["IRoleRepository"] = "GetRoleQueryHandler",
        ["IGroupRepository"] = "GetGroupQueryHandler",
        ["IUpstreamProviderRepository"] = "ListProvidersQueryHandler",
        ["IAuthenticationSettingsRepository"] = "GetAuthenticationSettingsQueryHandler",
        ["IApplicationRepository"] = "GetApplicationQueryHandler",
        ["IApplicationPermissionRegistryRepository"] = "ApplicationPermissionMaintenanceUseCases",
        ["IResourceAccessRepository"] = "ResourcePermissionService",
    };

    private static readonly Regex RepositoryName = new(
        @"\b(?<name>I(?:User|Session|Role|Group|UpstreamProvider|AuthenticationSettings|Application|ApplicationPermissionRegistry|ResourceAccess)Repository)\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [Fact]
    public void ApiAndAuthentication_DoNotReferenceRepositoryInterfaces()
    {
        string repoRoot = FindRepositoryRoot();
        string apiDirectory = Path.Combine(repoRoot, "src", "OpenIdentityStack.Api");
        var violations = new List<string>();
        foreach (string file in Directory.EnumerateFiles(apiDirectory, "*.cs", SearchOption.AllDirectories))
        {
            string[] lines = File.ReadAllLines(file);
            string relativePath = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
            for (int index = 0; index < lines.Length; index++)
            {
                foreach (Match match in RepositoryName.Matches(lines[index]))
                {
                    string repository = match.Groups["name"].Value;
                    string handler = HandlerToCall[repository];
                    violations.Add(
                        $"{relativePath}:{index + 1}: references {repository}. Call {handler} instead.");
                }
            }
        }

        violations.Sort(StringComparer.Ordinal);
        violations.ShouldBeEmpty(
            "Api and Authentication must not reference a repository interface. Call the use case or query handler instead."
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
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
