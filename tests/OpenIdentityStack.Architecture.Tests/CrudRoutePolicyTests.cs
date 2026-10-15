using System.Diagnostics;
using System.Text.RegularExpressions;

namespace OpenIdentityStack.Architecture.Tests;

/// <summary>
/// Single-permission CRUD routes take their permission from the endpoint
/// RequireAuthorization policy. At 85e46a1 (PR #143) the users route and nav
/// declare no permission while GET api/admin/users requires users:read.
/// Screens that combine several permissions stay explicit and are not listed here.
/// </summary>
public sealed class CrudRoutePolicyTests
{
    private readonly record struct CrudScreen(string Key, string UiPath, string ApiFile, string GroupPath, string Template);

    private static readonly CrudScreen[] Screens =
    [
        new("users", "users", "src/OpenIdentityStack.Api/Users/UsersApi.cs", "api/admin/users", ""),
        new("userDetail", "users/:userId", "src/OpenIdentityStack.Api/Users/UsersApi.cs", "api/admin/users", "{id:guid}"),
        new("groups", "groups", "src/OpenIdentityStack.Api/Groups/GroupsApi.cs", "api/admin/groups", ""),
        new("groupDetail", "groups/:groupId", "src/OpenIdentityStack.Api/Groups/GroupsApi.cs", "api/admin/groups", "{id:guid}"),
        new("roles", "roles", "src/OpenIdentityStack.Api/Admin/RolesApi.cs", "api/admin/roles", ""),
        new("roleDetail", "roles/:roleId", "src/OpenIdentityStack.Api/Admin/RolesApi.cs", "api/admin/roles", "{id:guid}"),
        new("applicationPermissions", "application-permissions", "src/OpenIdentityStack.Api/Admin/ApplicationPermissionsApi.cs", "api/admin/application-permissions", "applications"),
        new("applicationPermissionDetail", "application-permissions/:registrationId", "src/OpenIdentityStack.Api/Admin/ApplicationPermissionsApi.cs", "api/admin/application-permissions", "applications/{id:guid}"),
        new("applications", "applications", "src/OpenIdentityStack.Api/Applications/ApplicationsApi.cs", "api/admin/applications", ""),
        new("applicationDetail", "applications/:applicationId", "src/OpenIdentityStack.Api/Applications/ApplicationsApi.cs", "api/admin/applications", "{id:guid}"),
        new("sessions", "sessions", "src/OpenIdentityStack.Api/Sessions/SessionsApi.cs", "api/admin/sessions", ""),
        new("sessionDetail", "sessions/:sessionId", "src/OpenIdentityStack.Api/Sessions/SessionsApi.cs", "api/admin/sessions", "{id:guid}"),
        new("providers", "providers", "src/OpenIdentityStack.Api/Federation/ProvidersApi.cs", "api/admin/providers", ""),
        new("providerDetail", "providers/:providerId", "src/OpenIdentityStack.Api/Federation/ProvidersApi.cs", "api/admin/providers", "{id:guid}"),
        new("authenticationSettings", "providers/settings", "src/OpenIdentityStack.Api/Settings/AuthenticationSettingsApi.cs", "api/admin/authentication-settings", ""),
        new("auditEntries", "audit-entries", "src/OpenIdentityStack.Api/Audit/AuditEntriesApi.cs", "api/admin/audit-entries", ""),
    ];

    [Fact]
    public void CrudRoutes_UseEndpointAuthorizationPolicy()
    {
        string repoRoot = FindRepositoryRoot();
        List<string> mismatches = FindMismatches(repoRoot, fixtureOnly: false);
        mismatches.ShouldBeEmpty(
            "A single-permission CRUD route uses the policy generated from RequireAuthorization. "
            + "A valid permission constant for a different endpoint still fails."
            + Environment.NewLine
            + string.Join(Environment.NewLine, mismatches));

        string fixture = Path.Combine(
            repoRoot,
            "tests",
            "OpenIdentityStack.Architecture.Tests",
            "Fixtures",
            "wrong-crud-route-policy.tsx");
        if (File.Exists(fixture))
        {
            List<string> fixtureMismatches = FindMismatches(repoRoot, fixtureOnly: true);
            fixtureMismatches.ShouldContain(hit => hit.Contains("users:write", StringComparison.Ordinal));
        }

        string script = Path.Combine(repoRoot, "scripts", "ci", "generate-crud-route-policies.py");
        if (File.Exists(script))
        {
            GeneratedMapIsFresh(repoRoot, script);
        }
    }

