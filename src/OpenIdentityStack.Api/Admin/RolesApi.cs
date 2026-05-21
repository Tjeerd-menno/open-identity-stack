using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;
using OpenIdentityStack.Api.Admin.Requests;
using OpenIdentityStack.Api.Admin.Responses;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;

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
            .Produces(StatusCodes.Status409Conflict)
            .WithName("CreateRole")
            .WithSummary("Creates a new role");

        group.MapPut("{id:guid}", UpdateRole)
            .RequireAuthorization(Permissions.Roles.Write)
            .Produces<RoleResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
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
            .Produces(StatusCodes.Status404NotFound)
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
            .Produces(StatusCodes.Status404NotFound)
            .WithName("SetRolePermissions")
            .WithSummary("Sets the permissions for a role, replacing any existing permissions");

        group.MapPost("{id:guid}/permissions", AddPermission)
            .RequireAuthorization(Permissions.Roles.Assign)
            .Produces<RoleResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
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
            return TypedResults.BadRequest(new { error = result.Error.Description });
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
        [FromServices] IRoleRepository roleRepository,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var roleId = new RoleId(id);
        Role? role = await roleRepository.GetByIdAsync(roleId, cancellationToken);

        if (role is null)
        {
            return TypedResults.NotFound(new { error = "Role not found." });
        }

        return TypedResults.Ok(MapToResponse(role));
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
            request.Permissions);
        Result<CreateRoleResponse> result = await createRoleUseCase.ExecuteAsync(command, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("Conflict"))
            {
                return TypedResults.Conflict(new { error = result.Error.Description });
            }

            return TypedResults.BadRequest(new { error = result.Error.Description });
        }

        var response = new RoleResponse
        {
            Id = result.Value.Id,
            Name = result.Value.Name,
            DisplayName = result.Value.DisplayName,
            Description = result.Value.Description,
            IsSystemRole = false,
            IsActive = result.Value.IsActive
        };

        return TypedResults.Created($"/api/admin/roles/{response.Id}", response);
    }

    private static async Task<IResult> UpdateRole(
        [FromServices] IRoleRepository roleRepository,
        Guid id,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var roleId = new RoleId(id);
        Role? role = await roleRepository.GetByIdAsync(roleId, cancellationToken);

        if (role is null)
        {
            return TypedResults.NotFound(new { error = "Role not found." });
        }

        role.UpdateDescription(request.Description);
        await roleRepository.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(MapToResponse(role));
    }

    private static async Task<IResult> DeleteRole(
        [FromServices] IRoleRepository roleRepository,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var roleId = new RoleId(id);
        Role? role = await roleRepository.GetByIdAsync(roleId, cancellationToken);

        if (role is null)
        {
            return TypedResults.NotFound(new { error = "Role not found." });
        }

        if (role.IsSystemRole)
        {
            return TypedResults.BadRequest(new { error = "System roles cannot be deleted." });
        }

        roleRepository.Remove(role);
        await roleRepository.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<IResult> DisableRole(
        [FromServices] IRoleRepository roleRepository,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var roleId = new RoleId(id);
        Role? role = await roleRepository.GetByIdAsync(roleId, cancellationToken);

        if (role is null)
        {
            return TypedResults.NotFound(new { error = "Role not found." });
        }

        Result result = role.Disable();
        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new { error = result.Error.Description });
        }

        await roleRepository.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(MapToResponse(role));
    }

    private static async Task<IResult> EnableRole(
        [FromServices] IRoleRepository roleRepository,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var roleId = new RoleId(id);
        Role? role = await roleRepository.GetByIdAsync(roleId, cancellationToken);

        if (role is null)
        {
            return TypedResults.NotFound(new { error = "Role not found." });
        }

        Result result = role.Enable();
        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new { error = result.Error.Description });
        }

        await roleRepository.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(MapToResponse(role));
    }

    private static async Task<IResult> GetPermissions(
        [FromServices] IRoleRepository roleRepository,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var roleId = new RoleId(id);
        Role? role = await roleRepository.GetByIdAsync(roleId, cancellationToken);

        if (role is null)
        {
            return TypedResults.NotFound(new { error = "Role not found." });
        }

        return TypedResults.Ok(role.Permissions);
    }

    private static async Task<IResult> SetPermissions(
        [FromServices] IRoleRepository roleRepository,
        [FromServices] IPermissionAssignmentValidator permissionAssignmentValidator,
        Guid id,
        [FromBody] SetRolePermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var roleId = new RoleId(id);
        Role? role = await roleRepository.GetByIdAsync(roleId, cancellationToken);

        if (role is null)
        {
            return TypedResults.NotFound(new { error = "Role not found." });
        }

        Result validationResult = await permissionAssignmentValidator.ValidateAssignableAsync(request.Permissions, cancellationToken);
        if (validationResult.IsFailure)
        {
            return TypedResults.BadRequest(new { error = validationResult.Error.Description });
        }

        role.SetPermissions(request.Permissions);
        await roleRepository.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(MapToResponse(role));
    }

    private static async Task<IResult> AddPermission(
        [FromServices] IRoleRepository roleRepository,
        [FromServices] IPermissionAssignmentValidator permissionAssignmentValidator,
        Guid id,
        [FromBody] AddPermissionRequest request,
        CancellationToken cancellationToken = default)
    {
        var roleId = new RoleId(id);
        Role? role = await roleRepository.GetByIdAsync(roleId, cancellationToken);

        if (role is null)
        {
            return TypedResults.NotFound(new { error = "Role not found." });
        }

        Result validationResult = await permissionAssignmentValidator.ValidateAssignableAsync([request.Permission], cancellationToken);
        if (validationResult.IsFailure)
        {
            return TypedResults.BadRequest(new { error = validationResult.Error.Description });
        }

        Result result = role.AddPermission(request.Permission);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("Conflict"))
            {
                return TypedResults.Conflict(new { error = result.Error.Description });
            }

            return TypedResults.BadRequest(new { error = result.Error.Description });
        }

        await roleRepository.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(MapToResponse(role));
    }

    private static async Task<IResult> RemovePermission(
        [FromServices] IRoleRepository roleRepository,
        Guid id,
        string permission,
        CancellationToken cancellationToken = default)
    {
        var roleId = new RoleId(id);
        Role? role = await roleRepository.GetByIdAsync(roleId, cancellationToken);

        if (role is null)
        {
            return TypedResults.NotFound(new { error = "Role not found." });
        }

        string decodedPermission = Uri.UnescapeDataString(permission);
        Result result = role.RemovePermission(decodedPermission);
        if (result.IsFailure)
        {
            return TypedResults.BadRequest(new { error = result.Error.Description });
        }

        await roleRepository.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(MapToResponse(role));
    }

    private static RoleResponse MapToResponse(Role role)
    {
        return new RoleResponse
        {
            Id = role.Id.Value,
            Name = role.Name,
            DisplayName = role.DisplayName,
            Description = role.Description,
            IsSystemRole = role.IsSystemRole,
            IsActive = role.IsActive,
            Permissions = role.Permissions
        };
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
