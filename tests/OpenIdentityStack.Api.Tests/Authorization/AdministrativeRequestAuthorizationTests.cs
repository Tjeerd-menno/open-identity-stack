using System.Security.Claims;
using OpenIdentityStack.Testing;
using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Domain.Resources;
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
        var authorization = new AdministrativeRequestAuthorization(evaluator, AdministrativeMfaTestPolicy.Create());
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
        var authorization = new AdministrativeRequestAuthorization(evaluator, AdministrativeMfaTestPolicy.Create());
        var identity = new ClaimsIdentity([new Claim("permission", "users:read")], "mock");
        ClaimsPrincipal principal = ApprovedAdministrativeAccess.Principal(identity);
        identity.AddClaim(new Claim(claimType, value));
        (await authorization.EvaluateAsync(principal)).ShouldBeEmpty();
        evaluator.Request.ShouldBeNull();
    }

    [Theory]
    [InlineData("duplicated-human", "duplicated-human")]
    [InlineData("first-human", "second-human")]
    public async Task DuplicatedHumanSubjectClaimFailsClosed(string first, string second)
    {
        var evaluator = new CapturingEvaluator();
        var authorization = new AdministrativeRequestAuthorization(evaluator, AdministrativeMfaTestPolicy.Create());
        var identity = new ClaimsIdentity([new Claim("permission", "users:read")], "mock");
        // The fixture uses sub == client_id, so dropping the duplicate check would downgrade this
        // ambiguous delegated token into an accepted application token.
        ClaimsPrincipal principal = ApprovedAdministrativeAccess.Principal(identity);
        identity.AddClaims([new Claim("ois_human_subject", first), new Claim("ois_human_subject", second)]);
        (await authorization.EvaluateAsync(principal)).ShouldBeEmpty();
        evaluator.Request.ShouldBeNull();
    }

    [Fact]
    public async Task ApplicationTokenWithoutHumanSubjectClaimIsStillEvaluated()
    {
        var evaluator = new CapturingEvaluator();
        var authorization = new AdministrativeRequestAuthorization(evaluator, AdministrativeMfaTestPolicy.Create());
        ClaimsPrincipal principal = ApprovedAdministrativeAccess.Principal(new ClaimsIdentity([new Claim("permission", "users:read")], "mock"));
        (await authorization.EvaluateAsync(principal)).ShouldBe(["users:read"]);
        evaluator.Request!.UserId.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductionMachineExemptionRequiresTheIssuedApplicationSubjectClassification(bool classified)
    {
        var evaluator = new CapturingEvaluator();
        var authorization = new AdministrativeRequestAuthorization(evaluator, AdministrativeMfaTestPolicy.Create("Production"));
        var identity = new ClaimsIdentity([new Claim("permission", "users:read")], "Bearer");
        ClaimsPrincipal principal = ApprovedAdministrativeAccess.Principal(identity);
        if (classified) { identity.AddClaim(new Claim(TokenSubjectClaims.Kind, TokenSubjectClaims.Application)); }

        IReadOnlyList<string> permissions = await authorization.EvaluateAsync(principal);
        permissions.ShouldBe(classified ? ["users:read"] : Array.Empty<string>());
        evaluator.Calls.ShouldBe(classified ? 1 : 0);
    }

    [Fact]
    public async Task HumanSubjectIsBoundAndRequestAuthorizationIsEvaluatedOnce()
    {
        var evaluator = new CapturingEvaluator();
        var authorization = new AdministrativeRequestAuthorization(evaluator, AdministrativeMfaTestPolicy.Create());
        string userId = Guid.NewGuid().ToString();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new("aud", "urn:openidentitystack:admin-api"), new("scope", "ois.admin"), new("client_id", "approved-client"),
            new("sub", userId), new("ois_human_subject", userId), new("permission", "users:read")], "mock"));
        (await authorization.EvaluateAsync(principal)).ShouldBe(["users:read"]);
        (await authorization.EvaluateAsync(principal)).ShouldBe(["users:read"]);
        evaluator.Request!.UserId!.Value.Value.ToString().ShouldBe(userId);
        evaluator.Calls.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProductionDelegatedBearerRequiresTrustedMfaBeforePermissionLookup(bool withMfa)
    {
        AdministrativeMfaPolicy policy = AdministrativeMfaTestPolicy.Create("Production");
        var evaluator = new CapturingEvaluator();
        var authorization = new AdministrativeRequestAuthorization(evaluator, policy);
        string subject = Guid.NewGuid().ToString();
        var identity = new ClaimsIdentity([
            new("aud", ProtectedResource.AdministrativeAudience), new("scope", ProtectedResource.AdministrativeScope),
            new("client_id", "management-web-client"), new("sub", subject),
            new(AdministrativeActorContext.HumanSubjectClaim, subject), new("permission", "users:read")], "Bearer");
        if (withMfa)
        {
            string? evidence = policy.FederatedEvidence(AdministrativeMfaTestPolicy.ProviderId,
                AdministrativeMfaTestPolicy.Issuer, AdministrativeMfaTestPolicy.Acr);
            identity.AddClaim(new Claim(AdministrativeMfaPolicy.ClaimType, evidence!));
        }

        IReadOnlyList<string> permissions = await authorization.EvaluateAsync(new ClaimsPrincipal(identity));
        permissions.ShouldBe(withMfa ? ["users:read"] : Array.Empty<string>());
        evaluator.Calls.ShouldBe(withMfa ? 1 : 0);
    }

    [Fact]
    public async Task CurrentEntitlementDenialOverridesPermissionClaim()
    {
        var evaluator = new CapturingEvaluator { Deny = true };
        var authorization = new AdministrativeRequestAuthorization(evaluator, AdministrativeMfaTestPolicy.Create());
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
