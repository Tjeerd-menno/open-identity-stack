namespace OpenIdentityStack.Application.ServicePermissions.Dtos;

public sealed record RegisteredServiceDto(
    Guid Id,
    string ServiceIdentifier,
    string DisplayName,
    string? Description,
    string OwnerId,
    string OwnerType,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    uint ConcurrencyToken,
    IReadOnlyList<ServicePermissionDto> Permissions,
    IReadOnlyList<DelegatedMaintainerDto> Maintainers);

public sealed record ServicePermissionDto(
    Guid Id,
    string PermissionKey,
    string FullPermissionKey,
    string DisplayName,
    string? Description,
    string? IntendedUse,
    string? DocumentationUrl,
    string Status,
    bool IsAssignable,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? DeprecatedAt,
    DateTimeOffset? DisabledAt,
    DateTimeOffset? RetiredAt);

public sealed record DelegatedMaintainerDto(
    Guid Id,
    string PrincipalId,
    string PrincipalType,
    string GrantedBy,
    DateTimeOffset GrantedAt);

public sealed record RegisteredServiceSummaryDto(
    Guid Id,
    string ServiceIdentifier,
    string DisplayName,
    string OwnerId,
    string Status,
    int PermissionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
