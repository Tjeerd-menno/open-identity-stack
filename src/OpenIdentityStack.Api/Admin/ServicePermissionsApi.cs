using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OpenIdentityStack.Api.Admin.Requests.ServicePermissions;
using OpenIdentityStack.Api.Admin.Responses.ServicePermissions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Application.ServicePermissions.Commands;
using OpenIdentityStack.Application.ServicePermissions.Dtos;
using OpenIdentityStack.Application.ServicePermissions.Queries;
using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Api.Admin;

internal static class ServicePermissionsApi
{
    public static IEndpointRouteBuilder MapServicePermissionsApi(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/admin/service-permissions")
            .WithTags(nameof(ServicePermissionsApi));

        group.MapPost("services", RegisterService)
            .RequireAuthorization(Permissions.ServicePermissions.Write)
            .Produces<RegisterServiceResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("RegisterServicePermissions")
            .WithSummary("Registers a service and its permission namespace");

        group.MapGet("services", ListServices)
            .RequireAuthorization(Permissions.ServicePermissions.Read)
            .Produces<RegisteredServiceListResponse>(StatusCodes.Status200OK)
            .WithName("ListRegisteredServices")
            .WithSummary("Lists registered services");

        group.MapGet("services/{id:guid}", GetService)
            .RequireAuthorization(Permissions.ServicePermissions.Read)
            .Produces<RegisteredServiceDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetRegisteredService")
            .WithSummary("Gets a registered service");

        group.MapGet("catalog", ListCatalog)
            .RequireAuthorization(Permissions.ServicePermissions.Read)
            .Produces<AssignablePermissionCatalogResponse>(StatusCodes.Status200OK)
            .WithName("ListAssignablePermissionCatalog")
            .WithSummary("Lists assignable service permissions");

        return app;
    }

    private static async Task<IResult> RegisterService(
        [FromServices] IRegisterServiceUseCase registerServiceUseCase,
        HttpContext context,
        [FromBody] RegisterServiceRequest request,
        CancellationToken cancellationToken)
    {
        string actorId = GetActorId(context);
        var command = new RegisterServiceCommand(
            request.ServiceIdentifier,
            request.DisplayName,
            request.Description,
            request.OwnerId,
            request.OwnerType,
            actorId,
            request.Permissions.Select(p => new RegisterServicePermissionInput(
                p.PermissionKey,
                p.DisplayName,
                p.Description,
                p.IntendedUse,
                p.DocumentationUrl)).ToList());

        Result<RegisterServiceResult> result = await registerServiceUseCase.ExecuteAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            return ToErrorResult(result.Error);
        }

        var response = new RegisterServiceResponse(result.Value.ServiceId, result.Value.ServiceIdentifier, result.Value.PermissionsRegistered);
        return TypedResults.Created($"/api/admin/service-permissions/services/{result.Value.ServiceId}", response);
    }

    private static async Task<IResult> ListServices(
        [FromServices] IListRegisteredServicesQueryHandler handler,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? owner = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        PagedResult<RegisteredServiceSummaryDto> result = await handler.HandleAsync(
            new ListRegisteredServicesQuery(page, pageSize, status, owner, search),
            cancellationToken);
        return TypedResults.Ok(new RegisteredServiceListResponse(result.Items, result.Page, result.PageSize, result.TotalCount, result.TotalPages));
    }

    private static async Task<IResult> GetService(
        [FromServices] IGetRegisteredServiceQueryHandler handler,
        Guid id,
        CancellationToken cancellationToken)
    {
        Result<RegisteredServiceDto> result = await handler.HandleAsync(new GetRegisteredServiceQuery(id), cancellationToken);
        return result.IsSuccess ? TypedResults.Ok(result.Value) : ToErrorResult(result.Error);
    }

    private static async Task<IResult> ListCatalog(
        [FromServices] IListAssignablePermissionCatalogQueryHandler handler,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? serviceIdentifier = null,
        [FromQuery] string? search = null,
        [FromQuery] bool assignableOnly = true,
        CancellationToken cancellationToken = default)
    {
        PagedResult<ServicePermissionDto> result = await handler.HandleAsync(
            new ListAssignablePermissionCatalogQuery(page, pageSize, serviceIdentifier, search, assignableOnly),
            cancellationToken);
        return TypedResults.Ok(new AssignablePermissionCatalogResponse(result.Items, result.Page, result.PageSize, result.TotalCount, result.TotalPages));
    }

    private static string GetActorId(HttpContext context)
    {
        return context.User.FindFirstValue("sub")
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? "system";
    }

    private static IResult ToErrorResult(DomainError error)
    {
        if (error.Code.StartsWith("NotFound.", StringComparison.Ordinal))
        {
            return TypedResults.NotFound(new { error = error.Code, message = error.Description });
        }

        if (error.Code.StartsWith("Conflict.", StringComparison.Ordinal))
        {
            return TypedResults.Conflict(new { error = error.Code, message = error.Description });
        }

        return TypedResults.BadRequest(new { error = error.Code, message = error.Description });
    }
}
