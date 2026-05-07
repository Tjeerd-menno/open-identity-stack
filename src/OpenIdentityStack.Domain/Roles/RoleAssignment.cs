
using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Roles;
/// <summary>
/// Represents the assignment of a role to a user.
/// This is a join entity that tracks when a role was assigned.
/// </summary>
public sealed class RoleAssignment
{
    private RoleAssignment()
    {
        // Required for EF Core
    }

    private RoleAssignment(UserId userId, RoleId roleId, DateTimeOffset assignedAt)
    {
        this.UserId = userId;
        this.RoleId = roleId;
        this.AssignedAt = assignedAt;
    }

    /// <summary>
    /// Gets the ID of the user to whom the role is assigned.
    /// </summary>
    public UserId UserId { get; private set; }

    /// <summary>
    /// Gets the ID of the assigned role.
    /// </summary>
    public RoleId RoleId { get; private set; }

    /// <summary>
    /// Gets when the role was assigned.
    /// </summary>
    public DateTimeOffset AssignedAt { get; private set; }

    /// <summary>
    /// Creates a new role assignment.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roleId">The ID of the role.</param>
    /// <param name="assignedAt">When the role was assigned.</param>
    /// <returns>A result containing the assignment or an error.</returns>
    public static Result<RoleAssignment> Create(UserId userId, RoleId roleId, DateTimeOffset assignedAt)
    {
        if (userId == default)
        {
            return DomainError.Validation("UserIdRequired", "User ID is required.");
        }

        if (roleId == default)
        {
            return DomainError.Validation("RoleIdRequired", "Role ID is required.");
        }

        return new RoleAssignment(userId, roleId, assignedAt);
    }

    /// <summary>
    /// Creates a role assignment for a user with the current timestamp.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="roleId">The ID of the role.</param>
    /// <returns>A result containing the assignment or an error.</returns>
    public static Result<RoleAssignment> ForUser(UserId userId, RoleId roleId)
    {
        return Create(userId, roleId, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Checks if this assignment matches the given user and role IDs.
    /// </summary>
    /// <param name="userId">The user ID to match.</param>
    /// <param name="roleId">The role ID to match.</param>
    /// <returns>True if both IDs match.</returns>
    public bool Matches(UserId userId, RoleId roleId)
    {
        return this.UserId == userId && this.RoleId == roleId;
    }
}
