using OpenIdentityStack.Application.Authorization;
using System.Text.Json;

namespace OpenIdentityStack.Application.Tests.Authorization;

public sealed class PermissionsMatchingTests
{
    [Theory]
    [InlineData("orders-api:patient:read", "orders-api:patient:read")]
    [InlineData("ORDERS-API:PATIENT:READ", "orders-api:patient:read")]
    public void Matches_ReturnsTrueForExactPermission(string grantedPermission, string requiredPermission)
    {
        Permissions.Matches(grantedPermission, requiredPermission).ShouldBeTrue();
    }

    [Theory]
    [InlineData("users:*", "users:read")]
    [InlineData("orders-api:patient:*", "orders-api:patient:read")]
    [InlineData("ORDERS-API:PATIENT:*", "orders-api:patient:read")]
    public void Matches_ReturnsTrueForOneSegmentPrefixWildcard(string grantedPermission, string requiredPermission)
    {
        Permissions.Matches(grantedPermission, requiredPermission).ShouldBeTrue();
    }

    [Theory]
    [InlineData("users:*", "users:profile:read")]
    [InlineData("orders-api:*", "orders-api:patient:read")]
    [InlineData("orders-api:patient:*", "orders-api:patient:medical-record:read")]
    [InlineData("orders-api:*:read", "orders-api:patient:read")]
    public void Matches_ReturnsFalseWhenWildcardWouldCoverMultipleSegmentsOrIsNotTerminal(
        string grantedPermission,
        string requiredPermission)
    {
        Permissions.Matches(grantedPermission, requiredPermission).ShouldBeFalse();
    }

    [Theory]
    [InlineData("users:read")]
    [InlineData("roles:write")]
    public void Matches_ReturnsTrueForPlatformSuperAdminWildcardAgainstPlatformPermissions(string requiredPermission)
    {
        Permissions.Matches(Permissions.All, requiredPermission).ShouldBeTrue();
    }

    [Theory]
    [InlineData("orders-api:patient:read")]
    [InlineData("orders-api:patient:*")]
    public void Matches_ReturnsFalseForPlatformSuperAdminWildcardAgainstDynamicPermissions(string requiredPermission)
    {
        Permissions.Matches(Permissions.All, requiredPermission).ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(PermissionSemanticsCases))]
    public void Matches_FollowsSharedPermissionSemanticsCases(PermissionSemanticsCase testCase)
    {
        Permissions.Matches(testCase.Granted, testCase.Required).ShouldBe(testCase.Matches, testCase.Name);
    }

    public static TheoryData<PermissionSemanticsCase> PermissionSemanticsCases()
    {
        string path = Path.Combine(FindRepositoryRoot(), "tests", "PermissionSemantics", "permission-semantics-cases.json");
        string json = File.ReadAllText(path);
        PermissionSemanticsCase[] cases =
            JsonSerializer.Deserialize<PermissionSemanticsCase[]>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ??
            [];

        TheoryData<PermissionSemanticsCase> data = [];
        foreach (PermissionSemanticsCase testCase in cases)
        {
            data.Add(testCase);
        }

        return data;
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

    public sealed record PermissionSemanticsCase(
        string Name,
        string Granted,
        string Required,
        bool Matches);
}
