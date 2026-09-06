using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using OpenIddict.Abstractions;
using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.AdministrativeAccess;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Resources;
using SharedKernel;

namespace OpenIdentityStack.Api.UnitTests.Authorization;

public sealed class AdministrativeGrantRevisionRequirementTests
{
    [Fact]
    public async Task AdministrativeRequirementsReuseOneCurrentPermissionProjection()
    {
        const long revision = 7;
        var identity = new ClaimsIdentity(
        [
            new(OpenIddictConstants.Claims.ClientId, "management-client"),
            new(ResourceTokenActorTypes.ClaimType, ResourceTokenActorTypes.Application),
            new(OpenIddictConstants.Claims.Subject, "management-client"),
            new(OpenIddictConstants.Claims.Scope, ProtectedResource.AdministrativeScope),
            new("permission", Permissions.Users.Read),
            new("ois.grant_revision", $"{ProtectedResource.AdministrativeResourceId:D}:{revision}")
        ], "Bearer");
        ClaimsPrincipal principal = new(identity);
        principal.SetAudiences(ProtectedResource.AdministrativeAudience);
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Audience, ProtectedResource.AdministrativeAudience));
        var permissions = new CountingResourcePermissionService(new ResourceTokenProjection(
            [ProtectedResource.AdministrativeAudience],
            [Permissions.Users.Read],
            new Dictionary<Guid, long> { [ProtectedResource.AdministrativeResourceId] = revision }));
        var administrativeAccess = new AdministrativeRequestAuthorization(new AdministrativeAccessEvaluator(
            permissions, new StubAdministrativeAuthoritySnapshot()));
        var revisionRequirement = new AdministrativeGrantRevisionRequirement();
        var accessRequirement = new AdministrativeAccessRequirement();
        var permissionRequirement = new PermissionRequirement(Permissions.Users.Read);
        AuthorizationHandlerContext context = new(
            [revisionRequirement, accessRequirement, permissionRequirement], principal, null);

        await new AdministrativeGrantRevisionHandler(administrativeAccess).HandleAsync(context);
        permissions.Calls.ShouldBe(1);
        await new AdministrativeAccessAuthorizationHandler(administrativeAccess).HandleAsync(context);
        permissions.Calls.ShouldBe(1);
        await new PermissionAuthorizationHandler(administrativeAccess).HandleAsync(context);

        permissions.Calls.ShouldBe(1);
        context.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task RevokedUserPermissionIsDeniedWhenApplicationGrantRevisionIsUnchanged()
    {
        var userId = UserId.Create();
        const long unchangedRevision = 7;
        Claim[] claims =
        [
            new(OpenIddictConstants.Claims.ClientId, "management-client"),
            new(ResourceTokenActorTypes.ClaimType, ResourceTokenActorTypes.User),
            new(OpenIddictConstants.Claims.Subject, userId.Value.ToString()),
            new(AdministrativeActorContext.HumanSubjectClaim, userId.Value.ToString()),
            new(OpenIddictConstants.Claims.Scope, ProtectedResource.AdministrativeScope),
            new("permission", Permissions.Users.Read),
            new("ois.grant_revision", $"{ProtectedResource.AdministrativeResourceId:D}:{unchangedRevision}")
        ];
        ClaimsPrincipal principal = new(new ClaimsIdentity(claims, "Bearer"));
        principal.SetAudiences(ProtectedResource.AdministrativeAudience);
        ((ClaimsIdentity)principal.Identity!).AddClaim(
            new Claim(OpenIddictConstants.Claims.Audience, ProtectedResource.AdministrativeAudience));
        IResourcePermissionService permissions = new StubResourcePermissionService(new ResourceTokenProjection(
            [ProtectedResource.AdministrativeAudience],
            [],
            new Dictionary<Guid, long>
            {
                [ProtectedResource.AdministrativeResourceId] = unchangedRevision
            }));
        var revisionRequirement = new AdministrativeGrantRevisionRequirement();
        var permissionRequirement = new PermissionRequirement(Permissions.Users.Read);
        AuthorizationHandlerContext context = new([revisionRequirement, permissionRequirement], principal, null);
        var administrativeAccess = new AdministrativeRequestAuthorization(new AdministrativeAccessEvaluator(
            permissions, new StubAdministrativeAuthoritySnapshot()));

        await new AdministrativeGrantRevisionHandler(administrativeAccess).HandleAsync(context);
        await new PermissionAuthorizationHandler(administrativeAccess).HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    private sealed class StubResourcePermissionService(ResourceTokenProjection projection) : IResourcePermissionService
    {
        public Task<Result<ResourceTokenProjection>> ProjectAsync(
            ResourceTokenRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Result<ResourceTokenProjection>>(projection);
    }

    private sealed class CountingResourcePermissionService(ResourceTokenProjection projection) : IResourcePermissionService
    {
        public int Calls { get; private set; }

        public Task<Result<ResourceTokenProjection>> ProjectAsync(
            ResourceTokenRequest request,
            CancellationToken cancellationToken = default)
        {
            this.Calls++;
            return Task.FromResult<Result<ResourceTokenProjection>>(projection);
        }
    }

    private sealed class StubAdministrativeAuthoritySnapshot : IAdministrativeAuthoritySnapshot
    {
        public Task CaptureAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

}
