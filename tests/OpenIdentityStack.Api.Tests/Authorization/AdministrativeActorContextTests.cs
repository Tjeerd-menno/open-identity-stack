using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Application.Abstractions;

namespace OpenIdentityStack.Api.Tests.Authorization;

public sealed class AdministrativeActorContextTests
{
    [Fact]
    public void AuditActorNamespacesSeparateClientsHumansAndBackgroundWork()
    {
        string humanId = Guid.NewGuid().ToString();
        string[] clientIds = ["system", "client:system", humanId, "authenticated:unknown", "sha256:" + new string('a', 64), new string('x', 255)];
        var recorded = new List<string>();
        foreach (string clientId in clientIds)
        {
            var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", clientId)], "Bearer")) };
            var actor = new AdministrativeActorContext(new HttpContextAccessor { HttpContext = context });
            actor.AuditActorId.ShouldBe("client:" + clientId);
            var entry = new OpenIdentityStack.Infrastructure.Audit.AuditLogEntry { UserId = actor.AuditActorId };
            entry.UserId.Length.ShouldBeLessThanOrEqualTo(128);
            recorded.Add(entry.UserId);
        }
        var human = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", humanId), new Claim(AdministrativeActorContext.HumanSubjectClaim, humanId)], "Bearer")) };
        var humanActor = new AdministrativeActorContext(new HttpContextAccessor { HttpContext = human });
        humanActor.AuditActorId.ShouldBe(humanId);
        humanActor.Current!.IsHuman.ShouldBeTrue();
        recorded.Add(humanActor.AuditActorId);
        var background = new AdministrativeActorContext(new HttpContextAccessor { HttpContext = null });
        background.AuditActorId.ShouldBe("system");
        recorded.Add(background.AuditActorId);
        recorded.Distinct(StringComparer.Ordinal).Count().ShouldBe(recorded.Count);
    }

    [Theory]
    [InlineData(null, false, false)]
    [InlineData("another-user", false, false)]
    [InlineData("self", true, true)]
    public void HumanProofIsBoundToActingSubject(string? proofSubject, bool human, bool fresh)
    {
        var id = Guid.NewGuid();
        var claims = new List<Claim> { new("sub", id.ToString()) };
        if (proofSubject is not null)
        {
            claims.Add(new(AdministrativeActorContext.HumanSubjectClaim, proofSubject == "self" ? id.ToString() : proofSubject));
            claims.Add(new(AdministrativeActorContext.HumanAuthenticationClaim, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));
        }
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")) };
        var adapter = new AdministrativeActorContext(new HttpContextAccessor { HttpContext = http });
        AdministrativeActor actor = adapter.Current!;
        actor.IsHuman.ShouldBe(human);
        (actor.IsHuman && actor.AuthenticatedAt.HasValue).ShouldBe(fresh);
    }

    [Theory]
    [InlineData("not-a-time")]
    [InlineData("9223372036854775807")]
    public void InvalidAuthenticationTimeCannotEstablishFreshness(string authenticationTime)
    {
        var id = Guid.NewGuid();
        Claim[] claims =
        [
            new("sub", id.ToString()),
            new(AdministrativeActorContext.HumanSubjectClaim, id.ToString()),
            new(AdministrativeActorContext.HumanAuthenticationClaim, authenticationTime),
        ];
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")) };
        var adapter = new AdministrativeActorContext(new HttpContextAccessor { HttpContext = http });
        adapter.Current!.AuthenticatedAt.ShouldBeNull();
    }

    [Fact]
    public void DuplicateAuthenticationTimeIsUntrustworthy()
    {
        var id = Guid.NewGuid();
        Claim[] claims =
        [
            new("sub", id.ToString()),
            new(AdministrativeActorContext.HumanSubjectClaim, id.ToString()),
            new(AdministrativeActorContext.HumanAuthenticationClaim, "1"),
            new(AdministrativeActorContext.HumanAuthenticationClaim, "2"),
        ];
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")) };
        var adapter = new AdministrativeActorContext(new HttpContextAccessor { HttpContext = http });
        adapter.Current!.AuthenticatedAt.ShouldBeNull();
    }
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void IndependentLoginMarkerRequiresExactlyOneProtectedSession(bool duplicate, bool accepted)
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new(AdministrativeActorContext.HumanSubjectClaim, userId.ToString()),
            new(OpenIdentityStack.Application.Authorization.IndependentAuthenticationClaims.LocalPasswordSession, sessionId.ToString()),
            new(CredentialBoundaryClaims.Epoch, Guid.Empty.ToString())
        };
        if (duplicate)
        {
            claims.Add(new(OpenIdentityStack.Application.Authorization.IndependentAuthenticationClaims.LocalPasswordSession, sessionId.ToString()));
        }
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")) };
        AdministrativeActor actor = new AdministrativeActorContext(new HttpContextAccessor { HttpContext = http }).Current!;
        actor.LocalPasswordSessionId.HasValue.ShouldBe(accepted);
        actor.CredentialEpoch.ShouldBe(Guid.Empty);
    }}
