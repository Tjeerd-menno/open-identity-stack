using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.Resources;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Application.Applications;

public sealed record SeededOAuthClientConfiguration(
    string ClientId,
    string DisplayName,
    ApplicationProfile Profile,
    OAuthClientType ClientType,
    IReadOnlyList<string> AllowedGrantTypes,
    IReadOnlyList<string> AllowedScopes,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    bool RequirePkce,
    bool RequireConsent);

public sealed record SeededProtectedResourceConfiguration(
    string Audience,
    string Scope,
    string DisplayName,
    IReadOnlyList<string> PermissionNamespaces,
    IReadOnlyList<string> DelegatedPermissions);

/// <summary>Creates the domain authority records for controlled seed profiles before projecting protocol state.</summary>
public sealed class SeededOAuthClientPreparation(
    IApplicationRepository applications,
    IApplicationProtocolProjection projection,
    IResourceAccessRepository resources,
    IDateTimeProvider clock)
{
    private static readonly DomainError identityMismatch = DomainError.Forbidden(
        "Application.SeedIdentityMismatch",
        "A seeded OAuth client or resource does not match its controlled configuration.");

    public async Task<Result<DomainApplication>> PrepareAsync(
        SeededOAuthClientConfiguration configuration,
        string? clientSecret,
        IReadOnlyList<SeededProtectedResourceConfiguration>? resourceConfigurations = null,
        CancellationToken cancellationToken = default)
    {
        DomainApplication? application = await applications.GetByClientIdAsync(configuration.ClientId, cancellationToken);
        if (application is null)
        {
            Result<DomainApplication> created = DomainApplication.Create(
                configuration.ClientId, configuration.DisplayName, null, configuration.Profile, configuration.ClientType,
                configuration.AllowedGrantTypes, configuration.AllowedScopes, configuration.RedirectUris,
                configuration.PostLogoutRedirectUris, configuration.RequirePkce, configuration.RequireConsent, clock);
            if (created.IsFailure) { return created.Error; }
            application = created.Value;
            await applications.AddAsync(application, cancellationToken);
            await applications.SaveChangesAsync(cancellationToken);
        }
        else if (!Matches(application, configuration))
        {
            return identityMismatch;
        }

        Result projected = await projection.UpsertAsync(application, clientSecret, cancellationToken);
        if (projected.IsFailure) { return projected.Error; }

        foreach (SeededProtectedResourceConfiguration resourceConfiguration in resourceConfigurations ?? [])
        {
            Result resourceResult = await PrepareResourceAsync(application, resourceConfiguration, cancellationToken);
            if (resourceResult.IsFailure) { return resourceResult.Error; }
        }

        return application;
    }

    private async Task<Result> PrepareResourceAsync(
        DomainApplication application,
        SeededProtectedResourceConfiguration configuration,
        CancellationToken cancellationToken)
    {
        ProtectedResource? resource = await resources.FindByScopeAsync(configuration.Scope, cancellationToken);
        if (resource is null)
        {
            Result<ProtectedResource> created = ProtectedResource.Create(
                configuration.Audience, configuration.Scope, configuration.DisplayName, configuration.PermissionNamespaces);
            if (created.IsFailure) { return created.Error; }
            resource = created.Value;
            resources.AddResource(resource);
        }
        else if (!resource.Enabled
            || !string.Equals(resource.Audience, configuration.Audience, StringComparison.Ordinal)
            || !string.Equals(resource.DisplayName, configuration.DisplayName, StringComparison.Ordinal)
            || !SameValues(resource.PermissionNamespaces, configuration.PermissionNamespaces))
        {
            return identityMismatch;
        }

        ClientResourceGrant? grant = await resources.GetGrantAsync(application.Id, resource.Id, cancellationToken);
        if (grant is not null)
        {
            return SameValues(grant.DelegatedPermissions, configuration.DelegatedPermissions)
                && grant.ApplicationPermissions.Count == 0 ? Result.Success() : identityMismatch;
        }

        Result<ClientResourceGrant> createdGrant = ClientResourceGrant.Create(
            application.Id, resource.Id, configuration.DelegatedPermissions, []);
        if (createdGrant.IsFailure) { return createdGrant.Error; }
        resources.AddGrant(createdGrant.Value);
        await resources.SaveChangesAsync("deployment-seed", "SeededClient.ResourceAccessPrepared",
            application.Id.Value.ToString(), resource, cancellationToken);
        return Result.Success();
    }

    private static bool Matches(DomainApplication application, SeededOAuthClientConfiguration configuration) =>
        application.Status == ApplicationStatus.Active
        && application.Profile == configuration.Profile
        && application.ClientType == configuration.ClientType
        && application.RequirePkce == configuration.RequirePkce
        && application.RequireConsent == configuration.RequireConsent
        && string.Equals(application.DisplayName, configuration.DisplayName, StringComparison.Ordinal)
        && SameValues(application.AllowedGrantTypes, configuration.AllowedGrantTypes)
        && SameValues(application.AllowedScopes, configuration.AllowedScopes)
        && SameValues(application.RedirectUris, configuration.RedirectUris)
        && SameValues(application.PostLogoutRedirectUris, configuration.PostLogoutRedirectUris);

    private static bool SameValues(IReadOnlyList<string> left, IReadOnlyList<string> right) =>
        left.Order(StringComparer.Ordinal).SequenceEqual(right.Order(StringComparer.Ordinal));
}
