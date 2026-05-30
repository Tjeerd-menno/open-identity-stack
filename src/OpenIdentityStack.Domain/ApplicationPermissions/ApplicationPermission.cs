using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Domain.ApplicationPermissions;

[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Permission is the domain term for this aggregate child.")]
public sealed partial class ApplicationPermission : Entity<ApplicationPermissionId>
{
    private static readonly Regex permissionKeyRegex = GeneratePermissionKeyRegex();

    public static readonly DomainError PermissionKeyRequired = DomainError.Validation("ApplicationPermission.PermissionKeyRequired", "Permission key is required.");
    public static readonly DomainError PermissionKeyInvalidFormat = DomainError.Validation("ApplicationPermission.PermissionKeyInvalidFormat", "Permission key must match resourceOrAggregate:action.");
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

    public string CreatedBy { get; private set; } = string.Empty;

    public string UpdatedBy { get; private set; } = string.Empty;

    public DateTimeOffset? RemovedAt { get; private set; }

    public string? RemovedBy { get; private set; }

    public string? RemoveReason { get; private set; }

    public string? ReplacementFullPermissionKey { get; private set; }

    public string? ReplacementNote { get; private set; }

    public bool IsRemoved => this.RemovedAt.HasValue;

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
            FullPermissionKey = $"{applicationIdentifier}:{normalizedKey}",
            DisplayName = displayName.Trim(),
            Description = description?.Trim(),
            Category = category?.Trim(),
            CreatedBy = createdBy,
            UpdatedBy = createdBy,
            CreatedAt = dateTimeProvider.UtcNow,
        };

        return permission;
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

    internal void MarkRemoved(string removedBy, string removeReason, IDateTimeProvider dateTimeProvider)
    {
        this.RemovedAt = dateTimeProvider.UtcNow;
        this.RemovedBy = removedBy;
        this.RemoveReason = removeReason;
        this.UpdatedBy = removedBy;
        this.SetModified(dateTimeProvider.UtcNow);
    }

    internal Result SetReplacementGuidance(
        string replacementFullPermissionKey,
        string replacementNote,
        string updatedBy,
        IDateTimeProvider dateTimeProvider)
    {
        if (!this.IsRemoved)
        {
            return DomainError.Conflict("ApplicationPermission.NotRemoved", "Replacement guidance can only be added to removed permissions.");
        }

        if (string.IsNullOrWhiteSpace(replacementFullPermissionKey))
        {
            return DomainError.Validation("ApplicationPermission.ReplacementRequired", "Replacement permission is required.");
        }

        if (replacementFullPermissionKey.Length > 200)
        {
            return DomainError.Validation("ApplicationPermission.ReplacementTooLong", "Replacement permission must not exceed 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(replacementNote))
        {
            return DomainError.Validation("ApplicationPermission.ReplacementNoteRequired", "Replacement note is required.");
        }

        if (replacementNote.Length > 1000)
        {
            return DomainError.Validation("ApplicationPermission.ReplacementNoteTooLong", "Replacement note must not exceed 1000 characters.");
        }

        this.ReplacementFullPermissionKey = replacementFullPermissionKey.Trim().ToLowerInvariant();
        this.ReplacementNote = replacementNote.Trim();
        this.UpdatedBy = updatedBy;
        this.SetModified(dateTimeProvider.UtcNow);
        return Result.Success();
    }

    [GeneratedRegex(@"^[a-z][a-z0-9-]{1,62}:[a-z][a-z0-9-]{1,62}$")]
    private static partial Regex GeneratePermissionKeyRegex();
}
