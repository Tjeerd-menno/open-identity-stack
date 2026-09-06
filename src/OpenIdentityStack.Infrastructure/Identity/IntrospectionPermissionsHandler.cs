using System.Collections.Immutable;
using System.Security.Claims;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Domain.Resources;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Identity;

internal sealed class IntrospectionPermissionsHandler(IResourcePermissionService projection, IResourceAccessRepository resources, IApplicationRepository applications) :
    IOpenIddictServerHandler<OpenIddictServerEvents.HandleIntrospectionRequestContext>
{
    public async ValueTask HandleAsync(OpenIddictServerEvents.HandleIntrospectionRequestContext context)
    {
        ClaimsPrincipal? principal = context.GenericTokenPrincipal;
        string? clientId = principal?.GetClaim(OpenIddictConstants.Claims.ClientId);
        Domain.Applications.Application? caller = await applications.GetByClientIdAsync(context.Request.ClientId ?? string.Empty, context.CancellationToken);
        if (principal is null || string.IsNullOrEmpty(clientId) || caller is null || caller.Status != Domain.Applications.ApplicationStatus.Active)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidToken);
            return;
        }

        ImmutableArray<string> audiences = principal.GetAudiences();
        if (audiences.IsEmpty) { audiences = principal.GetResources(); }
        foreach (string audience in audiences)
        {
            ProtectedResource? resource = await resources.FindByAudienceAsync(audience, context.CancellationToken);
            if (resource is null || await resources.GetGrantAsync(caller.Id, resource.Id, context.CancellationToken) is null)
            {
                context.Reject(OpenIddictConstants.Errors.InvalidToken);
                return;
            }
        }

        string? subject = principal.GetClaim(OpenIddictConstants.Claims.Subject);
        string? actorType = principal.GetClaim(ResourceTokenActorTypes.ClaimType);
        if (actorType is not (ResourceTokenActorTypes.User or ResourceTokenActorTypes.Application))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidToken);
            return;
        }

        UserId? userId = actorType == ResourceTokenActorTypes.User && Guid.TryParse(subject, out Guid value)
            ? new UserId(value)
            : null;
        if (actorType == ResourceTokenActorTypes.User && userId is null)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidToken);
            return;
        }
        Result<ResourceTokenProjection> result = await projection.ProjectAsync(new ResourceTokenRequest(clientId, principal.GetScopes(), audiences, userId,
            principal.FindAll("permission").Select(static claim => claim.Value).ToArray(), audiences), context.CancellationToken);
        if (result.IsFailure)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidToken);
            return;
        }

        context.Claims.Remove("permission");
        context.Claims["permissions"] = new OpenIddictParameter(result.Value.Permissions.Select(static permission => (string?)permission).ToImmutableArray());
    }
}
