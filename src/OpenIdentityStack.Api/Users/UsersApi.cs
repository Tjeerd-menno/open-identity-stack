using Microsoft.AspNetCore.Mvc;
using OpenIdentityStack.Api.Common;
using OpenIddict.Validation.AspNetCore;
using OpenIdentityStack.Api.Admin.Responses;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Groups;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Application.Roles.Queries;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;

using SharedKernel;
namespace OpenIdentityStack.Api.Users;

/// <summary>
/// Minimal API endpoints for managing users via the Admin API.
/// </summary>
internal static class UsersApi
{
    public static IEndpointRouteBuilder MapUsersApi(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("api/admin/users")
            .WithTags(nameof(UsersApi));

        // User CRUD
        group.MapPost(string.Empty, CreateUser)
            .RequireAuthorization(Permissions.Users.Write)
            .Produces<CreateUserResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("CreateUser")
            .WithSummary("Creates a new user");

        group.MapGet("{id:guid}", GetUser)
            .RequireAuthorization(Permissions.Users.Read)
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetUser")
            .WithSummary("Gets a user by ID");

        group.MapGet(string.Empty, ListUsers)
            .RequireAuthorization(Permissions.Users.Read)
            .Produces<UserListResponse>(StatusCodes.Status200OK)
            .WithName("ListUsers")
            .WithSummary("Lists users with pagination");

        group.MapPut("{id:guid}", UpdateUser)
            .RequireAuthorization(Permissions.Users.Write)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UpdateUser")
            .WithSummary("Updates a user");

        group.MapDelete("{id:guid}", DeleteUser)
            .RequireAuthorization(Permissions.Users.Delete)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DeleteUser")
            .WithSummary("Deletes a user");

        // User status operations
        group.MapPost("{id:guid}/disable", DisableUser)
            .RequireAuthorization(Permissions.Users.Disable)
            .Produces<UserStatusChangeResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("DisableUser")
            .WithSummary("Disables a user account");

        group.MapPost("{id:guid}/enable", EnableUser)
            .RequireAuthorization(Permissions.Users.Write)
            .Produces<UserStatusChangeResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("EnableUser")
            .WithSummary("Enables a disabled user account");

        group.MapPost("{id:guid}/reset-password", ResetPassword)
            .RequireAuthorization(Permissions.Users.ResetPassword)
            .Produces<PasswordResetResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("ResetPassword")
            .WithSummary("Resets a user's password");

        // User roles
        group.MapGet("{id:guid}/roles", GetUserRoles)
            .RequireAuthorization(Permissions.Roles.Read)
            .Produces<UserRolesResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetUserRoles")
            .WithSummary("Gets the roles assigned to a user");

        group.MapPost("{userId:guid}/roles/{roleId:guid}", AssignRole)
            .RequireAuthorization(Permissions.Roles.Assign)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("AssignRole")
            .WithSummary("Assigns a role to a user");

        group.MapDelete("{userId:guid}/roles/{roleId:guid}", UnassignRole)
            .RequireAuthorization(Permissions.Roles.Assign)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UnassignRole")
            .WithSummary("Unassigns a role from a user");

        // User groups
        group.MapGet("{id:guid}/groups", GetUserGroups)
            .RequireAuthorization(Permissions.Groups.Read)
            .Produces<List<GroupResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetUserGroups")
            .WithSummary("Gets the groups a user belongs to");

        // Upstream identities
        group.MapGet("{userId:guid}/upstream-identities", GetUpstreamIdentities)
            .RequireAuthorization(Permissions.Users.Read)
            .Produces<UpstreamIdentitiesResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("GetUpstreamIdentities")
            .WithSummary("Gets all upstream identities linked to a user");

        group.MapPost("{userId:guid}/upstream-identities", LinkUpstreamIdentity)
            .RequireAuthorization(Permissions.Users.Write)
            .Produces<LinkUpstreamIdentityResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .WithName("LinkUpstreamIdentity")
            .WithSummary("Links an upstream identity to a user");

        group.MapDelete("{userId:guid}/upstream-identities/{providerId:guid}", UnlinkUpstreamIdentity)
            .RequireAuthorization(Permissions.Users.Write)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .WithName("UnlinkUpstreamIdentity")
            .WithSummary("Unlinks an upstream identity from a user");

        return app;
    }

