using System.Text.RegularExpressions;

namespace OpenIdentityStack.Architecture.Tests;

/// <summary>
/// Concrete use-case and query-handler classes in Application stay public and sealed.
/// Api calls those types directly, so a non-public or unsealed class is a break in that entry point.
/// </summary>
public sealed class UseCaseSealingTests
{
    private static readonly Regex ClassDeclaration = new(
        @"^(?<modifiers>(?:public|internal|protected|private|file|sealed|abstract|static|partial|new)\s+)*class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)",
        RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [Fact]
    public void ApplicationUseCasesAndQueryHandlers_ArePublicSealed()
    {
        string repoRoot = FindRepositoryRoot();
        string applicationDirectory = Path.Combine(repoRoot, "src", "OpenIdentityStack.Application");
        var violations = new List<string>();
        foreach (string file in Directory.EnumerateFiles(applicationDirectory, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);
            string relativePath = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');
            foreach (Match match in ClassDeclaration.Matches(source))
            {
                string name = match.Groups["name"].Value;
                if (!IsUseCaseOrQueryHandler(name))
                {
                    continue;
                }

                string declaration = match.Value;
                if (declaration.Contains("abstract", StringComparison.Ordinal)
                    || declaration.Contains("static", StringComparison.Ordinal))
                {
                    continue;
                }

                bool isPublic = declaration.Contains("public", StringComparison.Ordinal);
                bool isSealed = declaration.Contains("sealed", StringComparison.Ordinal);
                if (isPublic && isSealed)
                {
                    continue;
                }

                int line = source[..match.Index].Count(character => character == '\n') + 1;
                violations.Add(
                    $"{relativePath}:{line}: {name} must be public sealed. Declare it as 'public sealed class {name}'.");
            }
        }

        violations.Sort(StringComparer.Ordinal);
        violations.ShouldBeEmpty(
            "Concrete use-case and query-handler classes in Application must be public sealed."
            + Environment.NewLine
            + string.Join(Environment.NewLine, violations));
    }

    private static bool IsUseCaseOrQueryHandler(string name) =>
        name.EndsWith("UseCase", StringComparison.Ordinal)
        || name.EndsWith("UseCases", StringComparison.Ordinal)
        || name.EndsWith("QueryHandler", StringComparison.Ordinal);

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
