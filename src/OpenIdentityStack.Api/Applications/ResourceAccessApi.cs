using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OpenIdentityStack.Api.Common;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Resources;
using SharedKernel;

namespace OpenIdentityStack.Api.Applications;

internal static class ResourceAccessApi
{
    public static void MapResourceAccessApi(this RouteGroupBuilder group)
    {
        group = group.MapGroup(string.Empty);
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (ResourceAccessConflictException)
            {
                return TypedResults.Problem(statusCode: 409, title: "Resource access changed", detail: "Reload before saving.",
                    extensions: new Dictionary<string, object?> { ["code"] = "Conflict.ResourceAccess.Conflict" });
            }
        });
        group.MapGet("resources", async (ResourceAccessWorkflow workflow, CancellationToken ct) => TypedResults.Ok(await workflow.ListResourcesAsync(ct)))
            .RequireAuthorization(Permissions.Applications.Read).Produces<IReadOnlyList<ProtectedResourceDto>>().WithName("ListProtectedResources");
        group.MapPost("resources", async (ResourceConfiguration request, ResourceAccessWorkflow workflow, ClaimsPrincipal user, CancellationToken ct) =>
            ToResult(await workflow.SaveResourceAsync(null, request, Actor(user), ct)))
            .RequireAuthorization(Permissions.Applications.Write).Produces<ProtectedResourceDto>().Produces<ProblemDetails>(400).Produces<ProblemDetails>(403).Produces<ProblemDetails>(409).WithName("CreateProtectedResource");
        group.MapPut("resources/{resourceId:guid}", async (Guid resourceId, ResourceConfiguration request, ResourceAccessWorkflow workflow, ClaimsPrincipal user, CancellationToken ct) =>
            ToResult(await workflow.SaveResourceAsync(resourceId, request, Actor(user), ct)))
            .RequireAuthorization(Permissions.Applications.Write).Produces<ProtectedResourceDto>().Produces<ProblemDetails>(400).Produces<ProblemDetails>(403).Produces<ProblemDetails>(409).WithName("ConfigureProtectedResource");
        group.MapGet("{id:guid}/resource-grants", async (Guid id, ResourceAccessWorkflow workflow, CancellationToken ct) => TypedResults.Ok(await workflow.ListGrantsAsync(id, ct)))
            .RequireAuthorization(Permissions.Applications.Read).Produces<IReadOnlyList<ClientResourceGrantDto>>().WithName("ListClientResourceGrants");
        group.MapPut("{id:guid}/resource-grants/{resourceId:guid}", async (Guid id, Guid resourceId, ClientResourceGrantConfiguration request, ResourceAccessWorkflow workflow, ClaimsPrincipal user, CancellationToken ct) =>
            ToResult(await workflow.SaveGrantAsync(id, resourceId, request, Actor(user), ct)))
            .RequireAuthorization(Permissions.Applications.Write).Produces<ClientResourceGrantDto>().Produces<ProblemDetails>(400).Produces<ProblemDetails>(403).Produces<ProblemDetails>(409).WithName("ConfigureClientResourceGrant");
    }

    private static string Actor(ClaimsPrincipal principal) => Authorization.AdministrativeActorContext.ResolveAuditActorId(principal);
    private static IResult ToResult<T>(Result<T> result)
    {
        if (result.IsSuccess) { return TypedResults.Ok(result.Value); }
        int status = result.Error.Code.StartsWith("Forbidden.", StringComparison.Ordinal) ? 403
            : result.Error.Code.StartsWith("Conflict.", StringComparison.Ordinal) ? 409 : 400;
        return TypedResults.Problem(statusCode: status, title: "Resource access request rejected", detail: result.Error.Description,
            extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code });
    }
}
