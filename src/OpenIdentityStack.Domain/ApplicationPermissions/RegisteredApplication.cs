using System.Text.RegularExpressions;
using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Domain.ApplicationPermissions;

public sealed partial class RegisteredApplication : AggregateRoot<RegisteredApplicationId>
{
    private static readonly Regex applicationIdentifierRegex = GenerateApplicationIdentifierRegex();

    public static readonly DomainError IdentifierRequired = DomainError.Validation("RegisteredApplication.IdentifierRequired", "Application identifier is required.");
    public static readonly DomainError IdentifierInvalidFormat = DomainError.Validation("RegisteredApplication.IdentifierInvalidFormat", "Application identifier must match ^[a-z][a-z0-9-]{2,62}$.");
    public static readonly DomainError IdentifierReserved = DomainError.Conflict("RegisteredApplication.IdentifierReserved", "Application identifier is reserved and cannot be used.");
    public static readonly DomainError DisplayNameRequired = DomainError.Validation("RegisteredApplication.DisplayNameRequired", "Display name is required.");
    public static readonly DomainError DisplayNameTooLong = DomainError.Validation("RegisteredApplication.DisplayNameTooLong", "Display name must not exceed 120 characters.");
    public static readonly DomainError DescriptionTooLong = DomainError.Validation("RegisteredApplication.DescriptionTooLong", "Description must not exceed 1000 characters.");
    public static readonly DomainError OwnerRequired = DomainError.Validation("RegisteredApplication.OwnerRequired", "An owner is required before permissions can be made available.");
    public static readonly DomainError AtLeastOnePermissionRequired = DomainError.Validation("RegisteredApplication.AtLeastOnePermissionRequired", "At least one permission is required for registration.");
    public static readonly DomainError DuplicatePermissionKeys = DomainError.Validation("RegisteredApplication.DuplicatePermissionKeys", "Duplicate permission keys are not allowed.");
    public static readonly DomainError PermissionNotFound = DomainError.NotFound("RegisteredApplication.PermissionNotFound", "Permission was not found on the registered application.");
    public static readonly DomainError MaintainerAlreadyExists = DomainError.Conflict("RegisteredApplication.MaintainerAlreadyExists", "Delegated maintainer already exists for this application.");
    public static readonly DomainError MaintainerNotFound = DomainError.NotFound("RegisteredApplication.MaintainerNotFound", "Delegated maintainer was not found for this application.");

    private readonly List<ApplicationPermission> permissions = [];
    private readonly List<DelegatedMaintainer> maintainers = [];

