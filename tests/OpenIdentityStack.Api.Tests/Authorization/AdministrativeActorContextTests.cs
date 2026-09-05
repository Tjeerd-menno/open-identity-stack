using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Application.Abstractions;

namespace OpenIdentityStack.Api.Tests.Authorization;

public sealed class AdministrativeActorContextTests
{
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
}
