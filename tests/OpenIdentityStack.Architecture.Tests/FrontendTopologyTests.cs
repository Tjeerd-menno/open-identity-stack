namespace OpenIdentityStack.Architecture.Tests;

public sealed class FrontendTopologyTests
{
    private static readonly string[] ObsoleteUiModules =
    [
        "OpenIdentityStack." + "Admin" + "Web",
        "OpenIdentityStack.ManagementWeb." + "Legacy",
        "Admin" + "Web",
        "ManagementWeb." + "Legacy",
    ];

    private static readonly string[] SkippedDirectories =
    [
        ".agents",
        ".claude",
        ".git",
        ".vs",
        "bin",
        "obj",
        "node_modules",
        "dist",
    ];

    [Fact]
    public void ManagementWeb_IsTheOnlyUiModule()
    {
        string repoRoot = FindRepositoryRoot();

        Directory.Exists(Path.Combine(repoRoot, "src", ObsoleteUiModules[0])).ShouldBeFalse();
        Directory.Exists(Path.Combine(repoRoot, "src", ObsoleteUiModules[1])).ShouldBeFalse();

        string[] offendingFiles = Directory
            .EnumerateFiles(repoRoot, "*", SearchOption.AllDirectories)
            .Where(IsTextFile)
            .Where(path => !IsUnderSkippedDirectory(repoRoot, path))
            .Where(ContainsObsoleteUiReference)
            .Select(path => Path.GetRelativePath(repoRoot, path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        offendingFiles.ShouldBeEmpty();
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

    private static bool IsUnderSkippedDirectory(string repoRoot, string path)
    {
        string relativePath = Path.GetRelativePath(repoRoot, path);
        return relativePath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => SkippedDirectories.Contains(part, StringComparer.OrdinalIgnoreCase));
    }

    private static bool ContainsObsoleteUiReference(string path)
    {
        string content = File.ReadAllText(path);
        return ObsoleteUiModules.Any(token => content.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTextFile(string path)
    {
        string extension = Path.GetExtension(path);
        return extension is
            ".cs" or
            ".csproj" or
            ".json" or
            ".md" or
            ".ps1" or
            ".sh" or
            ".slnx" or
            ".ts" or
            ".tsx" or
            ".yaml" or
            ".yml";
    }
}
