using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Domain.ServicePermissions;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Permission is the domain term for this aggregate child.")]
public sealed partial class ServicePermission : Entity<ServicePermissionId>
{
    private static readonly Regex permissionKeyRegex = GeneratePermissionKeyRegex();

    public static readonly DomainError PermissionKeyRequired = DomainError.Validation("ServicePermission.PermissionKeyRequired", "Permission key is required.");
    public static readonly DomainError PermissionKeyInvalidFormat = DomainError.Validation("ServicePermission.PermissionKeyInvalidFormat", "Permission key must match ^[a-z][a-z0-9-]{1,62}$.");
    public static readonly DomainError DisplayNameRequired = DomainError.Validation("ServicePermission.DisplayNameRequired", "Display name is required.");
    public static readonly DomainError DisplayNameTooLong = DomainError.Validation("ServicePermission.DisplayNameTooLong", "Display name must not exceed 120 characters.");
    public static readonly DomainError DescriptionTooLong = DomainError.Validation("ServicePermission.DescriptionTooLong", "Description must not exceed 1000 characters.");

    public RegisteredServiceId RegisteredServiceId { get; private set; }

    public string PermissionKey { get; private set; } = string.Empty;

    public string FullPermissionKey { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string? IntendedUse { get; private set; }

    public string? DocumentationUrl { get; private set; }

    public PermissionLifecycleStatus Status { get; private set; }

    public bool IsAssignable { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public string UpdatedBy { get; private set; } = string.Empty;

    private ServicePermission()
    {
    }

    public static Result<ServicePermission> Create(
        RegisteredServiceId registeredServiceId,
        string serviceIdentifier,
        string permissionKey,
        string displayName,
        string? description,
        string? intendedUse,
        string? documentationUrl,
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

        var permission = new ServicePermission
        {
            Id = ServicePermissionId.Create(),
            RegisteredServiceId = registeredServiceId,
            PermissionKey = normalizedKey,
            FullPermissionKey = $"{serviceIdentifier}:{normalizedKey}",
            DisplayName = displayName.Trim(),
            Description = description?.Trim(),
            IntendedUse = intendedUse?.Trim(),
            DocumentationUrl = documentationUrl?.Trim(),
            Status = PermissionLifecycleStatus.Active,
            IsAssignable = true,
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
            CreatedAt = dateTimeProvider.UtcNow,
        };

        return permission;
    }

    internal void RecalculateAssignability(ServiceLifecycleStatus serviceStatus)
    {
        this.IsAssignable = serviceStatus == ServiceLifecycleStatus.Active && this.Status == PermissionLifecycleStatus.Active;
    }

    [GeneratedRegex(@"^[a-z][a-z0-9-]{1,62}$")]
    private static partial Regex GeneratePermissionKeyRegex();
}
