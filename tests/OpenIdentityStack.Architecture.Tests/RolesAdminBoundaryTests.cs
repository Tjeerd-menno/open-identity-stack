using System.Text.RegularExpressions;

namespace OpenIdentityStack.Architecture.Tests;

/// <summary>
/// Api Roles code may name <see cref="OpenIdentityStack.Application.Roles.IRolesAdmin"/> only.
/// At 1edd28f2b87805b26861c1f8094af6f238584b91, RolesApi injects ICreateRoleUseCase and the
/// other Roles use cases directly.
/// </summary>
public sealed class RolesAdminBoundaryTests
{
    private static readonly Regex ForbiddenName = new(
        @"\b(IRoleRepository|IUserRepository|ISessionRepository|[A-Za-z_][A-Za-z0-9]*(?:UseCase|QueryHandler))\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [Fact]
    public void ApiRolesCode_NamesIRolesAdminOnly()
    {
        string repoRoot = FindRepositoryRoot();
        string apiDirectory = Path.Combine(repoRoot, "src", "OpenIdentityStack.Api");
        var hits = new List<string>();
        foreach (string file in Directory.EnumerateFiles(apiDirectory, "*Role*.cs", SearchOption.AllDirectories))
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
            "Api Roles code names IRolesAdmin. A Roles use case or repository in that code is a second path."
            + Environment.NewLine
            + string.Join(Environment.NewLine, hits));

        string rolesApi = File.ReadAllText(Path.Combine(apiDirectory, "Admin", "RolesApi.cs"));
        rolesApi.ShouldContain("IRolesAdmin");
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
