using System.Text.RegularExpressions;

namespace OpenIdentityStack.Architecture.Tests;

/// <summary>
/// Api Sessions code may name <see cref="OpenIdentityStack.Application.Sessions.ISessionsAdmin"/> only.
/// At 7e81b0a18f176f097772b7ce6b198bc6625dc4b3, SessionsApi injects IListSessionsQueryHandler and the
/// other admin session use cases directly.
/// </summary>
public sealed class SessionsAdminBoundaryTests
{
    private static readonly Regex ForbiddenName = new(
        @"\b(ISessionRepository|IUserRepository|IRoleRepository|IGroupRepository|[A-Za-z_][A-Za-z0-9]*(?:UseCase|QueryHandler))\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [Fact]
    public void ApiSessionsCode_NamesISessionsAdminOnly()
    {
        string repoRoot = FindRepositoryRoot();
        string sessionsDirectory = Path.Combine(repoRoot, "src", "OpenIdentityStack.Api", "Sessions");
        var hits = new List<string>();
        foreach (string file in Directory.EnumerateFiles(sessionsDirectory, "*.cs"))
        {
            string[] lines = File.ReadAllLines(file);
            string relativePath = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
            for (int index = 0; index < lines.Length; index++)
            {
                foreach (Match match in ForbiddenName.Matches(lines[index]))
                {
                    hits.Add($"{relativePath}:{index + 1}: {match.Value}");
                }
            }
        }

        hits.Sort(StringComparer.Ordinal);
        hits.ShouldBeEmpty(
            "Api Sessions code names ISessionsAdmin. A Sessions use case or repository in that folder is a second path."
            + Environment.NewLine
            + string.Join(Environment.NewLine, hits));

        string sessionsApi = File.ReadAllText(Path.Combine(sessionsDirectory, "SessionsApi.cs"));
        sessionsApi.ShouldContain("ISessionsAdmin");
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
