using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.AdministrativeAccess;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.Resources;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Infrastructure.Persistence;

/// <summary>Uncached persisted preparation checks, also used inside the serializable cutover transaction.</summary>
public sealed class CredentialCutoverResourceInventory(OpenIdentityStackDbContext db, IConfiguration configuration,
    IHostEnvironment environment) : ICredentialCutoverResourceInventory
{
    public async Task<CutoverResourceInventory> ReadAsync(CancellationToken cancellationToken = default)
    {
        List<DomainApplication> applications = await db.Applications.AsNoTracking().Include(x => x.Credentials).ToListAsync(cancellationToken);
        List<ProtectedResource> resources = await db.ProtectedResources.AsNoTracking().ToListAsync(cancellationToken);
        List<ClientResourceGrant> grants = await db.ClientResourceGrants.AsNoTracking()
            .Where(x => x.ResourceId == ProtectedResource.AdministrativeResourceId).ToListAsync(cancellationToken);
        var blockers = new List<CutoverBlocker>();
        var clients = new List<CutoverAdministrativeClient>();
        foreach (DomainApplication application in applications.OrderBy(x => x.ClientId, StringComparer.Ordinal))
        {
            ClientResourceGrant? grant = grants.SingleOrDefault(x => x.ClientApplicationId == application.Id);
            bool approved = grant is not null && (grant.DelegatedPermissions.Count > 0 || grant.ApplicationPermissions.Count > 0);
            bool active = application.Status == ApplicationStatus.Active;
            if (application.ClientId == ManagementWebPreparation.ClientId || grant is not null || application.AllowedScopes.Contains(ProtectedResource.AdministrativeScope))
            {
                clients.Add(new(application.Id.Value, application.ClientId, active, approved,
                    grant?.DelegatedPermissions ?? [], grant?.ApplicationPermissions ?? [], application.RequiresMigrationReview));
                if (active && application.AllowedScopes.Contains(ProtectedResource.AdministrativeScope) && !approved)
                {
                    blockers.Add(new("Administrative.ClientUnapproved", $"Review administrative access for client {application.ClientId}."));
                }
            }
            if (active && application.RequiresMigrationReview)
            {
                blockers.Add(new("Administrative.ClientMigrationUnresolved", $"Complete migration review for client {application.ClientId}."));
            }
            foreach (string scope in application.AllowedScopes.Where(scope => !ProtectedResource.IsProtocolScope(scope) && !resources.Any(resource => resource.Scope == scope)))
            {
                blockers.Add(new("Resource.UnmappedScope", $"Map legacy scope {scope} used by client {application.ClientId} to a reviewed resource."));
            }
        }
        DomainApplication? management = applications.SingleOrDefault(x => x.ClientId == ManagementWebPreparation.ClientId);
        ClientResourceGrant? managementGrant = management is null ? null : grants.SingleOrDefault(x => x.ClientApplicationId == management.Id);
        string[] required = ["users:read", "applications:read", "sessions:revoke"];
        bool permissionReady = managementGrant is not null && required.All(permission =>
            managementGrant.DelegatedPermissions.Contains("*", StringComparer.Ordinal) || managementGrant.DelegatedPermissions.Contains(permission, StringComparer.Ordinal));
        if (management is null || !permissionReady || !this.IsManagementPrepared(management))
        {
            blockers.Add(new("Administrative.ManagementWebUnprepared", "Prepare and approve the configured Management Web client with delegated access to users, applications and credential cutover."));
        }
        return new(clients, resources.Where(x => !x.IsAdministrative).Select(x => new CutoverProtectedResource(x.Id, x.DisplayName, x.Audience, x.Scope, x.Revision)).ToArray(), blockers);
    }

    private bool IsManagementPrepared(DomainApplication client)
    {
        List<string> redirects = Configured("RedirectUris");
        List<string> logout = Configured("PostLogoutRedirectUris");
        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            foreach (int port in new[] { 5175, 5173, 5174, 3000 })
            {
                redirects.Add($"http://localhost:{port}/auth/callback");
                redirects.Add($"http://localhost:{port}/auth/silent-callback");
                logout.Add($"http://localhost:{port}/");
            }
        }
        return redirects.Count > 0 && logout.Count > 0 && client.Status == ApplicationStatus.Active && !client.RequiresMigrationReview
            && client.Profile == ApplicationProfile.SinglePage && client.ClientType == OAuthClientType.Public && client.RequirePkce && client.Credentials.Count == 0
            && client.AllowedScopes.Contains(ProtectedResource.AdministrativeScope)
            && client.AllowedGrantTypes.Contains("authorization_code") && !client.AllowedGrantTypes.Except(["authorization_code", "refresh_token"], StringComparer.Ordinal).Any()
            && client.RedirectUris.Order(StringComparer.Ordinal).SequenceEqual(redirects.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            && client.PostLogoutRedirectUris.Order(StringComparer.Ordinal).SequenceEqual(logout.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

        List<string> Configured(string key) => configuration.GetSection($"OpenIddict:Clients:ManagementWeb:{key}").Get<string[]>()?
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.Ordinal).ToList() ?? [];
    }
}
