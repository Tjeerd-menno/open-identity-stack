using System.Security.Claims;
using OpenIdentityStack.Api.Common;
using OpenIdentityStack.Application.AdministrativeAccess;
using OpenIdentityStack.Application.Authorization;
using SharedKernel;

namespace OpenIdentityStack.Api.Applications;

internal static class AdministrativeAccessApi
{
    public static IEndpointRouteBuilder MapAdministrativeAccessApi(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/admin/applications/{id:guid}/administrative-access").WithTags("Applications");
        group.MapGet("", async (Guid id, AdministrativeAccessWorkflow workflow, CancellationToken cancellationToken) =>
        {
            Result<AdministrativeAccessDto> result = await workflow.GetAsync(id, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : ErrorResultMapper.ToErrorResult(result.Error);
        }).RequireAuthorization(Permissions.Applications.Read)
            .Produces<AdministrativeAccessDto>().Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden)
            .WithName("GetAdministrativeAccess").WithSummary("Gets an application's approved administrative permission ceilings");

        group.MapPut("", async (Guid id, AdministrativeAccessConfiguration request, ClaimsPrincipal actor,
            AdministrativeAccessWorkflow workflow, CancellationToken cancellationToken) =>
        {
            Result<AdministrativeAccessDto> result = await workflow.SaveAsync(id, request, actor.FindFirstValue("sub") ?? "unknown", cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : ErrorResultMapper.ToErrorResult(result.Error);
        }).RequireAuthorization(Permissions.Applications.Write)
            .Produces<AdministrativeAccessDto>().Produces(StatusCodes.Status400BadRequest).Produces(StatusCodes.Status401Unauthorized).Produces(StatusCodes.Status403Forbidden).Produces(StatusCodes.Status409Conflict)
            .WithName("SaveAdministrativeAccess").WithSummary("Approves, reduces, or withdraws administrative access; approval and expansion require fresh human approval");
        return endpoints;
    }
}
