using System.Text.RegularExpressions;

namespace OpenIdentityStack.Architecture.Tests;

/// <summary>
/// The final Dockerfile stage must not run as root.
/// Nginx images end with USER nginx. ASP.NET images end with USER app.
/// At a978202c4489f2cb110edbd4750dedf598dcc44c (parent of b96d9c3, PR #419) the
/// Management Web nginx stage has no USER, so the container runs as root.
/// </summary>
public sealed class ContainerUserTests
{
    private static readonly Regex FromPattern = new(
        @"^\s*FROM\s+(?:--platform=\S+\s+)?(\S+)",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex UserPattern = new(
        @"^\s*USER\s+(\S+)",
        RegexOptions.Multiline | RegexOptions.CultureInvariant);

    [Fact]
    public void Dockerfiles_EndAsAppOrNginx()
    {
        string repoRoot = FindRepositoryRoot();
        var hits = new List<string>();
        foreach (string file in Directory.EnumerateFiles(repoRoot, "Dockerfile", SearchOption.AllDirectories))
        {
            if (IsUnderSkippedDirectory(repoRoot, file))
            {
                continue;
            }

            hits.AddRange(FindUserProblems(repoRoot, file));
        }

        hits.Sort(StringComparer.Ordinal);
        hits.ShouldBeEmpty(
            "A Dockerfile's final stage ends with USER app or USER nginx. A missing USER runs as root."
            + Environment.NewLine
            + string.Join(Environment.NewLine, hits));
    }

    private static List<string> FindUserProblems(string repoRoot, string file)
    {
        string text = File.ReadAllText(file);
        string relative = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
        MatchCollection froms = FromPattern.Matches(text);
        if (froms.Count == 0)
        {
            return [$"{relative}: Dockerfile has no FROM. Expected USER app or USER nginx."];
        }

        Match lastFrom = froms[^1];
        string image = lastFrom.Groups[1].Value;
        string stage = text[lastFrom.Index..];
        MatchCollection users = UserPattern.Matches(stage);
        string? lastUser = users.Count == 0 ? null : users[^1].Groups[1].Value;
        string expected = ExpectedUser(image);
        if (string.Equals(lastUser, expected, StringComparison.Ordinal))
        {
            return [];
        }

        string actual = lastUser is null
            ? "has no USER, so the container runs as root"
            : $"ends with USER {lastUser}";
        return [$"{relative}: final stage {image} {actual}. Expected USER {expected}."];
    }

    private static string ExpectedUser(string image)
    {
        if (image.Contains("nginx", StringComparison.OrdinalIgnoreCase))
        {
            return "nginx";
        }

        if (image.Contains("aspnet", StringComparison.OrdinalIgnoreCase)
            || image.Contains("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return "app";
        }

        return "app";
    }

    private static bool IsUnderSkippedDirectory(string repoRoot, string file)
    {
        string relative = Path.GetRelativePath(repoRoot, file);
        foreach (string segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (segment is ".git" or "bin" or "obj" or "node_modules" or "dist")
            {
                return true;
            }
        }

        return false;
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
