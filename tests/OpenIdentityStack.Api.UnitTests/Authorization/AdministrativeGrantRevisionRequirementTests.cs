using System.Security.Claims;

using Microsoft.AspNetCore.Authorization;
using OpenIddict.Abstractions;
using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Resources;
using SharedKernel;

namespace OpenIdentityStack.Api.UnitTests.Authorization;

public sealed class AdministrativeGrantRevisionRequirementTests
{
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
            new("permission", Permissions.Users.Read),
            new("ois.grant_revision", $"{ProtectedResource.AdministrativeResourceId:D}:{unchangedRevision}")
        ];
        ClaimsPrincipal principal = new(new ClaimsIdentity(claims, "Bearer"));
        principal.SetAudiences(ProtectedResource.AdministrativeAudience);
        principal.SetScopes(ProtectedResource.AdministrativeAudience);
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

        await new AdministrativeGrantRevisionHandler(permissions).HandleAsync(context);
        var administrativeAccess = new AdministrativeRequestAuthorization(new TokenPermissionEvaluator());
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

    private sealed class TokenPermissionEvaluator : OpenIdentityStack.Application.Abstractions.IAdministrativeAccessEvaluator
    {
        public Task<Result<IReadOnlyList<string>>> EvaluateAsync(
            OpenIdentityStack.Application.Abstractions.AdministrativeAccessRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult((Result<IReadOnlyList<string>>)request.TokenPermissions.ToList());
    }
}
