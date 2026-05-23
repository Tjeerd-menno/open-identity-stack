using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Domain.ApplicationPermissions;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Permission is the domain term for this aggregate child.")]
public sealed partial class ApplicationPermission : Entity<ApplicationPermissionId>
{
    private static readonly Regex permissionKeyRegex = GeneratePermissionKeyRegex();

    public static readonly DomainError PermissionKeyRequired = DomainError.Validation("ApplicationPermission.PermissionKeyRequired", "Permission key is required.");
    public static readonly DomainError PermissionKeyInvalidFormat = DomainError.Validation("ApplicationPermission.PermissionKeyInvalidFormat", "Permission key must be an action key or permission name such as read-patients or read:patients.");
    public static readonly DomainError DisplayNameRequired = DomainError.Validation("ApplicationPermission.DisplayNameRequired", "Display name is required.");
    public static readonly DomainError DisplayNameTooLong = DomainError.Validation("ApplicationPermission.DisplayNameTooLong", "Display name must not exceed 120 characters.");
    public static readonly DomainError DescriptionTooLong = DomainError.Validation("ApplicationPermission.DescriptionTooLong", "Description must not exceed 1000 characters.");
    public static readonly DomainError CategoryTooLong = DomainError.Validation("ApplicationPermission.CategoryTooLong", "Category must not exceed 1000 characters.");

    public RegisteredApplicationId RegisteredApplicationId { get; private set; }

    public string PermissionKey { get; private set; } = string.Empty;

    public string FullPermissionKey { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? Category { get; private set; }

    public PermissionLifecycleStatus Status { get; private set; }

    public bool IsAssignable { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public string UpdatedBy { get; private set; } = string.Empty;

    public DateTimeOffset? DisabledAt { get; private set; }

    public DateTimeOffset? DeprecatedAt { get; private set; }

    public DateTimeOffset? RetiredAt { get; private set; }

    private ApplicationPermission()
    {
    }

    public static Result<ApplicationPermission> Create(
        RegisteredApplicationId registeredApplicationId,
        string applicationIdentifier,
        string permissionKey,
        string displayName,
        string? description,
        string? category,
        string createdBy,
        IDateTimeProvider dateTimeProvider)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            return PermissionKeyRequired;
        }

        string normalizedKey = permissionKey.Trim().ToLowerInvariant();

        if (!permissionKeyRegex.IsMatch(normalizedKey))
        {
            return PermissionKeyInvalidFormat;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return DisplayNameRequired;
        }

        if (displayName.Length > 120)
        {
            return DisplayNameTooLong;
        }

        if (description?.Length > 1000)
        {
            return DescriptionTooLong;
        }

        if (category?.Length > 1000)
        {
            return CategoryTooLong;
        }

        var permission = new ApplicationPermission
        {
            Id = ApplicationPermissionId.Create(),
            RegisteredApplicationId = registeredApplicationId,
            PermissionKey = normalizedKey,
            FullPermissionKey = normalizedKey.Contains(':', StringComparison.Ordinal) ? normalizedKey : $"{applicationIdentifier}:{normalizedKey}",
            DisplayName = displayName.Trim(),
            Description = description?.Trim(),
            Category = category?.Trim(),
            Status = PermissionLifecycleStatus.Active,
            IsAssignable = true,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
            CreatedAt = dateTimeProvider.UtcNow,
        };

        return permission;
    }

    internal void RecalculateAssignability(ApplicationLifecycleStatus applicationStatus)
    {
        this.IsAssignable = applicationStatus == ApplicationLifecycleStatus.Active && this.Status == PermissionLifecycleStatus.Active;
    }

    internal Result UpdateMetadata(
        string displayName,
        string? description,
        string? category,
        string updatedBy,
        IDateTimeProvider dateTimeProvider)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return DisplayNameRequired;
        }

        if (displayName.Length > 120)
        {
            return DisplayNameTooLong;
        }

        if (description?.Length > 1000)
        {
            return DescriptionTooLong;
        }

        if (category?.Length > 1000)
        {
            return CategoryTooLong;
        }

        this.DisplayName = displayName.Trim();
        this.Description = description?.Trim();
        this.Category = category?.Trim();
        this.UpdatedBy = updatedBy;
        this.SetModified(dateTimeProvider.UtcNow);
        return Result.Success();
    }

    internal Result ChangeStatus(
        PermissionLifecycleStatus status,
        bool hasBlockingDependencies,
        string updatedBy,
        IDateTimeProvider dateTimeProvider)
    {
        if (status == PermissionLifecycleStatus.Retired && hasBlockingDependencies)
        {
            return DomainError.Conflict("ApplicationPermission.HasBlockingDependencies", "Permission cannot be retired while blocking dependencies exist.");
        }

        this.Status = status;
        this.UpdatedBy = updatedBy;
        this.SetModified(dateTimeProvider.UtcNow);

        if (status == PermissionLifecycleStatus.Deprecated)
        {
            this.DeprecatedAt ??= dateTimeProvider.UtcNow;
        }
        else if (status == PermissionLifecycleStatus.Disabled)
        {
            this.DisabledAt ??= dateTimeProvider.UtcNow;
        }
        else if (status == PermissionLifecycleStatus.Retired)
        {
            this.RetiredAt ??= dateTimeProvider.UtcNow;
        }

        return Result.Success();
    }

    [GeneratedRegex(@"^(?=.{2,63}$)[a-z][a-z0-9-]{1,62}(:[a-z][a-z0-9-]{1,62})?$")]
    private static partial Regex GeneratePermissionKeyRegex();
}
