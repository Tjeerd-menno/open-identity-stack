using OpenIdentityStack.Domain.Roles;

namespace OpenIdentityStack.Application.Roles.Queries;

/// <summary>
/// Maps <see cref="Role"/> aggregates to <see cref="RoleDto"/> projections.
/// </summary>
internal static class RoleDtoMapper
{
    public static RoleDto ToDto(Role role) => new(
        role.Id.Value,
        role.Name,
        role.DisplayName,
        role.Description,
        role.IsSystemRole,
        role.IsActive,
        role.Permissions);
}
