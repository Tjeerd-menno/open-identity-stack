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
using OpenIdentityStack.Domain.ServicePermissions;

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

        group.MapPatch("services/{id:guid}", UpdateService)
            .RequireAuthorization(Permissions.ServicePermissions.Write)
            .Produces<RegisteredServiceDto>(StatusCodes.Status200OK)
            .WithName("UpdateRegisteredService")
            .WithSummary("Updates registered service metadata");

        group.MapPost("services/{id:guid}/permissions", AddPermission)
            .RequireAuthorization(Permissions.ServicePermissions.Write)
            .Produces<RegisteredServiceDto>(StatusCodes.Status200OK)
            .WithName("AddRegisteredServicePermission")
            .WithSummary("Adds a permission to a registered service");

        group.MapPatch("permissions/{permissionId:guid}", UpdatePermission)
            .RequireAuthorization(Permissions.ServicePermissions.Write)
            .Produces<RegisteredServiceDto>(StatusCodes.Status200OK)
            .WithName("UpdateRegisteredServicePermission")
            .WithSummary("Updates registered permission metadata");

        group.MapPost("services/{id:guid}/lifecycle", ChangeServiceLifecycle)
            .RequireAuthorization(Permissions.ServicePermissions.Write)
            .Produces<RegisteredServiceDto>(StatusCodes.Status200OK)
            .WithName("ChangeRegisteredServiceLifecycle")
            .WithSummary("Changes registered service lifecycle status");

        group.MapPost("permissions/{permissionId:guid}/lifecycle", ChangePermissionLifecycle)
            .RequireAuthorization(Permissions.ServicePermissions.Write)
            .Produces<RegisteredServiceDto>(StatusCodes.Status200OK)
            .WithName("ChangeRegisteredServicePermissionLifecycle")
            .WithSummary("Changes registered permission lifecycle status");

        group.MapGet("permissions/{permissionId:guid}/dependencies", GetPermissionDependencies)
            .RequireAuthorization(Permissions.ServicePermissions.Read)
            .Produces<IReadOnlyList<RoleAssignmentDependency>>(StatusCodes.Status200OK)
            .WithName("GetRegisteredServicePermissionDependencies")
            .WithSummary("Lists role dependencies for a registered permission");

        group.MapPost("services/{id:guid}/ownership", TransferOwnership)
            .RequireAuthorization(Permissions.ServicePermissions.Admin)
            .Produces<RegisteredServiceDto>(StatusCodes.Status200OK)
            .WithName("TransferRegisteredServiceOwnership")
            .WithSummary("Transfers registered service ownership");

        group.MapPost("services/{id:guid}/maintainers", AddMaintainer)
            .RequireAuthorization(Permissions.ServicePermissions.Admin)
            .Produces<RegisteredServiceDto>(StatusCodes.Status200OK)
            .WithName("AddRegisteredServiceMaintainer")
            .WithSummary("Adds a delegated maintainer");

        group.MapDelete("services/{id:guid}/maintainers/{principalId}", RemoveMaintainer)
            .RequireAuthorization(Permissions.ServicePermissions.Admin)
            .Produces<RegisteredServiceDto>(StatusCodes.Status200OK)
            .WithName("RemoveRegisteredServiceMaintainer")
            .WithSummary("Removes a delegated maintainer");

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

    private static async Task<IResult> UpdateService(
        [FromServices] IUpdateRegisteredServiceUseCase useCase,
        HttpContext context,
        Guid id,
        [FromBody] UpdateRegisteredServiceRequest request,
        CancellationToken cancellationToken)
    {
        Result<RegisteredServiceDto> result = await useCase.ExecuteAsync(
            new UpdateRegisteredServiceCommand(id, request.DisplayName, request.Description, GetActorId(context), request.ConcurrencyToken),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> AddPermission(
        [FromServices] IAddServicePermissionUseCase useCase,
        HttpContext context,
        Guid id,
        [FromBody] AddServicePermissionRequest request,
        CancellationToken cancellationToken)
    {
        Result<RegisteredServiceDto> result = await useCase.ExecuteAsync(
            new AddServicePermissionCommand(id, request.PermissionKey, request.DisplayName, request.Description, request.IntendedUse, request.DocumentationUrl, GetActorId(context), request.ConcurrencyToken),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> UpdatePermission(
        [FromServices] IUpdateServicePermissionUseCase useCase,
        HttpContext context,
        Guid permissionId,
        [FromBody] UpdateServicePermissionRequest request,
        CancellationToken cancellationToken)
    {
        Result<RegisteredServiceDto> result = await useCase.ExecuteAsync(
            new UpdateServicePermissionCommand(permissionId, request.DisplayName, request.Description, request.IntendedUse, request.DocumentationUrl, GetActorId(context), request.ConcurrencyToken),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> ChangeServiceLifecycle(
        [FromServices] IChangeRegisteredServiceLifecycleUseCase useCase,
        HttpContext context,
        Guid id,
        [FromBody] ChangeServiceLifecycleRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(request.Status, ignoreCase: true, out ServiceLifecycleStatus status))
        {
            return TypedResults.BadRequest(new { error = "Invalid service lifecycle status." });
        }

        Result<RegisteredServiceDto> result = await useCase.ExecuteAsync(
            new ChangeRegisteredServiceLifecycleCommand(id, status, GetActorId(context), request.AcknowledgeDependencies, request.ConcurrencyToken),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> ChangePermissionLifecycle(
        [FromServices] IChangeServicePermissionLifecycleUseCase useCase,
        HttpContext context,
        Guid permissionId,
        [FromBody] ChangePermissionLifecycleRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(request.Status, ignoreCase: true, out PermissionLifecycleStatus status))
        {
            return TypedResults.BadRequest(new { error = "Invalid permission lifecycle status." });
        }

        Result<RegisteredServiceDto> result = await useCase.ExecuteAsync(
            new ChangeServicePermissionLifecycleCommand(permissionId, status, GetActorId(context), request.AcknowledgeDependencies, request.ConcurrencyToken),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> GetPermissionDependencies(
        [FromServices] IGetPermissionDependenciesQueryHandler handler,
        Guid permissionId,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<RoleAssignmentDependency>> result = await handler.HandleAsync(new GetPermissionDependenciesQuery(permissionId), cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> TransferOwnership(
        [FromServices] ITransferRegisteredServiceOwnershipUseCase useCase,
        HttpContext context,
        Guid id,
        [FromBody] TransferServiceOwnershipRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(request.OwnerType, ignoreCase: true, out OwnerType ownerType))
        {
            return TypedResults.BadRequest(new { error = "Invalid owner type." });
        }

        Result<RegisteredServiceDto> result = await useCase.ExecuteAsync(
            new TransferRegisteredServiceOwnershipCommand(id, request.OwnerId, ownerType, GetActorId(context), request.ConcurrencyToken),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> AddMaintainer(
        [FromServices] IAddDelegatedMaintainerUseCase useCase,
        HttpContext context,
        Guid id,
        [FromBody] AddDelegatedMaintainerRequest request,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(request.PrincipalType, ignoreCase: true, out OwnerType principalType))
        {
            return TypedResults.BadRequest(new { error = "Invalid principal type." });
        }

        Result<RegisteredServiceDto> result = await useCase.ExecuteAsync(
            new AddDelegatedMaintainerCommand(id, request.PrincipalId, principalType, GetActorId(context), request.ConcurrencyToken),
            cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> RemoveMaintainer(
        [FromServices] IRemoveDelegatedMaintainerUseCase useCase,
        HttpContext context,
        Guid id,
        string principalId,
        [FromQuery] uint? concurrencyToken,
        CancellationToken cancellationToken)
    {
        Result<RegisteredServiceDto> result = await useCase.ExecuteAsync(
            new RemoveDelegatedMaintainerCommand(id, principalId, GetActorId(context), concurrencyToken),
            cancellationToken);
        return ToResult(result);
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

        if (error.Code.StartsWith("Forbidden.", StringComparison.Ordinal))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: error.Code, detail: error.Description);
        }

        return TypedResults.BadRequest(new { error = error.Code, message = error.Description });
    }

    private static IResult ToResult<T>(Result<T> result)
    {
        return result.IsSuccess ? TypedResults.Ok(result.Value) : ToErrorResult(result.Error);
    }
}