    private static List<string> FindMismatches(string repoRoot, bool fixtureOnly)
    {
        var mismatches = new List<string>();
        Dictionary<string, string> exports = ReadPermissionExports(repoRoot);
        string generatedMapPath = Path.Combine(
            repoRoot,
            "src",
            "OpenIdentityStack.ManagementWeb",
            "src",
            "lib",
            "generated-crud-route-policies.ts");
        bool generatedMapExists = File.Exists(generatedMapPath);
        Dictionary<string, string> mapMembers = generatedMapExists
            ? ReadGeneratedMap(generatedMapPath)
            : [];
        if (generatedMapExists && !fixtureOnly)
        {
            string[] expectedKeys = Screens.Select(screen => screen.Key).Order(StringComparer.Ordinal).ToArray();
            string[] actualKeys = mapMembers.Keys.Order(StringComparer.Ordinal).ToArray();
            if (!expectedKeys.SequenceEqual(actualKeys, StringComparer.Ordinal))
            {
                mismatches.Add(
                    "src/OpenIdentityStack.ManagementWeb/src/lib/generated-crud-route-policies.ts keys differ from the single-permission CRUD screen list.");
            }
        }

        string routesSource = fixtureOnly
            ? File.ReadAllText(Path.Combine(
                repoRoot,
                "tests",
                "OpenIdentityStack.Architecture.Tests",
                "Fixtures",
                "wrong-crud-route-policy.tsx"))
            : ReadIfExists(Path.Combine(repoRoot, "src", "OpenIdentityStack.ManagementWeb", "src", "routes", "AppRoutes.tsx"));
        string routesRelative = fixtureOnly
            ? "tests/OpenIdentityStack.Architecture.Tests/Fixtures/wrong-crud-route-policy.tsx"
            : "src/OpenIdentityStack.ManagementWeb/src/routes/AppRoutes.tsx";
        List<(string RelativePath, string Source)> navigation = fixtureOnly
            ? []
            : ReadNavigation(repoRoot);

        foreach (CrudScreen screen in Screens)
        {
            if (!TryFindBlock(routesSource, $"path=\"{screen.UiPath}\"", out string routeBlock, out int routeLine))
            {
                continue;
            }

            if (routeBlock.Contains("PlaceholderPage", StringComparison.Ordinal))
            {
                continue;
            }

            string policy = ReadEndpointPolicy(repoRoot, screen, exports);
            string operation = string.IsNullOrEmpty(screen.Template)
                ? $"GET {screen.GroupPath}"
                : $"GET {screen.GroupPath}/{screen.Template}";
            CompareSelection(
                mismatches,
                routesRelative,
                routeLine,
                $"route \"{screen.UiPath}\"",
                routeBlock,
                screen,
                policy,
                operation,
                exports,
                mapMembers,
                generatedMapExists);

            foreach ((string relativePath, string source) in navigation)
            {
                if (!TryFindBlock(source, $"to: '/{screen.UiPath}'", out string navBlock, out int navLine)
                    && !TryFindBlock(source, $"to: \"/{screen.UiPath}\"", out navBlock, out navLine))
                {
                    continue;
                }

                CompareSelection(
                    mismatches,
                    relativePath,
                    navLine,
                    $"link \"/{screen.UiPath}\"",
                    navBlock,
                    screen,
                    policy,
                    operation,
                    exports,
                    mapMembers,
                    generatedMapExists);
            }
        }

        mismatches.Sort(StringComparer.Ordinal);
        return mismatches;
    }

    private static void CompareSelection(
        List<string> mismatches,
        string relativePath,
        int line,
        string subject,
        string block,
        CrudScreen screen,
        string policy,
        string operation,
        IReadOnlyDictionary<string, string> exports,
        IReadOnlyDictionary<string, string> mapMembers,
        bool requireGeneratedMember)
    {
        string[] expressions = ReadPermissionExpressions(block);
        string generatedMember = $"crudRoutePolicies.{screen.Key}";
        bool usesGeneratedMember = expressions.Length == 1
            && string.Equals(expressions[0], generatedMember, StringComparison.Ordinal);
        if (!TryResolve(expressions, exports, mapMembers, out string selected))
        {
            mismatches.Add($"{relativePath}:{line}: {subject} selects {selected}; {operation} requires {policy}.");
            return;
        }

        if (!string.Equals(selected, policy, StringComparison.Ordinal))
        {
            mismatches.Add($"{relativePath}:{line}: {subject} selects {selected}; {operation} requires {policy}.");
            return;
        }

        if (requireGeneratedMember && !usesGeneratedMember)
        {
            mismatches.Add(
                $"{relativePath}:{line}: {subject} must use {generatedMember}. {operation} requires {policy}.");
        }
    }

