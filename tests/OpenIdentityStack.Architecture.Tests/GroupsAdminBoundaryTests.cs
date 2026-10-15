using System.Text.RegularExpressions;

namespace OpenIdentityStack.Architecture.Tests;

/// <summary>
/// Api Groups code may name <see cref="OpenIdentityStack.Application.Groups.IGroupsAdmin"/> only.
/// At 23397e327200fa9842dd860b83b1d922f6f793ac, GroupsApi injects ICreateGroupUseCase and
/// IRoleRepository directly.
/// </summary>
public sealed class GroupsAdminBoundaryTests
{
    private static readonly Regex ForbiddenName = new(
        @"\b(IGroupRepository|IRoleRepository|IUserRepository|ISessionRepository|[A-Za-z_][A-Za-z0-9]*(?:UseCase|QueryHandler))\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [Fact]
    public void ApiGroupsCode_NamesIGroupsAdminOnly()
    {
        string repoRoot = FindRepositoryRoot();
        string groupsDirectory = Path.Combine(repoRoot, "src", "OpenIdentityStack.Api", "Groups");
        var hits = new List<string>();
        foreach (string file in Directory.EnumerateFiles(groupsDirectory, "*.cs"))
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
            "Api Groups code names IGroupsAdmin. A Groups use case or repository in that folder is a second path."
            + Environment.NewLine
            + string.Join(Environment.NewLine, hits));

        string groupsApi = File.ReadAllText(Path.Combine(groupsDirectory, "GroupsApi.cs"));
        groupsApi.ShouldContain("IGroupsAdmin");
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
