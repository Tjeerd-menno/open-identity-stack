using SharedKernel;
namespace OpenIdentityStack.Domain.Roles;

/// <summary>
/// Role-related domain errors.
/// </summary>
public static class RoleErrors
{
    /// <summary>
    /// The role could not be found.
    /// </summary>
    public static readonly DomainError NotFound =
        DomainError.NotFound("Role.NotFound", "Role not found.");

    /// <summary>
    /// A system role cannot be deleted.
    /// </summary>
    public static readonly DomainError CannotDeleteSystemRole =
        DomainError.Validation("Role.CannotDeleteSystemRole", "System roles cannot be deleted.");
}
