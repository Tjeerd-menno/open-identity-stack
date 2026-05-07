using SharedKernel;
namespace OpenIdentityStack.Application.Roles.Queries;
/// <summary>
/// Response containing user's roles.
/// </summary>
/// <param name="UserId">The user ID.</param>
/// <param name="Roles">The roles assigned to the user.</param>
public sealed record GetUserRolesResponse(
    Guid UserId,
    IReadOnlyList<RoleDto> Roles);
