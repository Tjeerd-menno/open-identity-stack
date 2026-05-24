using OpenIdentityStack.Application.ApplicationPermissions.Dtos;

namespace OpenIdentityStack.Api.Admin.Responses.ApplicationPermissions;

public sealed record RegisterApplicationResponse(
    Guid ApplicationId,
    string ApplicationIdentifier,
    int PermissionsRegistered);

public sealed record RegisteredApplicationListResponse(
    IReadOnlyList<RegisteredApplicationSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record AssignablePermissionCatalogResponse(
    IReadOnlyList<ApplicationPermissionDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
