using System.Text.RegularExpressions;
using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Domain.ServicePermissions;

public sealed partial class RegisteredService : AggregateRoot<RegisteredServiceId>
{
    private static readonly Regex serviceIdentifierRegex = GenerateServiceIdentifierRegex();

    public static readonly DomainError IdentifierRequired = DomainError.Validation("RegisteredService.IdentifierRequired", "Service identifier is required.");
    public static readonly DomainError IdentifierInvalidFormat = DomainError.Validation("RegisteredService.IdentifierInvalidFormat", "Service identifier must match ^[a-z][a-z0-9-]{2,62}$.");
    public static readonly DomainError IdentifierReserved = DomainError.Conflict("RegisteredService.IdentifierReserved", "Service identifier is reserved and cannot be used.");
    public static readonly DomainError DisplayNameRequired = DomainError.Validation("RegisteredService.DisplayNameRequired", "Display name is required.");
    public static readonly DomainError DisplayNameTooLong = DomainError.Validation("RegisteredService.DisplayNameTooLong", "Display name must not exceed 120 characters.");
    public static readonly DomainError DescriptionTooLong = DomainError.Validation("RegisteredService.DescriptionTooLong", "Description must not exceed 1000 characters.");
    public static readonly DomainError OwnerRequired = DomainError.Validation("RegisteredService.OwnerRequired", "An owner is required before permissions can be made available.");
    public static readonly DomainError AtLeastOnePermissionRequired = DomainError.Validation("RegisteredService.AtLeastOnePermissionRequired", "At least one permission is required for registration.");
    public static readonly DomainError DuplicatePermissionKeys = DomainError.Validation("RegisteredService.DuplicatePermissionKeys", "Duplicate permission keys are not allowed.");
    public static readonly DomainError PermissionNotFound = DomainError.NotFound("RegisteredService.PermissionNotFound", "Permission was not found on the registered service.");
    public static readonly DomainError MaintainerAlreadyExists = DomainError.Conflict("RegisteredService.MaintainerAlreadyExists", "Delegated maintainer already exists for this service.");
    public static readonly DomainError MaintainerNotFound = DomainError.NotFound("RegisteredService.MaintainerNotFound", "Delegated maintainer was not found for this service.");

    private readonly List<ServicePermission> permissions = [];
    private readonly List<DelegatedMaintainer> maintainers = [];

