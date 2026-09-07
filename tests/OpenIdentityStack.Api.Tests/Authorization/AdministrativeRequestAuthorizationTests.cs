using System.Security.Claims;
using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Application.Abstractions;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Authorization;

public sealed class AdministrativeRequestAuthorizationTests
{
    [Theory]
    [InlineData("permissions")]
    [InlineData("scope")]
    [InlineData("scp")]
    [InlineData("role")]
    public async Task AlternateClaimsCannotSupplyAdministrativePermission(string claimType)
    {
        var evaluator = new CapturingEvaluator();
        var authorization = new AdministrativeRequestAuthorization(evaluator);
        ClaimsPrincipal principal = ApprovedAdministrativeAccess.Principal(new ClaimsIdentity([new Claim(claimType, "users:read")], "mock"));
        (await authorization.EvaluateAsync(principal)).ShouldBeEmpty();
        evaluator.Request!.TokenPermissions.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("client_id", "second-client")]
    [InlineData("sub", "second-subject")]
    [InlineData("aud", "urn:business")]
    [InlineData("ois_human_subject", "different-human")]
    public async Task AmbiguousOrUnboundIdentityDeniesBeforeEntitlementLookup(string claimType, string value)
    {
        var evaluator = new CapturingEvaluator();
        var authorization = new AdministrativeRequestAuthorization(evaluator);
        var identity = new ClaimsIdentity([new Claim("permission", "users:read")], "mock");
        ClaimsPrincipal principal = ApprovedAdministrativeAccess.Principal(identity);
        identity.AddClaim(new Claim(claimType, value));
        (await authorization.EvaluateAsync(principal)).ShouldBeEmpty();
        evaluator.Request.ShouldBeNull();
    }

    [Fact]
    public async Task HumanSubjectIsBoundAndRequestAuthorizationIsEvaluatedOnce()
    {
        var evaluator = new CapturingEvaluator();
        var authorization = new AdministrativeRequestAuthorization(evaluator);
        string userId = Guid.NewGuid().ToString();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new("aud", "urn:openidentitystack:admin-api"), new("scope", "ois.admin"), new("client_id", "approved-client"),
            new("sub", userId), new("ois_human_subject", userId), new("permission", "users:read")], "mock"));
        (await authorization.EvaluateAsync(principal)).ShouldBe(["users:read"]);
        (await authorization.EvaluateAsync(principal)).ShouldBe(["users:read"]);
        evaluator.Request!.UserId!.Value.Value.ToString().ShouldBe(userId);
        evaluator.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task CurrentEntitlementDenialOverridesPermissionClaim()
    {
        var evaluator = new CapturingEvaluator { Deny = true };
        var authorization = new AdministrativeRequestAuthorization(evaluator);
        ClaimsPrincipal principal = ApprovedAdministrativeAccess.Principal(new ClaimsIdentity([new Claim("permission", "*")], "mock"));
        (await authorization.EvaluateAsync(principal)).ShouldBeEmpty();
    }

    private sealed class CapturingEvaluator : IAdministrativeAccessEvaluator
    {
        public AdministrativeAccessRequest? Request { get; private set; }
        public int Calls { get; private set; }
        public bool Deny { get; init; }
        public Task<Result<IReadOnlyList<string>>> EvaluateAsync(AdministrativeAccessRequest request, CancellationToken cancellationToken = default)
        {
            this.Request = request;
            this.Calls++;
            Result<IReadOnlyList<string>> result = this.Deny
                ? DomainError.Forbidden("AdministrativeAccess.NotApproved", "Client is not approved.")
                : request.TokenPermissions.ToList();
            return Task.FromResult(result);
        }
    }
}
