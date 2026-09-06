using Microsoft.AspNetCore.Mvc;

using OpenIdentityStack.Api.Admin.Requests;
using OpenIdentityStack.Api.Admin.Responses;
using OpenIdentityStack.Api.Common;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Api.Admin;

/// <summary>
/// Minimal API endpoints for managing roles.
/// </summary>
internal static class RolesApi
{
    public static IEndpointRouteBuilder MapRolesApi(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/admin/roles")
            .WithTags(nameof(RolesApi));

        // Role CRUD
        group.MapGet(string.Empty, ListRoles)
            .RequireAuthorization(Permissions.Roles.Read)
            .Produces<RolesListResponse>(StatusCodes.Status200OK)
            .WithName("ListRoles")
            .WithSummary("Lists all roles with pagination");

        group.MapGet("{id:guid}", GetRole)
            .RequireAuthorization(Permissions.Roles.Read)
            .Produces<RoleResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetRole")
            .WithSummary("Gets a role by ID");

        group.MapPost(string.Empty, CreateRole)
            .RequireAuthorization(Permissions.Roles.Write)
            .Produces<RoleResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces<AdministrativeApprovalProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("CreateRole")
            .WithSummary("Creates a new role");

        group.MapPut("{id:guid}", UpdateRole)
            .RequireAuthorization(Permissions.Roles.Write)
            .Produces<RoleResponse>(StatusCodes.Status200OK)
            .Produces<AdministrativeApprovalProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("UpdateRole")
            .WithSummary("Updates a role");

        group.MapDelete("{id:guid}", DeleteRole)
            .RequireAuthorization(Permissions.Roles.Write)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DeleteRole")
            .WithSummary("Deletes a role");

        // Role status
        group.MapPost("{id:guid}/disable", DisableRole)
            .RequireAuthorization(Permissions.Roles.Write)
            .Produces<RoleResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DisableRole")
            .WithSummary("Disables a role");

        group.MapPost("{id:guid}/enable", EnableRole)
            .RequireAuthorization(Permissions.Roles.Write)
            .Produces<RoleResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces<AdministrativeApprovalProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("EnableRole")
            .WithSummary("Enables a role");

        // Permissions
        group.MapGet("{id:guid}/permissions", GetPermissions)
            .RequireAuthorization(Permissions.Roles.Read)
            .Produces<IReadOnlyList<string>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetRolePermissions")
            .WithSummary("Gets the permissions for a role");

        group.MapPut("{id:guid}/permissions", SetPermissions)
            .RequireAuthorization(Permissions.Roles.Assign)
            .Produces<RoleResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces<AdministrativeApprovalProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("SetRolePermissions")
            .WithSummary("Sets the permissions for a role, replacing any existing permissions");

        group.MapPost("{id:guid}/permissions", AddPermission)
            .RequireAuthorization(Permissions.Roles.Assign)
            .Produces<RoleResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces<AdministrativeApprovalProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("AddRolePermission")
            .WithSummary("Adds a permission to a role");

        group.MapDelete("{id:guid}/permissions/{permission}", RemovePermission)
            .RequireAuthorization(Permissions.Roles.Assign)
            .Produces<RoleResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("RemoveRolePermission")
            .WithSummary("Removes a permission from a role");

        return app;
    }

