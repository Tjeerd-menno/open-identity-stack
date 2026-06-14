namespace OpenIdentityStack.Application.Roles.Commands;

/// <summary>
/// Command to add a single permission to a role.
/// </summary>
/// <param name="RoleId">The ID of the role.</param>
/// <param name="Permission">The permission to add.</param>
/// <param name="AcknowledgeWildcardGrant">Whether the caller acknowledged broad wildcard grants.</param>
public sealed record AddRolePermissionCommand(
    RoleId RoleId,
    string Permission,
    bool AcknowledgeWildcardGrant = false);
