using OpenIdentityStack.Application.Authorization;

namespace OpenIdentityStack.Application.Tests.Authorization;

public sealed class PermissionGrantIndexTests
{
    private static readonly string[] GrantedPermissions =
    [
        "users:read",
        "Users:Read",
        "USERS:READ",
        "users:*",
        "Users:*",
        " users:* ",
        "roles:*",
        "*",
        " * ",
        string.Empty,
        "   ",
        "orders-api:read",
        "orders-api:patient:*",
        "orders-api:*",
        "orders-api:*:read",
        "a:*/x",
        "*:",
        "**",
        "*a",
        "a*",
        "users:*x",
        ":*",
        ":",
        "a::*",
        "*:*",
        " users:read ",
        "x",
        "x:y:z",
    ];

    private static readonly string[] RequiredPermissions =
    [
        "users:read",
        "Users:Read",
        "users:disable",
        "users:profile:read",
        "users:",
        "users",
        "users:*",
        "*",
        string.Empty,
        "   ",
        " users:read ",
        "roles:read",
        "orders-api:read",
        "orders-api:patient:read",
        "orders-api:patient:medical-record:read",
        "orders-api:patient:read:x",
        "a:b",
        "a::b",
        "a:*/x",
        "*:",
        ":",
        "x",
        "x:y:z",
        "x:y:z:w",
    ];

    [Fact]
    public void Covers_AgreesWithPermissionSemanticsForSingleGrant()
    {
        foreach (string granted in GrantedPermissions)
        {
            var index = PermissionGrantIndex.Create([granted]);

            foreach (string required in RequiredPermissions)
            {
                bool expected = PermissionSemantics.Matches(granted, required);
                bool actual = index.Covers(required);

                actual.ShouldBe(expected, $"granted '{granted}' required '{required}'");
            }
        }
    }

    [Fact]
    public void Covers_AgreesWithPermissionSemanticsForCombinedGrants()
    {
        var index = PermissionGrantIndex.Create(GrantedPermissions);

        foreach (string required in RequiredPermissions)
        {
            bool expected = GrantedPermissions.Any(granted => PermissionSemantics.Matches(granted, required));
            bool actual = index.Covers(required);

            actual.ShouldBe(expected, $"required '{required}'");
        }
    }

    [Fact]
    public void Covers_ReturnsFalseForEmptyOrNullGrantSets()
    {
        var empty = PermissionGrantIndex.Create([]);

        foreach (string required in RequiredPermissions)
        {
            empty.Covers(required).ShouldBeFalse();
        }

        var nullIndex = PermissionGrantIndex.Create(null!);

        foreach (string required in RequiredPermissions)
        {
            nullIndex.Covers(required).ShouldBeFalse();
        }
    }

    [Theory]
    [InlineData("users:*", "users:disable")]
    [InlineData("orders-api:patient:*", "orders-api:patient:read")]
    [InlineData("orders-api:patient:*", "orders-api:patient:medical-record:read")]
    [InlineData("*", "users:read")]
    [InlineData("*", "orders-api:patient:read")]
    [InlineData("*", "*")]
    public void Covers_FollowsSharedPermissionSemantics(string granted, string required)
    {
        var index = PermissionGrantIndex.Create([granted]);

        index.Covers(required).ShouldBe(PermissionSemantics.Matches(granted, required));
    }
}
