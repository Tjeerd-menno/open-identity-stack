using System.Text.RegularExpressions;

namespace OpenIdentityStack.Architecture.Tests;

/// <summary>
/// Api Users code may name <see cref="OpenIdentityStack.Application.Users.IUsersAdmin"/> only.
/// At a453af641d094efaad488f48267422a1aed68a43, UsersApi injects ICreateUserUseCase and the
/// other Users use cases directly.
/// </summary>
public sealed class UsersAdminBoundaryTests
{
    private static readonly Regex ForbiddenName = new(
        @"\b(IUserRepository|[A-Za-z_][A-Za-z0-9]*(?:UseCase|QueryHandler))\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [Fact]
    public void ApiUsersCode_NamesIUsersAdminOnly()
    {
        string repoRoot = FindRepositoryRoot();
        string usersDirectory = Path.Combine(repoRoot, "src", "OpenIdentityStack.Api", "Users");
        var hits = new List<string>();
        foreach (string file in Directory.EnumerateFiles(usersDirectory, "*.cs"))
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
            "Api Users code names IUsersAdmin. A Users use case or IUserRepository in that folder is a second path."
            + Environment.NewLine
            + string.Join(Environment.NewLine, hits));

        string usersApi = File.ReadAllText(Path.Combine(usersDirectory, "UsersApi.cs"));
        usersApi.ShouldContain("IUsersAdmin");
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
