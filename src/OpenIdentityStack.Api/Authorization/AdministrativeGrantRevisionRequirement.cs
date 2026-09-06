using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OpenIddict.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Domain.Resources;
using OpenIdentityStack.Domain.Users;
using SharedKernel;

namespace OpenIdentityStack.Api.Authorization;

public sealed class AdministrativeGrantRevisionRequirement : IAuthorizationRequirement;

public sealed class AdministrativeGrantRevisionHandler(IResourcePermissionService permissions)
    : AuthorizationHandler<AdministrativeGrantRevisionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdministrativeGrantRevisionRequirement requirement)
    {
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            return;
        }

        string? clientId = context.User.FindFirstValue(OpenIddictConstants.Claims.ClientId);
        string? actorType = context.User.FindFirstValue(ResourceTokenActorTypes.ClaimType);
        UserId? userId = actorType == ResourceTokenActorTypes.User
            && Guid.TryParse(context.User.FindFirstValue(OpenIddictConstants.Claims.Subject), out Guid subject)
                ? new UserId(subject)
                : null;
        if (string.IsNullOrWhiteSpace(clientId)
            || actorType is not (ResourceTokenActorTypes.User or ResourceTokenActorTypes.Application)
            || (actorType == ResourceTokenActorTypes.User && userId is null))
        {
            return;
        }

        string[] audiences = context.User.GetAudiences().ToArray();
        Claim[] revisionClaims = context.User.FindAll("ois.grant_revision").ToArray();
        if (audiences.Length != 1
            || !string.Equals(audiences[0], ProtectedResource.AdministrativeAudience, StringComparison.Ordinal)
            || revisionClaims.Length != 1
            || !TryParseRevision(revisionClaims[0].Value, out Guid resourceId, out long tokenRevision)
            || resourceId != ProtectedResource.AdministrativeResourceId)
        {
            return;
        }

        Result<ResourceTokenProjection> current = await permissions.ProjectAsync(new ResourceTokenRequest(
            clientId,
            context.User.GetScopes(),
            audiences,
            userId,
            OriginalPermissions: null,
            audiences));
        if (current.IsFailure
            || !current.Value.GrantRevisions.TryGetValue(ProtectedResource.AdministrativeResourceId, out long currentRevision)
            || currentRevision != tokenRevision
            || context.Requirements.OfType<PermissionRequirement>().Any(permissionRequirement =>
                !current.Value.Permissions.Any(permission => Permissions.Matches(permission, permissionRequirement.Permission))))
        {
            return;
        }

        context.Succeed(requirement);
    }

    private static bool TryParseRevision(string value, out Guid resourceId, out long revision)
    {
        resourceId = Guid.Empty;
        revision = 0;
        int separator = value.LastIndexOf(':');
        return separator > 0
            && Guid.TryParse(value.AsSpan(0, separator), out resourceId)
            && long.TryParse(value.AsSpan(separator + 1), out revision)
            && revision > 0;
    }
}
