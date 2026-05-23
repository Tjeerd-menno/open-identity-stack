namespace OpenIdentityStack.Application.ApplicationPermissions.Queries;

public sealed record ListRegisteredApplicationsQuery(
    int Page = 1,
    int PageSize = 20,
    string? StatusFilter = null,
    string? OwnerFilter = null,
    string? SearchTerm = null);

public sealed record GetRegisteredApplicationQuery(Guid ApplicationId);

public sealed record ListAssignablePermissionCatalogQuery(
    int Page = 1,
    int PageSize = 50,
    string? ApplicationIdentifier = null,
    string? SearchTerm = null,
    bool AssignableOnly = true);