    private static bool TryResolve(
        string[] expressions,
        IReadOnlyDictionary<string, string> exports,
        IReadOnlyDictionary<string, string> mapMembers,
        out string selected)
    {
        if (expressions.Length == 0)
        {
            selected = "no permission";
            return false;
        }

        var values = new List<string>(expressions.Length);
        foreach (string expression in expressions)
        {
            if (!TryResolveExpression(expression, exports, mapMembers, out string value))
            {
                selected = expression;
                return false;
            }

            values.Add(value);
        }

        selected = string.Join(", ", values);
        return values.Count == 1;
    }

    private static bool TryResolveExpression(
        string expression,
        IReadOnlyDictionary<string, string> exports,
        IReadOnlyDictionary<string, string> mapMembers,
        out string value)
    {
        if (expression.Length >= 2
            && (expression[0] == '\'' || expression[0] == '"')
            && expression[^1] == expression[0])
        {
            value = expression[1..^1];
            return true;
        }

        const string mapPrefix = "crudRoutePolicies.";
        if (expression.StartsWith(mapPrefix, StringComparison.Ordinal))
        {
            string key = expression[mapPrefix.Length..];
            if (mapMembers.TryGetValue(key, out string? export) && exports.TryGetValue(export, out string? mapped))
            {
                value = mapped;
                return true;
            }

            value = expression;
            return false;
        }

        if (expression.StartsWith("managementRoutePermissions.", StringComparison.Ordinal))
        {
            value = expression;
            return false;
        }

        if (exports.TryGetValue(expression, out string? exported))
        {
            value = exported;
            return true;
        }

        value = expression;
        return false;
    }

