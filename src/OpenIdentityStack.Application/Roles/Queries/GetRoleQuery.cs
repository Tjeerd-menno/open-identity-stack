namespace OpenIdentityStack.Application.Roles.Queries;

/// <summary>
/// Query to retrieve a single role by its ID.
/// </summary>
/// <param name="RoleId">The ID of the role to retrieve.</param>
public sealed record GetRoleQuery(RoleId RoleId);
