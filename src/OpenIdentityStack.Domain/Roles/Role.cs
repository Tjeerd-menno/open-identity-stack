
using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Roles;
/// <summary>
/// Represents a role that can be assigned to users.
/// Roles provide a way to group permissions and appear as claims in issued tokens.
/// </summary>
public sealed class Role
{
    private readonly List<string> permissions = [];

    private Role()
    {
        // Required for EF Core
    }

    private Role(
        RoleId id,
        string name,
        string displayName,
        string description,
        bool isSystemRole,
        bool isActive)
    {
        this.Id = id;
        this.Name = name;
        this.DisplayName = displayName;
        this.Description = description;
        this.IsSystemRole = isSystemRole;
        this.IsActive = isActive;
    }

    /// <summary>
    /// Gets the unique identifier for the role.
    /// </summary>
    public RoleId Id { get; private set; }

    /// <summary>
    /// Gets the normalized name of the role (lowercase, trimmed).
    /// Used for lookups and as the claim value in tokens.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the display name of the role.
    /// </summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the description of the role.
    /// </summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether this is a system-defined role that cannot be deleted or disabled.
    /// </summary>
    public bool IsSystemRole { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the role is active.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Gets the permissions granted by this role.
    /// </summary>
    public IReadOnlyList<string> Permissions => this.permissions.AsReadOnly();

    /// <summary>
    /// Creates a new role.
    /// </summary>
    /// <param name="name">The name of the role.</param>
    /// <param name="description">Optional description of the role.</param>
    /// <returns>A result containing the new role or an error.</returns>
    public static Result<Role> Create(string name, string? description)
    {
        return Create(name, displayName: null, description);
    }

    /// <summary>
    /// Creates a new role with a custom display name.
    /// </summary>
    /// <param name="name">The name of the role (will be normalized to lowercase).</param>
    /// <param name="displayName">The display name of the role. If null, uses the trimmed name.</param>
    /// <param name="description">Optional description of the role.</param>
    /// <returns>A result containing the new role or an error.</returns>
    public static Result<Role> Create(string name, string? displayName, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return DomainError.Validation("NameRequired", "Role name is required.");
        }

        string trimmedName = name.Trim();
        string normalizedName = trimmedName.ToLowerInvariant();
        string effectiveDisplayName = string.IsNullOrWhiteSpace(displayName) ? trimmedName : displayName.Trim();

        return new Role(
            RoleId.Create(),
            normalizedName,
            effectiveDisplayName,
            description ?? string.Empty,
            isSystemRole: false,
            isActive: true);
    }

    /// <summary>
    /// Creates a system role that cannot be disabled or deleted.
    /// </summary>
    /// <param name="name">The normalized name of the system role.</param>
    /// <param name="displayName">The display name of the role.</param>
    /// <param name="description">Description of the role.</param>
    /// <returns>A result containing the new system role or an error.</returns>
    public static Result<Role> CreateSystemRole(string name, string displayName, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return DomainError.Validation("NameRequired", "Role name is required.");
        }

        return new Role(
            RoleId.Create(),
            name.Trim().ToLowerInvariant(),
            displayName.Trim(),
            description ?? string.Empty,
            isSystemRole: true,
            isActive: true);
    }

    /// <summary>
    /// Updates the description of the role.
    /// </summary>
    /// <param name="description">The new description.</param>
    public void UpdateDescription(string? description)
    {
        this.Description = description ?? string.Empty;
    }

    /// <summary>
    /// Updates the display name of the role.
    /// </summary>
    /// <param name="displayName">The new display name. If null or whitespace, the current name is used.</param>
    public void UpdateDisplayName(string? displayName)
    {
        this.DisplayName = string.IsNullOrWhiteSpace(displayName) ? this.Name : displayName.Trim();
    }

    /// <summary>
    /// Disables the role.
    /// </summary>
    /// <returns>A result indicating success or failure.</returns>
    public Result Disable()
    {
        if (this.IsSystemRole)
        {
            return Result.Failure(DomainError.Validation("CannotDisableSystemRole", "System roles cannot be disabled."));
        }

        if (!this.IsActive)
        {
            return Result.Failure(DomainError.Validation("AlreadyDisabled", "Role is already disabled."));
        }

        this.IsActive = false;
        return Result.Success();
    }

    /// <summary>
    /// Enables a previously disabled role.
    /// </summary>
    /// <returns>A result indicating success or failure.</returns>
    public Result Enable()
    {
        if (this.IsActive)
        {
            return Result.Failure(DomainError.Validation("AlreadyActive", "Role is already active."));
        }

        this.IsActive = true;
        return Result.Success();
    }

    /// <summary>
    /// Adds a permission to this role.
    /// </summary>
    /// <param name="permission">The permission to add.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Result AddPermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            return Result.Failure(DomainError.Validation("PermissionRequired", "Permission is required."));
        }

        string normalizedPermission = permission.Trim().ToLowerInvariant();
        
        if (this.permissions.Contains(normalizedPermission, StringComparer.OrdinalIgnoreCase))
        {
            return Result.Failure(DomainError.Conflict("PermissionAlreadyExists", "Permission already exists on this role."));
        }

        this.permissions.Add(normalizedPermission);
        return Result.Success();
    }

    /// <summary>
    /// Removes a permission from this role.
    /// </summary>
    /// <param name="permission">The permission to remove.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Result RemovePermission(string permission)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            return Result.Failure(DomainError.Validation("PermissionRequired", "Permission is required."));
        }

        string normalizedPermission = permission.Trim().ToLowerInvariant();
        bool removed = this.permissions.RemoveAll(p => 
            string.Equals(p, normalizedPermission, StringComparison.OrdinalIgnoreCase)) > 0;

        if (!removed)
        {
            return Result.Failure(DomainError.NotFound("PermissionNotFound", "Permission not found on this role."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Sets the permissions for this role, replacing any existing permissions.
    /// </summary>
    /// <param name="permissions">The permissions to set.</param>
    public void SetPermissions(IEnumerable<string> permissions)
    {
        this.permissions.Clear();
        foreach (string permission in permissions)
        {
            if (!string.IsNullOrWhiteSpace(permission))
            {
                this.permissions.Add(permission.Trim().ToLowerInvariant());
            }
        }
    }
}
