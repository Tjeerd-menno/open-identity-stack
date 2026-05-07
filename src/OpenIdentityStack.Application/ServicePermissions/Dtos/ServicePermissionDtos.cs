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
    IReadOnlyList<ServicePermissionDto> Permissions);

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
    DateTimeOffset? UpdatedAt);

public sealed record RegisteredServiceSummaryDto(
    Guid Id,
    string ServiceIdentifier,
    string DisplayName,
    string OwnerId,
    string Status,
    int PermissionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