    private static async Task<IResult> CreateUser(
        [FromServices] ICreateUserUseCase createUserUseCase,
        [FromBody] CreateUserRequest request)
    {
        var command = new CreateUserCommand(request.Email, request.DisplayName, request.Password);
        Result<CreateUserResult> result = await createUserUseCase.ExecuteAsync(command);

        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        var response = new CreateUserResponse(
            result.Value.UserId.Value,
            result.Value.Email,
            result.Value.DisplayName,
            "PendingVerification",
            DateTimeOffset.UtcNow);

        return TypedResults.Created($"/api/admin/users/{result.Value.UserId.Value}", response);
    }

    private static async Task<IResult> GetUser(
        [FromServices] IGetUserQueryHandler getUserQueryHandler,
        Guid id)
    {
        var query = new GetUserQuery(new UserId(id));
        Result<GetUserResult> result = await getUserQueryHandler.HandleAsync(query);

        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        var response = new UserResponse(
            result.Value.Id.Value,
            result.Value.Email,
            result.Value.DisplayName,
            result.Value.Status.ToString(),
            result.Value.MfaEnabled,
            result.Value.LastLoginAt,
            result.Value.CreatedAt,
            result.Value.ModifiedAt);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> ListUsers(
        [FromServices] IListUsersQueryHandler listUsersQueryHandler,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var query = new ListUsersQuery(page, pageSize, search);
        Result<ListUsersResult> result = await listUsersQueryHandler.HandleAsync(query);

        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        var items = result.Value.Items.Select(u => new UserListItemResponse(
            u.Id.Value,
            u.Email,
            u.DisplayName,
            u.Status.ToString(),
            u.CreatedAt)).ToList();

        var response = new UserListResponse(
            items,
            result.Value.TotalCount,
            result.Value.Page,
            result.Value.PageSize,
            result.Value.TotalPages);

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> UpdateUser(
        [FromServices] IUpdateUserUseCase updateUserUseCase,
        Guid id,
        [FromBody] UpdateUserRequest request)
    {
        var command = new UpdateUserCommand(new UserId(id), request.DisplayName);
        Result<UpdateUserResult> result = await updateUserUseCase.ExecuteAsync(command);

        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        return TypedResults.Ok(new { userId = id, updatedAt = result.Value.UpdatedAt });
    }

    private static async Task<IResult> DeleteUser(
        [FromServices] IDeleteUserUseCase deleteUserUseCase,
        Guid id)
    {
        var command = new DeleteUserCommand(new UserId(id));
        Result result = await deleteUserUseCase.ExecuteAsync(command);

        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        return TypedResults.NoContent();
    }

    private static async Task<IResult> DisableUser(
        [FromServices] IDisableUserUseCase disableUserUseCase,
        Guid id,
        [FromBody] DisableUserRequest request)
    {
        var command = new DisableUserCommand(new UserId(id), request.Reason);
        Result<DisableUserResult> result = await disableUserUseCase.ExecuteAsync(command);

        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        return TypedResults.Ok(new UserStatusChangeResponse(id, result.Value.DisabledAt));
    }

    private static async Task<IResult> EnableUser(
        [FromServices] IEnableUserUseCase enableUserUseCase,
        Guid id)
    {
        var command = new EnableUserCommand(new UserId(id));
        Result<EnableUserResult> result = await enableUserUseCase.ExecuteAsync(command);

        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        return TypedResults.Ok(new UserStatusChangeResponse(id, result.Value.EnabledAt));
    }

    private static async Task<IResult> ResetPassword(
        [FromServices] IResetPasswordUseCase resetPasswordUseCase,
        Guid id,
        [FromBody] ResetPasswordRequest request)
    {
        var command = new ResetPasswordCommand(new UserId(id), request.NewPassword);
        Result<ResetPasswordResult> result = await resetPasswordUseCase.ExecuteAsync(command);

        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        return TypedResults.Ok(new PasswordResetResponse(id, result.Value.ResetAt));
    }

    private static async Task<IResult> GetUserRoles(
        [FromServices] IGetUserRolesQueryHandler getUserRolesQueryHandler,
        Guid id,
        [FromQuery] bool activeOnly = true)
    {
        Result<GetUserRolesResponse> result = await getUserRolesQueryHandler.HandleAsync(new UserId(id), activeOnly);

        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        var response = new UserRolesResponse
        {
            UserId = result.Value.UserId,
            Roles = result.Value.Roles.Select(r => new RoleResponse
            {
                Id = r.Id,
                Name = r.Name,
                DisplayName = r.DisplayName,
                Description = r.Description,
                IsSystemRole = r.IsSystemRole,
                IsActive = r.IsActive
            }).ToList()
        };

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> GetUserGroups(
        [FromServices] IGetUserGroupsQueryHandler getUserGroupsQueryHandler,
        Guid id)
    {
        Result<List<GroupResponse>> result = await getUserGroupsQueryHandler.HandleAsync(new UserId(id));
        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }
        return TypedResults.Ok(result.Value);
    }

    private static async Task<IResult> AssignRole(
        [FromServices] IAssignRoleUseCase assignRoleUseCase,
        Guid userId,
        Guid roleId)
    {
        var command = new AssignRoleCommand(new UserId(userId), new RoleId(roleId));
        Result result = await assignRoleUseCase.ExecuteAsync(command);

        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        return TypedResults.Ok(new { userId, roleId, assignedAt = DateTimeOffset.UtcNow });
    }

    private static async Task<IResult> UnassignRole(
        [FromServices] IUnassignRoleUseCase unassignRoleUseCase,
        Guid userId,
        Guid roleId)
    {
        var command = new UnassignRoleCommand(new UserId(userId), new RoleId(roleId));
        Result result = await unassignRoleUseCase.ExecuteAsync(command);

        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        return TypedResults.Ok(new { userId, roleId, unassignedAt = DateTimeOffset.UtcNow });
    }

    private static async Task<IResult> GetUpstreamIdentities(
        [FromServices] IListUserUpstreamIdentitiesQueryHandler listUpstreamIdentitiesQueryHandler,
        Guid userId)
    {
        var query = new ListUserUpstreamIdentitiesQuery(new UserId(userId));
        Result<ListUserUpstreamIdentitiesResult> result = await listUpstreamIdentitiesQueryHandler.ExecuteAsync(query);

        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        var response = new UpstreamIdentitiesResponse(
            result.Value.Items.Select(item => new UpstreamIdentityResponse(
                item.ProviderId.Value,
                item.ProviderName,
                item.SubjectId,
                item.Email,
                item.LinkedAt,
                item.LastLoginAt)).ToList());

        return TypedResults.Ok(response);
    }

    private static async Task<IResult> LinkUpstreamIdentity(
        [FromServices] ILinkUpstreamIdentityUseCase linkUpstreamIdentityUseCase,
        Guid userId,
        [FromBody] LinkUpstreamIdentityRequest request)
    {
        var command = new LinkUpstreamIdentityCommand(
            UserId: new UserId(userId),
            ProviderId: new UpstreamProviderId(request.ProviderId),
            ProviderName: string.Empty, // Will be looked up by use case
            SubjectId: request.SubjectId,
            Email: request.Email);

        Result<LinkUpstreamIdentityResult> result = await linkUpstreamIdentityUseCase.ExecuteAsync(command);

        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        var response = new LinkUpstreamIdentityResponse(
            userId,
            request.ProviderId,
            request.SubjectId,
            DateTimeOffset.UtcNow);

        return TypedResults.Created($"/api/admin/users/{userId}/upstream-identities", response);
    }

    private static async Task<IResult> UnlinkUpstreamIdentity(
        [FromServices] IUnlinkUpstreamIdentityUseCase unlinkUpstreamIdentityUseCase,
        Guid userId,
        Guid providerId)
    {
        var command = new UnlinkUpstreamIdentityCommand(
            new UserId(userId),
            new UpstreamProviderId(providerId));

        Result<UnlinkUpstreamIdentityResult> result = await unlinkUpstreamIdentityUseCase.ExecuteAsync(command);

        if (result.IsFailure)
        {
            return ErrorResultMapper.ToErrorResult(result.Error);
        }

        return TypedResults.NoContent();
    }

}
