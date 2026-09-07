using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Api.Federation;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Federation.Commands;

namespace OpenIdentityStack.Api.Tests.Admin;

public sealed class ProviderRejectionAuditTests
{
    [Theory]
    [InlineData(ClaimTypes.NameIdentifier, true)]
    [InlineData("sub", true)]
    [InlineData(ClaimTypes.NameIdentifier, false)]
    [InlineData("sub", false)]
    public async Task AuthorityRejectionRetainsAuthenticatedOperator(string claimType, bool human)
    {
        string actor = Guid.NewGuid().ToString();
        var providerId = Guid.NewGuid();
        var identity = new ClaimsIdentity([new Claim(claimType, actor)], "authenticated");
        if (human) { identity.AddClaim(new Claim(AdministrativeActorContext.HumanSubjectClaim, actor)); }
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        IAuditLog audit = Substitute.For<IAuditLog>();
        IUpdateProviderUseCase update = Substitute.For<IUpdateProviderUseCase>();
        Type api = typeof(PermissionRequirement).Assembly.GetType("OpenIdentityStack.Api.Federation.ProvidersApi")!;
        MethodInfo handler = api.GetMethod("UpdateProvider", BindingFlags.NonPublic | BindingFlags.Static)!;

        IResult result = await (Task<IResult>)handler.Invoke(null,
            [update, audit, context, providerId, new UpdateProviderRequest { Authority = "https://replacement.example" }, CancellationToken.None])!;

        ((IStatusCodeHttpResult)result).StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        await audit.Received(1).LogAsync(human ? actor : "client:" + actor, "Federation.AuthorityReplacementRejected", "UpstreamProvider", providerId.ToString(),
            Arg.Any<string>(), Arg.Any<CancellationToken>());
        await update.DidNotReceive().ExecuteAsync(Arg.Any<UpdateProviderCommand>(), Arg.Any<CancellationToken>());
    }
}
