using OpenIdentityStack.Application.Authorization;

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
}
