namespace OpenIdentityStack.Application.ServicePermissions.Queries;

public sealed record ListRegisteredServicesQuery(
    int Page = 1,
    int PageSize = 20,
    string? StatusFilter = null,
    string? OwnerFilter = null,
    string? SearchTerm = null);

public sealed record GetRegisteredServiceQuery(Guid ServiceId);

public sealed record ListAssignablePermissionCatalogQuery(
    int Page = 1,
    int PageSize = 50,
    string? ServiceIdentifier = null,
    string? SearchTerm = null,
    bool AssignableOnly = true);