    private static async Task<IResult> ListRoles(
        [FromServices] IListRolesQueryHandler listRolesQueryHandler,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeInactive = false,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = new ListRolesQuery(page, pageSize, includeInactive, search);
        Result<ListRolesResponse> result = await listRolesQueryHandler.HandleAsync(query, cancellationToken);

        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        var response = new RolesListResponse
        {
            Items = result.Value.Items.Select(MapToResponse).ToList(),
            TotalCount = result.Value.TotalCount,
            Page = result.Value.Page,
            PageSize = result.Value.PageSize
        };

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> GetRole(
        [FromServices] IGetRoleQueryHandler getRoleQueryHandler,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = new GetRoleQuery(new RoleId(id));
        Result<RoleDto> result = await getRoleQueryHandler.HandleAsync(query, cancellationToken);

        return result.IsFailure
            ? ErrorResultMapper.ToErrorResult(result.Error)
            : TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<IResult> CreateRole(
        [FromServices] ICreateRoleUseCase createRoleUseCase,
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateRoleCommand(
            request.Name,
            request.DisplayName,
            request.Description,
            request.Permissions,
            request.AcknowledgeWildcardGrant);
        Result<CreateRoleResponse> result = await createRoleUseCase.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        var response = new RoleResponse
        {
            Id = result.Value.Id,
            Name = result.Value.Name,
            DisplayName = result.Value.DisplayName,
            Description = result.Value.Description,
            IsSystemRole = false,
            IsActive = result.Value.IsActive,
            Permissions = result.Value.Permissions
        };

        return TypedResults.Created($"/api/admin/roles/{response.Id}", response);
    }

    private static async Task<IResult> UpdateRole(
        [FromServices] IUpdateRoleUseCase updateRoleUseCase,
        Guid id,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateRoleCommand(
            new RoleId(id),
            request.DisplayName,
            request.Description,
            request.Permissions,
            request.AcknowledgeWildcardGrant);
        Result<RoleDto> result = await updateRoleUseCase.ExecuteAsync(command, cancellationToken);

        return result.IsFailure
            ? ErrorResultMapper.ToErrorResult(result.Error)
            : TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<IResult> DeleteRole(
        [FromServices] IDeleteRoleUseCase deleteRoleUseCase,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var command = new DeleteRoleCommand(new RoleId(id));
        Result result = await deleteRoleUseCase.ExecuteAsync(command, cancellationToken);

        return result.IsFailure
            ? ErrorResultMapper.ToErrorResult(result.Error)
            : TypedResults.NoContent();
    }

    private static async Task<IResult> DisableRole(
        [FromServices] IDisableRoleUseCase disableRoleUseCase,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var command = new DisableRoleCommand(new RoleId(id));
        Result<RoleDto> result = await disableRoleUseCase.ExecuteAsync(command, cancellationToken);

        return result.IsFailure
            ? ErrorResultMapper.ToErrorResult(result.Error)
            : TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<IResult> EnableRole(
        [FromServices] IEnableRoleUseCase enableRoleUseCase,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var command = new EnableRoleCommand(new RoleId(id));
        Result<RoleDto> result = await enableRoleUseCase.ExecuteAsync(command, cancellationToken);

        return result.IsFailure
            ? ErrorResultMapper.ToErrorResult(result.Error)
            : TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<IResult> GetPermissions(
        [FromServices] IGetRoleQueryHandler getRoleQueryHandler,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = new GetRoleQuery(new RoleId(id));
        Result<RoleDto> result = await getRoleQueryHandler.HandleAsync(query, cancellationToken);

        return result.IsFailure
            ? ErrorResultMapper.ToErrorResult(result.Error)
            : TypedResults.Ok(result.Value.Permissions);
    }

    private static async Task<IResult> SetPermissions(
        [FromServices] ISetRolePermissionsUseCase setRolePermissionsUseCase,
        Guid id,
        [FromBody] SetRolePermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new SetRolePermissionsCommand(
            new RoleId(id),
            request.Permissions,
            request.AcknowledgeWildcardGrant);
        Result<RoleDto> result = await setRolePermissionsUseCase.ExecuteAsync(command, cancellationToken);

        return result.IsFailure
            ? ErrorResultMapper.ToErrorResult(result.Error)
            : TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<IResult> AddPermission(
        [FromServices] IAddRolePermissionUseCase addRolePermissionUseCase,
        Guid id,
        [FromBody] AddPermissionRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new AddRolePermissionCommand(
            new RoleId(id),
            request.Permission,
            request.AcknowledgeWildcardGrant);
        Result<RoleDto> result = await addRolePermissionUseCase.ExecuteAsync(command, cancellationToken);

        return result.IsFailure
            ? ErrorResultMapper.ToErrorResult(result.Error)
            : TypedResults.Ok(MapToResponse(result.Value));
    }

    private static async Task<IResult> RemovePermission(
        [FromServices] IRemoveRolePermissionUseCase removeRolePermissionUseCase,
        Guid id,
        string permission,
        CancellationToken cancellationToken = default)
    {
        string decodedPermission = Uri.UnescapeDataString(permission);
        var command = new RemoveRolePermissionCommand(new RoleId(id), decodedPermission);
        Result<RoleDto> result = await removeRolePermissionUseCase.ExecuteAsync(command, cancellationToken);

        return result.IsFailure
            ? ErrorResultMapper.ToErrorResult(result.Error)
            : TypedResults.Ok(MapToResponse(result.Value));
    }

    private static RoleResponse MapToResponse(RoleDto dto)
    {
        return new RoleResponse
        {
            Id = dto.Id,
            Name = dto.Name,
            DisplayName = dto.DisplayName,
            Description = dto.Description,
            IsSystemRole = dto.IsSystemRole,
            IsActive = dto.IsActive,
            Permissions = dto.Permissions
        };
    }
}
