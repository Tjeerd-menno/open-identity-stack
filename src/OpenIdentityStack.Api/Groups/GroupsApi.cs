using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Groups;
using OpenIdentityStack.Application.Groups.Commands;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Api.Groups;

/// <summary>
/// Minimal API endpoints for managing groups and group memberships via the Admin API.
/// </summary>
internal static class GroupsApi
{
    public static IEndpointRouteBuilder MapGroupsApi(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/admin/groups")
            .WithTags(nameof(GroupsApi));

        // Group CRUD
        group.MapGet(string.Empty, ListGroups)
            .RequireAuthorization(Permissions.Groups.Read)
            .Produces<GroupListResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("ListGroups")
            .WithSummary("Lists all groups with optional search and pagination");

        group.MapPost(string.Empty, CreateGroup)
            .RequireAuthorization(Permissions.Groups.Write)
            .Produces<GroupResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .WithName("CreateGroup")
            .WithSummary("Creates a new group");

        group.MapGet("{id:guid}", GetGroup)
            .RequireAuthorization(Permissions.Groups.Read)
            .Produces<GroupResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetGroup")
            .WithSummary("Gets a group by ID");

        group.MapPatch("{id:guid}", UpdateGroup)
            .RequireAuthorization(Permissions.Groups.Write)
            .Produces<GroupResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UpdateGroup")
            .WithSummary("Updates an existing group");

        group.MapDelete("{id:guid}", DeleteGroup)
            .RequireAuthorization(Permissions.Groups.Delete)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DeleteGroup")
            .WithSummary("Deletes a group");

        // Group members
        group.MapGet("{id:guid}/members", ListGroupMembers)
            .RequireAuthorization(Permissions.Groups.Read)
            .Produces<UserListResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("ListGroupMembers")
            .WithSummary("Lists members of a group");

        group.MapPost("{id:guid}/members/{userId:guid}", AddGroupMember)
            .RequireAuthorization(Permissions.Groups.ManageMembers)
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("AddGroupMember")
            .WithSummary("Adds a user to a group");

        group.MapDelete("{id:guid}/members/{userId:guid}", RemoveGroupMember)
            .RequireAuthorization(Permissions.Groups.ManageMembers)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("RemoveGroupMember")
            .WithSummary("Removes a user from a group");

        // Group mappings
        group.MapGet("{id:guid}/mappings", ListGroupMappings)
            .RequireAuthorization(Permissions.Groups.Read)
            .Produces<GroupMappingsListResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("ListGroupMappings")
            .WithSummary("Lists all mappings for a group");

        group.MapPost("{id:guid}/mappings", AddGroupMapping)
            .RequireAuthorization(Permissions.Groups.Write)
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("AddGroupMapping")
            .WithSummary("Adds a mapping (role or claim) to a group");

        group.MapDelete("{id:guid}/mappings/{mappingId:guid}", RemoveGroupMapping)
            .RequireAuthorization(Permissions.Groups.Write)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("RemoveGroupMapping")
            .WithSummary("Removes a mapping from a group");

        return app;
    }

    private static UserId GetCurrentUserId(HttpContext context)
    {
        string? userIdString = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
        {
            throw new UnauthorizedAccessException("User ID not found in claims.");
        }
        return new UserId(userId);
    }

    private static async Task<IResult> ListGroups(
        [FromServices] IListGroupsQueryHandler listGroupsQueryHandler,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        Result<GroupListResponse> result = await listGroupsQueryHandler.HandleAsync(page, pageSize, search);
        return result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.BadRequest(result.Error);
    }

    private static async Task<IResult> CreateGroup(
        [FromServices] ICreateGroupUseCase createGroupUseCase,
        [FromBody] CreateGroupRequest request)
    {
        var command = new CreateGroupCommand(
            request.Name,
            request.Description);

        Result<CreateGroupResponse> result = await createGroupUseCase.ExecuteAsync(command);

        if (result.IsSuccess)
        {
            return TypedResults.Created($"/api/admin/groups/{result.Value.Id}", result.Value);
        }
        return TypedResults.BadRequest(result.Error);
    }

