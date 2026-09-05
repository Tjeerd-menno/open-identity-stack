using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Domain.Resources;
using SharedKernel;
using ApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Application.AdministrativeAccess;

public sealed record AdministrativeAccessDto(bool Approved, IReadOnlyList<string> DelegatedPermissions, IReadOnlyList<string> ApplicationPermissions, long? Revision);
public sealed record AdministrativeAccessConfiguration(IReadOnlyList<string> DelegatedPermissions, IReadOnlyList<string> ApplicationPermissions, long? ExpectedRevision, bool AcknowledgeAdministrativeAccess = false);

/// <summary>The reserved Admin resource grant is the persisted administrative entitlement and ceiling.</summary>
public sealed class AdministrativeAccessWorkflow(
    IResourceAccessRepository resources,
    IApplicationRepository applications,
    IAdministrativeApproval approval) : IAdministrativeClientGuard
{
    private static readonly DomainError conflict = DomainError.Conflict("AdministrativeAccess.Conflict", "Administrative access changed; reload before saving.");

    public async Task<Result<AdministrativeAccessDto>> GetAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var clientId = new ApplicationId(applicationId);
        if (await applications.GetByIdAsync(clientId, cancellationToken) is null) { return ResourceAccessErrors.NotGranted; }
        return Map(await resources.GetGrantAsync(clientId, ProtectedResource.AdministrativeResourceId, cancellationToken));
    }

    public async Task<Result<AdministrativeAccessDto>> SaveAsync(Guid applicationId, AdministrativeAccessConfiguration request, string actorId, CancellationToken cancellationToken = default)
    {
        await approval.CaptureAuthorityAsync(cancellationToken);
        if (request.DelegatedPermissions is null || request.ApplicationPermissions is null
            || request.DelegatedPermissions.Count > 500 || request.ApplicationPermissions.Count > 500)
        {
            return ResourceAccessErrors.InvalidConfiguration;
        }
        var clientId = new ApplicationId(applicationId);
        if (await applications.GetByIdAsync(clientId, cancellationToken) is null) { return ResourceAccessErrors.NotGranted; }
        ClientResourceGrant? grant = await resources.GetGrantAsync(clientId, ProtectedResource.AdministrativeResourceId, cancellationToken);
        if (request.ExpectedRevision != grant?.Revision) { return conflict; }
        if (request.DelegatedPermissions.Concat(request.ApplicationPermissions).Any(permission => !IsPlatformPermission(permission)))
        {
            return ResourceAccessErrors.InvalidConfiguration;
        }
        bool expanded = grant is null
            || Expands(grant.DelegatedPermissions, request.DelegatedPermissions)
            || Expands(grant.ApplicationPermissions, request.ApplicationPermissions);
        if (expanded)
        {
            Result result = await approval.RequireAsync("AdministrativeClient.ApproveOrExpand", applicationId.ToString(), request.AcknowledgeAdministrativeAccess, cancellationToken);
            if (result.IsFailure) { return result.Error; }
        }
        if (grant is null)
        {
            Result<ClientResourceGrant> created = ClientResourceGrant.Create(clientId, ProtectedResource.AdministrativeResourceId, request.DelegatedPermissions, request.ApplicationPermissions);
            if (created.IsFailure) { return created.Error; }
            grant = created.Value;
            resources.AddGrant(grant);
        }
        else
        {
            Result configured = grant.Configure(request.DelegatedPermissions, request.ApplicationPermissions);
            if (configured.IsFailure) { return configured.Error; }
        }
        try
        {
            await resources.SaveChangesAsync(actorId, "AdministrativeClient.EntitlementChanged", applicationId.ToString(), cancellationToken: cancellationToken);
        }
        catch (ResourceAccessConflictException)
        {
            return conflict;
        }
        await approval.RecordOutcomeAsync(true, cancellationToken);
        return Map(grant);
    }

    public async Task<Result> RequireAsync(ApplicationId applicationId, string operation, CancellationToken cancellationToken = default)
    {
        ClientResourceGrant? grant = await resources.GetGrantAsync(applicationId, ProtectedResource.AdministrativeResourceId, cancellationToken);
        return grant is not null && (grant.DelegatedPermissions.Count > 0 || grant.ApplicationPermissions.Count > 0)
            ? await approval.RequireAsync(operation, applicationId.Value.ToString(), cancellationToken: cancellationToken)
            : Result.Success();
    }

    public Task RecordOutcomeAsync(CancellationToken cancellationToken = default) => approval.RecordOutcomeAsync(true, cancellationToken);
    public Task CaptureAuthorityAsync(CancellationToken cancellationToken = default) => approval.CaptureAuthorityAsync(cancellationToken);

    private static bool IsPlatformPermission(string permission) => !string.IsNullOrWhiteSpace(permission) && (permission == "*"
        || Permissions.GetAllPermissions().Contains(permission, StringComparer.OrdinalIgnoreCase)
        || permission.EndsWith(":*", StringComparison.Ordinal) && permission.Count(character => character == ':') == 1
            && Permissions.GetAllPermissions().Any(candidate => PermissionSemantics.Matches(permission, candidate)));

    private static bool Expands(IReadOnlyList<string> current, IReadOnlyList<string> proposed) =>
        proposed.Any(permission => !current.Any(existing => existing == "*" || string.Equals(existing, permission, StringComparison.OrdinalIgnoreCase)
            || PermissionSemantics.Matches(existing, permission)));

    private static AdministrativeAccessDto Map(ClientResourceGrant? grant) => grant is null ? new(false, [], [], null)
        : new(grant.DelegatedPermissions.Count > 0 || grant.ApplicationPermissions.Count > 0, grant.DelegatedPermissions, grant.ApplicationPermissions, grant.Revision);
}
