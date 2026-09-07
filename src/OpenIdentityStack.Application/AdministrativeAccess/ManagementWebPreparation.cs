using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.Resources;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Application.AdministrativeAccess;

/// <summary>Controlled deployment preparation for the fixed Management Web registration; never a runtime approval bypass.</summary>
public sealed class ManagementWebPreparation(IApplicationRepository applications, IApplicationProtocolProjection projection,
    IResourceAccessRepository resources, IDateTimeProvider clock)
{
    public const string ClientId = "management-web-client";
    private static readonly string[] scopes = ["openid", "profile", "email", ProtectedResource.AdministrativeScope];
    private static readonly string[] grantTypes = ["authorization_code", "refresh_token"];

    public async Task<Result> PrepareAsync(IReadOnlyList<string> redirectUris, IReadOnlyList<string> postLogoutRedirectUris,
        bool bootstrapApproval = false, CancellationToken cancellationToken = default)
    {
        DomainApplication? client = await applications.GetByClientIdAsync(ClientId, cancellationToken);
        if (client is null)
        {
            Result<DomainApplication> created = DomainApplication.Create(ClientId, "Management Web Application", null,
                ApplicationProfile.SinglePage, OAuthClientType.Public, grantTypes, scopes, redirectUris, postLogoutRedirectUris,
                true, false, clock);
            if (created.IsFailure) { return created.Error; }
            client = created.Value;
            await applications.AddAsync(client, cancellationToken);
            await applications.SaveChangesAsync(cancellationToken);
        }

        if (client.Status != ApplicationStatus.Active || client.ClientType != OAuthClientType.Public || !client.RequirePkce
            || client.RequireConsent || client.Profile != ApplicationProfile.SinglePage || client.Credentials.Count != 0
            || !client.RedirectUris.Order(StringComparer.Ordinal).SequenceEqual(redirectUris.Order(StringComparer.Ordinal))
            || !client.PostLogoutRedirectUris.Order(StringComparer.Ordinal).SequenceEqual(postLogoutRedirectUris.Order(StringComparer.Ordinal))
            || !client.AllowedGrantTypes.Order(StringComparer.Ordinal).SequenceEqual(grantTypes.Order(StringComparer.Ordinal))
            || !client.AllowedScopes.Order(StringComparer.Ordinal).SequenceEqual(scopes.Order(StringComparer.Ordinal)))
        {
            return DomainError.Forbidden("AdministrativeAccess.BootstrapIdentityMismatch", "The Management Web registration does not match the independently reviewed deployment configuration. Reconcile it through the approved administrative workflow before preparing it.");
        }
        ClientResourceGrant? existing = await resources.GetGrantAsync(client.Id, ProtectedResource.AdministrativeResourceId, cancellationToken);
        Result projected = await projection.UpsertAsync(client, cancellationToken);
        if (projected.IsFailure) { return projected.Error; }
        if (!bootstrapApproval || existing is not null) { return Result.Success(); }

        Result<ClientResourceGrant> grant = ClientResourceGrant.Create(client.Id, ProtectedResource.AdministrativeResourceId, ["*"], []);
        if (grant.IsFailure) { return grant.Error; }
        resources.AddGrant(grant.Value);
        await resources.SaveChangesAsync("deployment-bootstrap", "AdministrativeClient.ManagementWebBootstrapApproved", client.Id.Value.ToString(), cancellationToken: cancellationToken);
        return Result.Success();
    }
}