    public string ApplicationIdentifier { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string OwnerId { get; private set; } = string.Empty;

    public OwnerType OwnerType { get; private set; }

    public ApplicationLifecycleStatus Status { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public string UpdatedBy { get; private set; } = string.Empty;

    public DateTimeOffset? DisabledAt { get; private set; }

    public DateTimeOffset? RetiredAt { get; private set; }

    public uint ConcurrencyToken { get; private set; }

    public IReadOnlyList<ApplicationPermission> Permissions => this.permissions.AsReadOnly();

    public IReadOnlyList<DelegatedMaintainer> Maintainers => this.maintainers.AsReadOnly();

    private RegisteredApplication()
    {
    }

    public static Result<RegisteredApplication> Register(
        string applicationIdentifier,
        string displayName,
        string? description,
        string ownerId,
        OwnerType ownerType,
        IEnumerable<(string Key, string DisplayName, string? Description, string? Category)> permissions,
        string createdBy,
        IDateTimeProvider dateTimeProvider,
        IEnumerable<string>? reservedIdentifiers = null)
    {
        if (string.IsNullOrWhiteSpace(applicationIdentifier))
        {
            return IdentifierRequired;
        }

        string normalized = applicationIdentifier.Trim().ToLowerInvariant();

        if (!applicationIdentifierRegex.IsMatch(normalized))
        {
            return IdentifierInvalidFormat;
        }

        if (reservedIdentifiers != null && reservedIdentifiers.Any(r => string.Equals(r, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return IdentifierReserved;
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

        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return OwnerRequired;
        }

        var permissionList = permissions.ToList();
        if (permissionList.Count == 0)
        {
            return AtLeastOnePermissionRequired;
        }

        var keys = permissionList.Select(p => p.Key.Trim().ToLowerInvariant()).ToList();
        if (keys.Count != keys.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            return DuplicatePermissionKeys;
        }

        var application = new RegisteredApplication
        {
            Id = RegisteredApplicationId.Create(),
            ApplicationIdentifier = normalized,
            DisplayName = displayName.Trim(),
            Description = description?.Trim(),
            OwnerId = ownerId.Trim(),
            OwnerType = ownerType,
            Status = ApplicationLifecycleStatus.Active,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
            CreatedAt = dateTimeProvider.UtcNow,
        };

        foreach ((string key, string permissionDisplayName, string? permissionDescription, string? category) in permissionList)
        {
            Result<ApplicationPermission> permissionResult = ApplicationPermission.Create(
                application.Id,
                normalized,
                key,
                permissionDisplayName,
                permissionDescription,
                category,
                createdBy,
                dateTimeProvider);
            if (permissionResult.IsFailure)
            {
                return permissionResult.Error;
            }

            application.permissions.Add(permissionResult.Value);
        }

        return application;
    }

    public Result UpdateMetadata(
        string displayName,
        string? description,
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

        this.DisplayName = displayName.Trim();
        this.Description = description?.Trim();
        this.Touch(updatedBy, dateTimeProvider);
        return Result.Success();
    }

    public Result<ApplicationPermission> AddPermission(
        string permissionKey,
        string displayName,
        string? description,
        string? category,
        string createdBy,
        IDateTimeProvider dateTimeProvider)
    {
        string normalizedKey = permissionKey.Trim().ToLowerInvariant();
        if (this.permissions.Any(p => string.Equals(p.PermissionKey, normalizedKey, StringComparison.OrdinalIgnoreCase)))
        {
            return DuplicatePermissionKeys;
        }

        Result<ApplicationPermission> permissionResult = ApplicationPermission.Create(
            this.Id,
            this.ApplicationIdentifier,
            permissionKey,
            displayName,
            description,
            category,
            createdBy,
            dateTimeProvider);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        ApplicationPermission permission = permissionResult.Value;
        permission.RecalculateAssignability(this.Status);
        this.permissions.Add(permission);
        this.Touch(createdBy, dateTimeProvider);
        return permission;
    }

    public Result UpdatePermission(
        ApplicationPermissionId permissionId,
        string displayName,
        string? description,
        string? category,
        string updatedBy,
        IDateTimeProvider dateTimeProvider)
    {
        ApplicationPermission? permission = this.permissions.FirstOrDefault(p => p.Id == permissionId);
        if (permission is null)
        {
            return PermissionNotFound;
        }

        Result result = permission.UpdateMetadata(displayName, description, category, updatedBy, dateTimeProvider);
        if (result.IsFailure)
        {
            return result;
        }

        this.Touch(updatedBy, dateTimeProvider);
        return Result.Success();
    }

    public Result ChangeStatus(
        ApplicationLifecycleStatus status,
        bool hasBlockingDependencies,
        string updatedBy,
        IDateTimeProvider dateTimeProvider)
    {
        if (status == ApplicationLifecycleStatus.Retired && hasBlockingDependencies)
        {
            return DomainError.Conflict("RegisteredApplication.HasBlockingDependencies", "Application cannot be retired while blocking dependencies exist.");
        }

        this.Status = status;
        this.Touch(updatedBy, dateTimeProvider);
        if (status == ApplicationLifecycleStatus.Disabled)
        {
            this.DisabledAt ??= dateTimeProvider.UtcNow;
        }
        else if (status == ApplicationLifecycleStatus.Retired)
        {
            this.RetiredAt ??= dateTimeProvider.UtcNow;
        }

        foreach (ApplicationPermission permission in this.permissions)
        {
            permission.RecalculateAssignability(this.Status);
        }

        return Result.Success();
    }

    public Result ChangePermissionStatus(
        ApplicationPermissionId permissionId,
        PermissionLifecycleStatus status,
        bool hasBlockingDependencies,
        string updatedBy,
        IDateTimeProvider dateTimeProvider)
    {
        ApplicationPermission? permission = this.permissions.FirstOrDefault(p => p.Id == permissionId);
        if (permission is null)
        {
            return PermissionNotFound;
        }

        Result result = permission.ChangeStatus(status, hasBlockingDependencies, updatedBy, dateTimeProvider);
        if (result.IsFailure)
        {
            return result;
        }

        permission.RecalculateAssignability(this.Status);
        this.Touch(updatedBy, dateTimeProvider);
        return Result.Success();
    }

    public Result TransferOwnership(
        string ownerId,
        OwnerType ownerType,
        string updatedBy,
        IDateTimeProvider dateTimeProvider)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return OwnerRequired;
        }

        this.OwnerId = ownerId.Trim();
        this.OwnerType = ownerType;
        this.Touch(updatedBy, dateTimeProvider);
        return Result.Success();
    }

    public Result AddMaintainer(
        string principalId,
        OwnerType principalType,
        string grantedBy,
        IDateTimeProvider dateTimeProvider)
    {
        if (string.IsNullOrWhiteSpace(principalId))
        {
            return DomainError.Validation("RegisteredApplication.MaintainerRequired", "Maintainer principal ID is required.");
        }

        if (this.maintainers.Any(m => string.Equals(m.PrincipalId, principalId.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return MaintainerAlreadyExists;
        }

        this.maintainers.Add(DelegatedMaintainer.Create(this.Id, principalId.Trim(), principalType, grantedBy, dateTimeProvider));
        this.Touch(grantedBy, dateTimeProvider);
        return Result.Success();
    }

    public Result RemoveMaintainer(
        string principalId,
        string updatedBy,
        IDateTimeProvider dateTimeProvider)
    {
        int removed = this.maintainers.RemoveAll(m => string.Equals(m.PrincipalId, principalId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
        {
            return MaintainerNotFound;
        }

        this.Touch(updatedBy, dateTimeProvider);
        return Result.Success();
    }

    public bool CanBeManagedBy(string actorId)
    {
        return string.Equals(this.OwnerId, actorId, StringComparison.OrdinalIgnoreCase)
            || this.maintainers.Any(m => string.Equals(m.PrincipalId, actorId, StringComparison.OrdinalIgnoreCase));
    }

    private void Touch(string updatedBy, IDateTimeProvider dateTimeProvider)
    {
        this.UpdatedBy = updatedBy;
        this.ConcurrencyToken++;
        this.SetModified(dateTimeProvider.UtcNow);
    }

    [GeneratedRegex(@"^[a-z][a-z0-9-]{2,62}$")]
    private static partial Regex GenerateApplicationIdentifierRegex();
}