    private static string[] ReadPermissionExpressions(string block)
    {
        Match permissions = Regex.Match(block, @"permissions=\{\s*\[([^\]]*)\]", RegexOptions.Singleline);
        if (!permissions.Success)
        {
            permissions = Regex.Match(block, @"permissions:\s*\[([^\]]*)\]", RegexOptions.Singleline);
        }

        if (!permissions.Success)
        {
            permissions = Regex.Match(block, @"withPermission\(\s*<[^>]+>\s*,\s*([^)]+)\)", RegexOptions.Singleline);
        }

        if (!permissions.Success)
        {
            return [];
        }

        string inside = permissions.Groups[1].Value.Trim();
        if (inside.StartsWith('[') && inside.EndsWith(']'))
        {
            inside = inside[1..^1];
        }

        return inside.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string ReadEndpointPolicy(string repoRoot, CrudScreen screen, IReadOnlyDictionary<string, string> exports)
    {
        string source = File.ReadAllText(Path.Combine(repoRoot, screen.ApiFile.Replace('/', Path.DirectorySeparatorChar)));
        string groupNeedle = $"MapGroup(\"{screen.GroupPath}\")";
        if (!source.Contains(groupNeedle, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{screen.ApiFile} does not map {screen.GroupPath}.");
        }

        foreach (Match match in Regex.Matches(source, """\bMapGet\(\s*(string\.Empty|"([^"]*)")"""))
        {
            string template = match.Groups[1].Value == "string.Empty" ? "" : match.Groups[2].Value;
            if (!string.Equals(template, screen.Template, StringComparison.Ordinal))
            {
                continue;
            }

            int nextCall = NextEndpointIndex(source, match.Index + match.Length);
            string chain = source[match.Index..nextCall];
            MatchCollection policies = Regex.Matches(chain, @"RequireAuthorization\(\s*(Permissions(?:\.\w+)+)");
            if (policies.Count != 1)
            {
                throw new InvalidOperationException(
                    $"{screen.ApiFile} GET {screen.GroupPath}/{screen.Template} is not a single-permission endpoint.");
            }

            string dotted = policies[0].Groups[1].Value;
            if (!exports.TryGetValue(dotted, out string? value))
            {
                throw new InvalidOperationException($"{dotted} is not a Permissions.cs constant.");
            }

            return value;
        }

        throw new InvalidOperationException($"No MapGet for {screen.GroupPath}/{screen.Template} in {screen.ApiFile}.");
    }

    private static int NextEndpointIndex(string source, int start)
    {
        Match next = Regex.Match(source[start..], @"\bMap(?:Get|Post|Put|Delete|Patch|Group|Methods)\(");
        return next.Success ? start + next.Index : source.Length;
    }

    private static Dictionary<string, string> ReadPermissionExports(string repoRoot)
    {
        string source = File.ReadAllText(Path.Combine(
            repoRoot,
            "src",
            "OpenIdentityStack.Application",
            "Authorization",
            "Permissions.cs"));
        var exports = new Dictionary<string, string>(StringComparer.Ordinal);
        var dotted = new Dictionary<string, string>(StringComparer.Ordinal);
        var classStack = new List<string>();
        int depth = 0;
        string? pendingClass = null;
        foreach (string line in source.Split('\n'))
        {
            Match classMatch = Regex.Match(line, @"\bpublic static class (\w+)");
            Match constMatch = Regex.Match(line, """public const string (\w+) = "([^"]*)";""");
            if (constMatch.Success && pendingClass is null)
            {
                string? owner = classStack.Count == 0 ? null : classStack[^1];
                string member = constMatch.Groups[1].Value;
                string name = ExportName(owner, member);
                exports[name] = constMatch.Groups[2].Value;
                dotted[DottedName(owner, member)] = constMatch.Groups[2].Value;
            }

            if (classMatch.Success)
            {
                pendingClass = classMatch.Groups[1].Value;
            }

            int opens = line.Count(character => character == '{');
            int closes = line.Count(character => character == '}');
            if (pendingClass is not null && opens > 0)
            {
                classStack.Add(pendingClass);
                pendingClass = null;
            }

            depth += opens - closes;
            while (classStack.Count > depth)
            {
                classStack.RemoveAt(classStack.Count - 1);
            }
        }

        foreach ((string name, string value) in dotted)
        {
            exports[name] = value;
        }

        return exports;
    }

    private static Dictionary<string, string> ReadGeneratedMap(string path)
    {
        var members = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in Regex.Matches(File.ReadAllText(path), @"^\s*(\w+):\s*(\w+),", RegexOptions.Multiline))
        {
            members[match.Groups[1].Value] = match.Groups[2].Value;
        }

        return members;
    }

    private static List<(string RelativePath, string Source)> ReadNavigation(string repoRoot)
    {
        string[] relatives =
        [
            "src/OpenIdentityStack.ManagementWeb/src/components/AppShell.tsx",
            "src/OpenIdentityStack.ManagementWeb/src/components/Navigation.tsx",
        ];
        var files = new List<(string RelativePath, string Source)>();
        foreach (string relative in relatives)
        {
            string source = ReadIfExists(Path.Combine(repoRoot, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (source.Length > 0)
            {
                files.Add((relative, source));
            }
        }

        return files;
    }

    private static string ReadIfExists(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path) : "";
    }

    private static bool TryFindBlock(string source, string needle, out string block, out int line)
    {
        int start = source.IndexOf(needle, StringComparison.Ordinal);
        if (start < 0)
        {
            block = "";
            line = 0;
            return false;
        }

        int nextPath = source.IndexOf("path=\"", start + needle.Length, StringComparison.Ordinal);
        int nextLink = source.IndexOf("to: '", start + needle.Length, StringComparison.Ordinal);
        int nextQuotedLink = source.IndexOf("to: \"", start + needle.Length, StringComparison.Ordinal);
        int end = source.Length;
        foreach (int candidate in new[] { nextPath, nextLink, nextQuotedLink })
        {
            if (candidate >= 0 && candidate < end)
            {
                end = candidate;
            }
        }

        block = source[start..end];
        line = source[..start].Count(character => character == '\n') + 1;
        return true;
    }

    private static string ExportName(string? owner, string member)
    {
        if (owner is null || owner == "Permissions")
        {
            return char.ToLowerInvariant(member[0]) + member[1..];
        }

        return char.ToLowerInvariant(owner[0]) + owner[1..] + member;
    }

    private static string DottedName(string? owner, string member)
    {
        if (owner is null || owner == "Permissions")
        {
            return $"Permissions.{member}";
        }

        return $"Permissions.{owner}.{member}";
    }

    private static void GeneratedMapIsFresh(string repoRoot, string script)
    {
        var start = new ProcessStartInfo
        {
            FileName = "python3",
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(script);
        start.ArgumentList.Add("--check");
        using Process process = Process.Start(start)
            ?? throw new InvalidOperationException("Could not start the CRUD route policy generator.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, output + error);
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
