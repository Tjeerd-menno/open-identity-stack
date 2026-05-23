namespace OpenIdentityStack.Application.ApplicationPermissions.Dtos;

public sealed record RegisteredApplicationDto(
    Guid Id,
    string ApplicationIdentifier,
    string DisplayName,
    string? Description,
    string OwnerId,
    string OwnerType,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    uint ConcurrencyToken,
    IReadOnlyList<ApplicationPermissionDto> Permissions,
    IReadOnlyList<DelegatedMaintainerDto> Maintainers);

public sealed record ApplicationPermissionDto(
    Guid Id,
    string PermissionKey,
    string FullPermissionKey,
    string DisplayName,
    string? Description,
    string? Category,
    string Status,
    bool IsAssignable,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? DeprecatedAt,
    DateTimeOffset? DisabledAt,
    DateTimeOffset? RetiredAt,
    string? ApplicationId = null,
    string? ApplicationName = null,
    string? ApplicationVersion = null);

public sealed record DelegatedMaintainerDto(
    Guid Id,
    string PrincipalId,
    string PrincipalType,
    string GrantedBy,
    DateTimeOffset GrantedAt);

public sealed record RegisteredApplicationSummaryDto(
    Guid Id,
    string ApplicationIdentifier,
    string DisplayName,
    string OwnerId,
    string Status,
    int PermissionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
