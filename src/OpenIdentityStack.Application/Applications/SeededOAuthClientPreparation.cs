using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.ApplicationPermissions;
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

public sealed record SeededPermissionConfiguration(
    string PermissionKey,
    string DisplayName,
    string? Description,
    string? Category);

public sealed record SeededPermissionCatalogConfiguration(
    string ApplicationIdentifier,
    string DisplayName,
    string OwnerId,
    OwnerType OwnerType,
    IReadOnlyList<SeededPermissionConfiguration> Permissions);

/// <summary>Creates the domain authority records for controlled seed profiles before projecting protocol state.</summary>
public sealed class SeededOAuthClientPreparation(
    IApplicationRepository applications,
    IApplicationProtocolProjection projection,
    IResourceAccessRepository resources,
    IApplicationPermissionRegistryRepository registry,
    IDateTimeProvider clock)
{
    private static readonly DomainError identityMismatch = DomainError.Forbidden(
        "Application.SeedIdentityMismatch",
        "A seeded OAuth client or resource does not match its controlled configuration.");

    public async Task<Result<DomainApplication>> PrepareAsync(
        SeededOAuthClientConfiguration configuration,
        string? clientSecret,
        IReadOnlyList<SeededProtectedResourceConfiguration>? resourceConfigurations = null,
        SeededPermissionCatalogConfiguration? catalogConfiguration = null,
        CancellationToken cancellationToken = default)
        => await this.PrepareAsync(configuration, clientSecret, resourceConfigurations, catalogConfiguration,
            projectProtocolClient: true, cancellationToken);

    public async Task<Result<DomainApplication>> PrepareAuthorityOnlyAsync(
        SeededOAuthClientConfiguration configuration,
        IReadOnlyList<SeededProtectedResourceConfiguration> resourceConfigurations,
        CancellationToken cancellationToken = default)
        => await this.PrepareAsync(configuration, null, resourceConfigurations, null,
            projectProtocolClient: false, cancellationToken);

    private async Task<Result<DomainApplication>> PrepareAsync(
        SeededOAuthClientConfiguration configuration,
        string? clientSecret,
        IReadOnlyList<SeededProtectedResourceConfiguration>? resourceConfigurations,
        SeededPermissionCatalogConfiguration? catalogConfiguration,
        bool projectProtocolClient,
        CancellationToken cancellationToken)
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

        if (catalogConfiguration is not null)
        {
            Result catalogResult = await this.PrepareCatalogAsync(catalogConfiguration, cancellationToken);
            if (catalogResult.IsFailure) { return catalogResult.Error; }
        }

        if (projectProtocolClient)
        {
            Result projected = await projection.UpsertAsync(application, clientSecret, cancellationToken);
            if (projected.IsFailure) { return projected.Error; }
        }

        foreach (SeededProtectedResourceConfiguration resourceConfiguration in resourceConfigurations ?? [])
        {
            Result resourceResult = await PrepareResourceAsync(application, resourceConfiguration, cancellationToken);
            if (resourceResult.IsFailure) { return resourceResult.Error; }
        }

        return application;
    }

    private async Task<Result> PrepareCatalogAsync(
        SeededPermissionCatalogConfiguration configuration,
        CancellationToken cancellationToken)
    {
        RegisteredApplication? catalog = await registry.GetByIdentifierAsync(configuration.ApplicationIdentifier, cancellationToken);
        if (catalog is not null)
        {
            return Matches(catalog, configuration) ? Result.Success() : identityMismatch;
        }

        Result<RegisteredApplication> created = RegisteredApplication.Register(
            configuration.ApplicationIdentifier,
            configuration.DisplayName,
            description: null,
            configuration.OwnerId,
            configuration.OwnerType,
            configuration.Permissions.Select(static permission => (
                permission.PermissionKey,
                permission.DisplayName,
                permission.Description,
                permission.Category)),
            "deployment-seed",
            clock);
        if (created.IsFailure) { return created.Error; }
        await registry.AddAsync(created.Value, cancellationToken);
        await registry.SaveChangesAsync(cancellationToken);
        return Result.Success();
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

    private static bool Matches(RegisteredApplication application, SeededPermissionCatalogConfiguration configuration) =>
        application.Status == ApplicationLifecycleStatus.Active
        && !application.IsDeleted
        && string.Equals(application.ApplicationIdentifier, configuration.ApplicationIdentifier, StringComparison.Ordinal)
        && string.Equals(application.DisplayName, configuration.DisplayName, StringComparison.Ordinal)
        && string.Equals(application.OwnerId, configuration.OwnerId, StringComparison.Ordinal)
        && application.OwnerType == configuration.OwnerType
        && application.Permissions.Count == configuration.Permissions.Count
        && application.Permissions.All(permission =>
            !permission.IsRemoved
            && configuration.Permissions.Any(expected =>
                string.Equals(permission.PermissionKey, expected.PermissionKey, StringComparison.Ordinal)
                && string.Equals(permission.DisplayName, expected.DisplayName, StringComparison.Ordinal)
                && string.Equals(permission.Description, expected.Description, StringComparison.Ordinal)
                && string.Equals(permission.Category, expected.Category, StringComparison.Ordinal)));

    private static bool SameValues(IReadOnlyList<string> left, IReadOnlyList<string> right) =>
        left.Order(StringComparer.Ordinal).SequenceEqual(right.Order(StringComparer.Ordinal));
}
