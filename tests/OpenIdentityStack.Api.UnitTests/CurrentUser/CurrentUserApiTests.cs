using System.Security.Claims;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging.Abstractions;

using OpenIdentityStack.Api.CurrentUser;

namespace OpenIdentityStack.Api.UnitTests.CurrentUser;

public sealed class CurrentUserApiTests
{
    [Fact]
    public void CreateResponse_ReadsCurrentUserFromValidatedPrincipal()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim("sub", "user-123"),
            new Claim("name", "Ada Lovelace"),
            new Claim("preferred_username", "ada"),
            new Claim("email", "ada@example.com"),
            new Claim("permission", "users:read"),
            new Claim("permissions", "roles:read"),
            new Claim("permission", "USERS:READ"),
            new Claim("scope", "applications:read"));

        IResult result = CurrentUserApi.CreateResponse(principal, ["users:read"], NullLoggerFactory.Instance);

        Ok<CurrentUserResponse> ok = result.ShouldBeOfType<Ok<CurrentUserResponse>>();
        ok.Value.ShouldNotBeNull();
        ok.Value.Subject.ShouldBe("user-123");
        ok.Value.UserName.ShouldBe("ada");
        ok.Value.DisplayName.ShouldBe("Ada Lovelace");
        ok.Value.Email.ShouldBe("ada@example.com");
        ok.Value.Permissions.ShouldBe(["users:read"]);
    }

    [Fact]
    public void CreateResponse_FallsBackDisplayFieldsToSubject()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim(ClaimTypes.NameIdentifier, "user-456"),
            new Claim("permission", "*"));

        IResult result = CurrentUserApi.CreateResponse(principal, ["users:read"], NullLoggerFactory.Instance);

        CurrentUserResponse response = result.ShouldBeOfType<Ok<CurrentUserResponse>>().Value.ShouldNotBeNull();
        response.Subject.ShouldBe("user-456");
        response.UserName.ShouldBe("user-456");
        response.DisplayName.ShouldBe("user-456");
        response.Email.ShouldBeNull();
        response.Permissions.ShouldBe(["users:read"]);
    }

    [Fact]
    public void CreateResponse_UsesEvaluatedPermissionsInsteadOfTokenClaims()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim("sub", "user-789"),
            new Claim("permission", "users:read"),
            new Claim("permissions", "roles:read"),
            new Claim("permission", "groups:read"));

        IResult result = CurrentUserApi.CreateResponse(principal, ["users:read"], NullLoggerFactory.Instance);

        CurrentUserResponse response = result.ShouldBeOfType<Ok<CurrentUserResponse>>().Value.ShouldNotBeNull();
        response.Permissions.ShouldBe(["users:read"]);
    }

    [Fact]
    public void CreateResponse_UsesEvaluatedPermissionSpelling()
    {
        ClaimsPrincipal principal = CreatePrincipal(
            new Claim("sub", "user-790"),
            new Claim("permissions", "Users:Read"),
            new Claim("permission", "users:read"));

        IResult result = CurrentUserApi.CreateResponse(principal, ["users:read"], NullLoggerFactory.Instance);

        CurrentUserResponse response = result.ShouldBeOfType<Ok<CurrentUserResponse>>().Value.ShouldNotBeNull();
        response.Permissions.ShouldBe(["users:read"]);
    }

    [Fact]
    public void CreateResponse_ReturnsUnauthorizedWhenSubjectIsMissing()
    {
        ClaimsPrincipal principal = CreatePrincipal(new Claim("permission", "users:read"));

        IResult result = CurrentUserApi.CreateResponse(principal, ["users:read"], NullLoggerFactory.Instance);

        result.ShouldBeOfType<UnauthorizedHttpResult>();
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "test"));
}
