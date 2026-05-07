using OpenIdentityStack.Application.ServicePermissions.Dtos;

namespace OpenIdentityStack.Api.Admin.Responses.ServicePermissions;

public sealed record RegisterServiceResponse(
    Guid ServiceId,
    string ServiceIdentifier,
    int PermissionsRegistered);

public sealed record RegisteredServiceListResponse(
    IReadOnlyList<RegisteredServiceSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record AssignablePermissionCatalogResponse(
    IReadOnlyList<ServicePermissionDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
