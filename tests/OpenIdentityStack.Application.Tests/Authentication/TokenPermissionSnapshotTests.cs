using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authentication;
using OpenIdentityStack.Application.Groups;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Roles;

namespace OpenIdentityStack.Application.Tests.Authentication;

/// <summary>
/// Authorize and refresh resolve effective permissions once, through ITokenPermissionSnapshot.
/// At 119d7d7028a703286a610e66ec5b577dc1bf6ca0, Authorize calls the roles handler and then
/// ProjectResourcesAsync without those permissions, so projection resolves them again.
/// Name-over-GUID precedence from 88eac46 stays.
/// </summary>
public sealed class TokenPermissionSnapshotTests
{
    [Fact]
    public async Task AuthorizeAndRefreshResolveEffectivePermissionsOnceAndKeepNameOverGuidPrecedence()
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "OpenIdentityStack.Api", "Authentication", "AuthorizationController.cs"));
        var sites = new List<string>();
        sites.AddRange(ResolutionSites(source, "public async Task<IActionResult> Authorize()", "public async Task<IActionResult> Exchange()"));
        sites.AddRange(ResolutionSites(source, "public async Task<IActionResult> Exchange()", "public async Task<IActionResult> UserInfo()"));
        var extra = sites
            .Where(static site => !site.Contains("tokenPermissionSnapshot.GetAsync", StringComparison.Ordinal))
            .ToList();
        extra.Sort(StringComparer.Ordinal);
        extra.ShouldBeEmpty(
            "Authorize and refresh resolve effective permissions once through tokenPermissionSnapshot.GetAsync."
            + Environment.NewLine
            + string.Join(Environment.NewLine, extra));

        string authorize = MethodBody(source, "public async Task<IActionResult> Authorize()", "public async Task<IActionResult> Exchange()");
        string exchange = MethodBody(source, "public async Task<IActionResult> Exchange()", "public async Task<IActionResult> UserInfo()");
        Count(authorize, "tokenPermissionSnapshot.GetAsync").ShouldBe(1);
        Count(exchange, "tokenPermissionSnapshot.GetAsync").ShouldBe(1);

        IRoleRepository roles = Substitute.For<IRoleRepository>();
        IGroupRepository groups = Substitute.For<IGroupRepository>();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        Role other = Role.Create("other", null).Value;
        Role named = Role.Create(other.Id.Value.ToString(), null).Value;
        named.AddPermission("*");
        var userId = new UserId(Guid.NewGuid());
        Group group = Group.Create("mapped", null, clock).Value;
        group.AddMapping(MappingType.Role, named.Name, null, TokenTarget.Both, clock);
        roles.GetUserRolesAsync(userId, true, Arg.Any<CancellationToken>()).Returns(Array.Empty<Role>());
        groups.GetGroupsForUserAsync(userId, Arg.Any<CancellationToken>()).Returns(new[] { group });
        roles.GetByNamesAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>()).Returns(new[] { named });
        roles.GetByIdsAsync(Arg.Any<IEnumerable<RoleId>>(), Arg.Any<CancellationToken>()).Returns(new[] { other });
        var snapshot = new TokenPermissionSnapshot(new GetUserEffectiveRolesQueryHandler(roles, new RequestScopedUserGroupsProvider(groups)));

        Result<TokenPermissionSnapshotResult> result = await snapshot.GetAsync(userId);

        result.Value.Roles.Single().Id.ShouldBe(named.Id.Value);
        result.Value.Permissions.ShouldBe(["*"]);
        await roles.DidNotReceive().GetByIdsAsync(Arg.Any<IEnumerable<RoleId>>(), Arg.Any<CancellationToken>());
    }

    private static List<string> ResolutionSites(string source, string startSignature, string endSignature)
    {
        string body = MethodBody(source, startSignature, endSignature);
        int bodyStart = source.IndexOf(startSignature, StringComparison.Ordinal);
        var sites = new List<string>();
        AddSites(source, body, bodyStart, "EffectiveRoles", sites);
        AddSites(source, body, bodyStart, "tokenPermissionSnapshot.GetAsync", sites);
        int search = 0;
        while ((search = body.IndexOf("ProjectResourcesAsync", search, StringComparison.Ordinal)) >= 0)
        {
            int open = body.IndexOf('(', search);
            string arguments = ReadBalanced(body, open);
            if (CountsAsRoleResolution(arguments))
            {
                int line = LineNumber(source, bodyStart + search);
                sites.Add($"AuthorizationController.cs:{line}: ProjectResourcesAsync without the snapshot permissions");
            }

            search = open + 1;
        }

        return sites;
    }

    private static void AddSites(string source, string body, int bodyStart, string needle, List<string> sites)
    {
        int search = 0;
        while ((search = body.IndexOf(needle, search, StringComparison.Ordinal)) >= 0)
        {
            int line = LineNumber(source, bodyStart + search);
            sites.Add($"AuthorizationController.cs:{line}: {needle}");
            search += needle.Length;
        }
    }

    private static bool CountsAsRoleResolution(string arguments)
    {
        List<string> args = TopLevelArguments(arguments);
        if (args.Count < 3 || args[2].Trim() == "null")
        {
            return false;
        }

        return !arguments.Contains("userPermissions", StringComparison.Ordinal);
    }

    private static List<string> TopLevelArguments(string arguments)
    {
        var args = new List<string>();
        int start = 0;
        int depth = 0;
        for (int index = 0; index < arguments.Length; index++)
        {
            char current = arguments[index];
            if (current is '(' or '[')
            {
                depth++;
            }
            else if (current is ')' or ']')
            {
                depth--;
            }
            else if (current == ',' && depth == 0)
            {
                args.Add(arguments[start..index]);
                start = index + 1;
            }
        }

        args.Add(arguments[start..]);
        return args;
    }

    private static string ReadBalanced(string source, int open)
    {
        int depth = 0;
        for (int index = open; index < source.Length; index++)
        {
            if (source[index] == '(')
            {
                depth++;
            }
            else if (source[index] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return source[(open + 1)..index];
                }
            }
        }

        throw new InvalidOperationException("Unbalanced call.");
    }

    private static string MethodBody(string source, string startSignature, string endSignature)
    {
        int start = source.IndexOf(startSignature, StringComparison.Ordinal);
        int end = source.IndexOf(endSignature, start + startSignature.Length, StringComparison.Ordinal);
        if (start < 0 || end < 0)
        {
            throw new InvalidOperationException($"Could not find {startSignature}");
        }

        return source[start..end];
    }

    private static int Count(string body, string needle)
    {
        int count = 0;
        int search = 0;
        while ((search = body.IndexOf(needle, search, StringComparison.Ordinal)) >= 0)
        {
            count++;
            search += needle.Length;
        }

        return count;
    }

    private static int LineNumber(string source, int index)
    {
        int line = 1;
        for (int cursor = 0; cursor < index && cursor < source.Length; cursor++)
        {
            if (source[cursor] == '\n')
            {
                line++;
            }
        }

        return line;
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