    private static async Task<IResult> GetGroup(
        [FromServices] IGetGroupQueryHandler getGroupQueryHandler,
        Guid id)
    {
        Group? group = await getGroupQueryHandler.HandleAsync(new GroupId(id));
        if (group is null)
        {
            return TypedResults.NotFound();
        }

        var response = new GroupResponse(
            group.Id.Value,
            group.Name,
            group.Description,
            group.ParentGroupId?.Value,
            group.CreatedAt.UtcDateTime);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> UpdateGroup(
        [FromServices] IUpdateGroupUseCase updateGroupUseCase,
        [FromServices] IGetGroupQueryHandler getGroupQueryHandler,
        Guid id,
        [FromBody] UpdateGroupRequest request)
    {
        Result result = await updateGroupUseCase.ExecuteAsync(new UpdateGroupCommand(new GroupId(id), request.Name, request.Description));
        if (!result.IsSuccess)
        {
            return MapFailure(result.Error);
        }

        Group? group = await getGroupQueryHandler.HandleAsync(new GroupId(id));
        if (group is null)
        {
            return TypedResults.NotFound();
        }

        var response = new GroupResponse(
            group.Id.Value,
            group.Name,
            group.Description,
            group.ParentGroupId?.Value,
            group.CreatedAt.UtcDateTime);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> DeleteGroup(
        [FromServices] IDeleteGroupUseCase deleteGroupUseCase,
        Guid id)
    {
        Result result = await deleteGroupUseCase.ExecuteAsync(new DeleteGroupCommand(new GroupId(id)));
        if (!result.IsSuccess)
        {
            return MapFailure(result.Error);
        }
        return TypedResults.NoContent();
    }

    private static async Task<IResult> ListGroupMembers(
        [FromServices] IListGroupMembersQueryHandler listGroupMembersQueryHandler,
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        Result<UserListResponse> result = await listGroupMembersQueryHandler.HandleAsync(new GroupId(id), page, pageSize);
        if (!result.IsSuccess)
        {
            return MapFailure(result.Error);
        }

        // Project the strongly-typed UserId to its Guid value; serializing the value object
        // directly would surface member ids as "[object Object]" to the client.
        var response = new
        {
            items = result.Value.Items.Select(member => new
            {
                id = member.Id.Value,
                email = member.Email,
                displayName = member.DisplayName,
                status = member.Status.ToString(),
                createdAt = member.CreatedAt,
            }),
            nextPageToken = result.Value.NextPageToken,
        };
        return TypedResults.Ok(response);
    }

    private static async Task<IResult> AddGroupMember(
        [FromServices] IAddUserToGroupUseCase addUserToGroupUseCase,
        HttpContext httpContext,
        Guid id,
        Guid userId)
    {
        Result result = await addUserToGroupUseCase.ExecuteAsync(
            new AddUserToGroupCommand(new GroupId(id), new UserId(userId), GetCurrentUserId(httpContext)));
        if (!result.IsSuccess)
        {
            return MapFailure(result.Error);
        }
        return TypedResults.Created($"/api/admin/groups/{id}/members");
    }

    private static async Task<IResult> RemoveGroupMember(
        [FromServices] IRemoveUserFromGroupUseCase removeUserFromGroupUseCase,
        Guid id,
        Guid userId)
    {
        Result result = await removeUserFromGroupUseCase.ExecuteAsync(new RemoveUserFromGroupCommand(new GroupId(id), new UserId(userId)));
        if (!result.IsSuccess)
        {
            return MapFailure(result.Error);
        }
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Frontend-compatible response item for group mappings.
    /// </summary>
    /// <param name="Id">Deterministic identifier derived from the mapping's value.</param>
    /// <param name="Type">The mapping type (<c>Role</c> or <c>Claim</c>).</param>
    /// <param name="Value">The resolved role name or <c>claimType:claimValue</c> pair.</param>
    /// <param name="CreatedAt">
    /// Always <see langword="null"/>. A group mapping is a value object with no identity of its
    /// own, so it carries no creation timestamp. The field is retained so existing clients keep
    /// parsing the response, but reporting a real time here would mean inventing one.
    /// </param>
    private sealed record GroupMappingItem(
        string Id,
        string Type,
        string Value,
        string? CreatedAt);

    /// <summary>
    /// Frontend-compatible response wrapper for group mappings list.
    /// </summary>
    private sealed record GroupMappingsListResponse(
        List<GroupMappingItem> Items);

    private static async Task<IResult> ListGroupMappings(
        [FromServices] IListGroupMappingsQueryHandler listGroupMappingsQueryHandler,
        [FromServices] IRoleRepository roleRepository,
        Guid id)
    {
        Result<List<GroupMappingResponse>> result = await listGroupMappingsQueryHandler.HandleAsync(new GroupId(id));
        if (!result.IsSuccess)
        {
            return TypedResults.NotFound();
        }

        // Build a dictionary of role IDs to role names for role mappings
        var roleIds = result.Value
            .Where(m => m.MappingType == MappingType.Role && m.TargetRoleId.HasValue)
            .Select(m => m.TargetRoleId!.Value)
            .Distinct()
            .ToList();

        Dictionary<Guid, string> roleNames = [];
        foreach (Guid roleId in roleIds)
        {
            Role? role = await roleRepository.GetByIdAsync(new RoleId(roleId));
            if (role is not null)
            {
                roleNames[roleId] = role.Name;
            }
        }

        // Transform to frontend-compatible format
        var items = result.Value.Select(m =>
        {
            // Build value based on mapping type
            string value;
            if (m.MappingType == MappingType.Role && m.TargetRoleId.HasValue)
            {
                // Use role name if found, otherwise fall back to ID
                value = roleNames.TryGetValue(m.TargetRoleId.Value, out string? roleName)
                    ? roleName
                    : m.TargetRoleId.Value.ToString();
            }
            else
            {
                value = string.IsNullOrEmpty(m.ClaimValue)
                    ? m.ClaimType ?? string.Empty
                    : $"{m.ClaimType}:{m.ClaimValue}";
            }

            return new GroupMappingItem(
                m.Id.ToString(),
                m.MappingType.ToString(),
                value,
                CreatedAt: null);
        }).ToList();

        return TypedResults.Ok(new GroupMappingsListResponse(items));
    }

    /// <summary>
    /// Frontend-compatible request for adding a group mapping.
    /// Maps to the internal CreateGroupMappingRequest format.
    /// </summary>
    private sealed record AddMappingRequest(
        string Type,
        string Value);

    private static async Task<IResult> AddGroupMapping(
        [FromServices] IAddGroupMappingUseCase addGroupMappingUseCase,
        Guid id,
        [FromBody] AddMappingRequest request)
    {
        // Parse the simplified frontend format
        if (!Enum.TryParse<MappingType>(request.Type, ignoreCase: true, out MappingType mappingType))
        {
            return TypedResults.BadRequest("Invalid mapping type. Must be 'Role' or 'Claim'.");
        }

        string target;
        string? value = null;

        if (mappingType == MappingType.Role)
        {
            // For Role mapping, value is the role ID
            target = request.Value;
        }
        else
        {
            // For Claim mapping, value may be "claimType" or "claimType:claimValue"
            string[] parts = request.Value.Split(':', 2);
            target = parts[0];
            value = parts.Length > 1 ? parts[1] : null;
        }

        Result result = await addGroupMappingUseCase.ExecuteAsync(new AddGroupMappingCommand(
            new GroupId(id),
            mappingType,
            target,
            value,
            TokenTarget.AccessToken));

        if (!result.IsSuccess)
        {
            return MapFailure(result.Error);
        }

        return TypedResults.Created($"/api/admin/groups/{id}/mappings");
    }

    private static async Task<IResult> RemoveGroupMapping(
        [FromServices] IRemoveGroupMappingUseCase removeGroupMappingUseCase,
        Guid id,
        Guid mappingId)
    {
        Result result = await removeGroupMappingUseCase.ExecuteAsync(new RemoveGroupMappingCommand(new GroupId(id), mappingId));
        if (!result.IsSuccess)
        {
            return MapFailure(result.Error);
        }
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Maps a failed group operation to an HTTP result: not-found errors return 404,
    /// all other errors return 400 with the domain error payload.
    /// </summary>
    private static IResult MapFailure(DomainError error)
    {
        if (error.Code is "Group.NotFound" or "GroupMapping.NotFound")
        {
            return TypedResults.NotFound();
        }

        return TypedResults.BadRequest(error);
    }
}
