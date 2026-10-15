using System.Text.RegularExpressions;

namespace OpenIdentityStack.Architecture.Tests;

/// <summary>
/// Authentication names IInteractiveSignIn. It does not name a repository.
/// At b721c778a1a6f12b4c74894c3106c04647f23f07, SessionCookieValidation names IUserRepository and ISessionRepository.
/// </summary>
public sealed class InteractiveSignInBoundaryTests
{
    private static readonly Regex RepositoryName = new(
        @"\b[A-Za-z_][A-Za-z0-9]*Repository\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [Fact]
    public void Authentication_NamesInteractiveSignInAndNoRepository()
    {
        string repoRoot = FindRepositoryRoot();
        string authenticationDirectory = Path.Combine(repoRoot, "src", "OpenIdentityStack.Api", "Authentication");
        var hits = new List<string>();
        foreach (string file in Directory.EnumerateFiles(authenticationDirectory, "*.cs", SearchOption.AllDirectories))
        {
            string[] lines = File.ReadAllLines(file);
            string relativePath = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
            for (int index = 0; index < lines.Length; index++)
            {
                foreach (Match match in RepositoryName.Matches(lines[index]))
                {
                    hits.Add($"{relativePath}:{index + 1}: {match.Value}");
                }
            }
        }

        hits.Sort(StringComparer.Ordinal);
        hits.ShouldBeEmpty(
            "Authentication names IInteractiveSignIn. A repository in that folder is a second path."
            + Environment.NewLine
            + string.Join(Environment.NewLine, hits));

        string authenticationSource = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(authenticationDirectory, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        authenticationSource.ShouldContain("IInteractiveSignIn");
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