    public string ServiceIdentifier { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string OwnerId { get; private set; } = string.Empty;

    public OwnerType OwnerType { get; private set; }

    public ServiceLifecycleStatus Status { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public string UpdatedBy { get; private set; } = string.Empty;

    public DateTimeOffset? DisabledAt { get; private set; }

    public DateTimeOffset? RetiredAt { get; private set; }

    public uint ConcurrencyToken { get; private set; }

    public IReadOnlyList<ServicePermission> Permissions => this.permissions.AsReadOnly();

    public IReadOnlyList<DelegatedMaintainer> Maintainers => this.maintainers.AsReadOnly();

    private RegisteredService()
    {
    }

    public static Result<RegisteredService> Register(
        string serviceIdentifier,
        string displayName,
        string? description,
        string ownerId,
        OwnerType ownerType,
        IEnumerable<(string Key, string DisplayName, string? Description, string? IntendedUse, string? DocUrl)> permissions,
        string createdBy,
        IDateTimeProvider dateTimeProvider,
        IEnumerable<string>? reservedIdentifiers = null)
    {
        if (string.IsNullOrWhiteSpace(serviceIdentifier))
        {
            return IdentifierRequired;
        }

        string normalized = serviceIdentifier.Trim().ToLowerInvariant();

        if (!serviceIdentifierRegex.IsMatch(normalized))
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

        var service = new RegisteredService
        {
            Id = RegisteredServiceId.Create(),
            ServiceIdentifier = normalized,
            DisplayName = displayName.Trim(),
            Description = description?.Trim(),
            OwnerId = ownerId.Trim(),
            OwnerType = ownerType,
            Status = ServiceLifecycleStatus.Active,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
            CreatedAt = dateTimeProvider.UtcNow,
        };

        foreach ((string key, string permissionDisplayName, string? permissionDescription, string? intendedUse, string? docUrl) in permissionList)
        {
            Result<ServicePermission> permissionResult = ServicePermission.Create(
                service.Id,
                normalized,
                key,
                permissionDisplayName,
                permissionDescription,
                intendedUse,
                docUrl,
                createdBy,
                dateTimeProvider);
            if (permissionResult.IsFailure)
            {
                return permissionResult.Error;
            }

            service.permissions.Add(permissionResult.Value);
        }

        return service;
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

    public Result<ServicePermission> AddPermission(
        string permissionKey,
        string displayName,
        string? description,
        string? intendedUse,
        string? documentationUrl,
        string createdBy,
        IDateTimeProvider dateTimeProvider)
    {
        string normalizedKey = permissionKey.Trim().ToLowerInvariant();
        if (this.permissions.Any(p => string.Equals(p.PermissionKey, normalizedKey, StringComparison.OrdinalIgnoreCase)))
        {
            return DuplicatePermissionKeys;
        }

        Result<ServicePermission> permissionResult = ServicePermission.Create(
            this.Id,
            this.ServiceIdentifier,
            permissionKey,
            displayName,
            description,
            intendedUse,
            documentationUrl,
            createdBy,
            dateTimeProvider);

        if (permissionResult.IsFailure)
        {
            return permissionResult.Error;
        }

        ServicePermission permission = permissionResult.Value;
        permission.RecalculateAssignability(this.Status);
        this.permissions.Add(permission);
        this.Touch(createdBy, dateTimeProvider);
        return permission;
    }

    public Result UpdatePermission(
        ServicePermissionId permissionId,
        string displayName,
        string? description,
        string? intendedUse,
        string? documentationUrl,
        string updatedBy,
        IDateTimeProvider dateTimeProvider)
    {
        ServicePermission? permission = this.permissions.FirstOrDefault(p => p.Id == permissionId);
        if (permission is null)
        {
            return PermissionNotFound;
        }

        Result result = permission.UpdateMetadata(displayName, description, intendedUse, documentationUrl, updatedBy, dateTimeProvider);
        if (result.IsFailure)
        {
            return result;
        }

        this.Touch(updatedBy, dateTimeProvider);
        return Result.Success();
    }

    public Result ChangeStatus(
        ServiceLifecycleStatus status,
        bool hasBlockingDependencies,
        string updatedBy,
        IDateTimeProvider dateTimeProvider)
    {
        if (status == ServiceLifecycleStatus.Retired && hasBlockingDependencies)
        {
            return DomainError.Conflict("RegisteredService.HasBlockingDependencies", "Service cannot be retired while blocking dependencies exist.");
        }

        this.Status = status;
        this.Touch(updatedBy, dateTimeProvider);
        if (status == ServiceLifecycleStatus.Disabled)
        {
            this.DisabledAt ??= dateTimeProvider.UtcNow;
        }
        else if (status == ServiceLifecycleStatus.Retired)
        {
            this.RetiredAt ??= dateTimeProvider.UtcNow;
        }

        foreach (ServicePermission permission in this.permissions)
        {
            permission.RecalculateAssignability(this.Status);
        }

        return Result.Success();
    }

    public Result ChangePermissionStatus(
        ServicePermissionId permissionId,
        PermissionLifecycleStatus status,
        bool hasBlockingDependencies,
        string updatedBy,
        IDateTimeProvider dateTimeProvider)
    {
        ServicePermission? permission = this.permissions.FirstOrDefault(p => p.Id == permissionId);
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
            return DomainError.Validation("RegisteredService.MaintainerRequired", "Maintainer principal ID is required.");
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
    private static partial Regex GenerateServiceIdentifierRegex();
}
