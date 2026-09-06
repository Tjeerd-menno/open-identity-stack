using OpenIdentityStack.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenIdentityStack.Domain.Applications;
using OpenIddict.Abstractions;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>
/// Synchronizes application domain state to OpenIddict protocol storage.
/// </summary>
public sealed class OpenIddictApplicationProjection : IApplicationProtocolProjection
{
    private const string applicationIdSetting = "openidentitystack:application-id";
    private const int placeholderSecretLengthBytes = 32;
    private static readonly DomainError projectionFailed =
        DomainError.Failure("Application.ProjectionFailed", "Failed to synchronize application protocol configuration.");
    private static readonly DomainError projectionConflict =
        DomainError.Conflict("Application.ProjectionConflict", "Administrative authority changed; reload before saving.");

    private readonly IOpenIddictApplicationManager applicationManager;
    private readonly IOpenIddictScopeManager scopeManager;
    private readonly ILogger<OpenIddictApplicationProjection> logger;

    public OpenIddictApplicationProjection(
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager,
        ILogger<OpenIddictApplicationProjection> logger)
    {
        this.applicationManager = applicationManager;
        this.scopeManager = scopeManager;
        this.logger = logger;
    }

    public Task<Result> UpsertAsync(DomainApplication application, CancellationToken cancellationToken = default) =>
        this.UpsertAsync(application, clientSecret: null, cancellationToken);

    public async Task<Result> UpsertAsync(
        DomainApplication application,
        string? clientSecret,
        CancellationToken cancellationToken = default)
    {
        try
        {
            object? existing = await this.applicationManager.FindByClientIdAsync(application.ClientId, cancellationToken);
            if (application.Status == ApplicationStatus.Disabled)
            {
                if (existing is not null)
                {
                    await this.applicationManager.DeleteAsync(existing, cancellationToken);
                }

                return Result.Success();
            }

            await this.EnsureScopesAsync(application.AllowedScopes, cancellationToken);

            var descriptor = new OpenIddictApplicationDescriptor();
            if (existing is not null)
            {
                await this.applicationManager.PopulateAsync(descriptor, existing, cancellationToken);
            }

            ApplyApplication(descriptor, application, clientSecret);

            if (existing is null)
            {
                await this.applicationManager.CreateAsync(descriptor, cancellationToken);
            }
            else
            {
                await this.applicationManager.UpdateAsync(existing, descriptor, cancellationToken);
            }

            return Result.Success();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            Log.ApplicationProjectionFailed(this.logger, ex, application.ClientId);
            return projectionConflict;
        }
        catch (Exception ex)
        {
            Log.ApplicationProjectionFailed(this.logger, ex, application.ClientId);
            return projectionFailed;
        }
    }

    public async Task<Result> DeleteAsync(DomainApplicationId applicationId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Buffer the streamed results before issuing further commands: ListAsync keeps a
            // data reader open for the lifetime of the enumeration, so calling GetSettingsAsync
            // or DeleteAsync inside the await foreach reuses the same connection mid-stream and
            // throws NpgsqlOperationInProgressException ("A command is already in progress").
            var applications = new List<object>();
            await foreach (object application in this.applicationManager.ListAsync(count: null, offset: null, cancellationToken))
            {
                applications.Add(application);
            }

            foreach (object application in applications)
            {
                System.Collections.Immutable.ImmutableDictionary<string, string> settings = await this.applicationManager.GetSettingsAsync(application, cancellationToken);
                if (settings.TryGetValue(applicationIdSetting, out string? projectedApplicationId) &&
                    string.Equals(projectedApplicationId, applicationId.Value.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    await this.applicationManager.DeleteAsync(application, cancellationToken);
                    break;
                }
            }

            return Result.Success();
        }
        catch (Exception ex)
        {
            Log.ApplicationProjectionDeleteFailed(this.logger, ex, applicationId.Value);
            return projectionFailed;
        }
    }

    private async Task EnsureScopesAsync(
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken)
    {
        foreach (string scope in scopes.Distinct(StringComparer.Ordinal))
        {
            if (await this.scopeManager.FindByNameAsync(scope, cancellationToken) is not null)
            {
                continue;
            }

            await this.scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = scope,
                DisplayName = $"{scope} Scope"
            }, cancellationToken);
        }
    }

    private static void ApplyApplication(
        OpenIddictApplicationDescriptor descriptor,
        DomainApplication application,
        string? clientSecret)
    {
        descriptor.ClientId = application.ClientId;
        descriptor.DisplayName = application.DisplayName;
        descriptor.ClientType = application.ClientType == OAuthClientType.Confidential
            ? OpenIddictConstants.ClientTypes.Confidential
            : OpenIddictConstants.ClientTypes.Public;
        if (clientSecret is not null)
        {
            descriptor.ClientSecret = clientSecret;
        }
        else if (application.ClientType == OAuthClientType.Confidential && string.IsNullOrEmpty(descriptor.ClientSecret))
        {
            descriptor.ClientSecret = GeneratePlaceholderClientSecret();
        }

        descriptor.ApplicationType = application.Profile is ApplicationProfile.Native or ApplicationProfile.SinglePage or ApplicationProfile.Device
            ? OpenIddictConstants.ApplicationTypes.Native
            : OpenIddictConstants.ApplicationTypes.Web;
        descriptor.ConsentType = application.RequireConsent
            ? OpenIddictConstants.ConsentTypes.Explicit
            : OpenIddictConstants.ConsentTypes.Implicit;

        descriptor.RedirectUris.Clear();
        foreach (string redirectUri in application.RedirectUris)
        {
            descriptor.RedirectUris.Add(new Uri(redirectUri));
        }

        descriptor.PostLogoutRedirectUris.Clear();
        foreach (string postLogoutRedirectUri in application.PostLogoutRedirectUris)
        {
            descriptor.PostLogoutRedirectUris.Add(new Uri(postLogoutRedirectUri));
        }

        descriptor.Permissions.Clear();
        AddGrantTypePermissions(descriptor, application.AllowedGrantTypes);
        foreach (string scope in application.AllowedScopes.Distinct(StringComparer.Ordinal))
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
        }

        descriptor.Requirements.Clear();
        if (application.RequirePkce)
        {
            descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
        }

        descriptor.Settings[applicationIdSetting] = application.Id.Value.ToString();
    }

    private static string GeneratePlaceholderClientSecret()
    {
        byte[] randomBytes = new byte[placeholderSecretLengthBytes];
        System.Security.Cryptography.RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private static void AddGrantTypePermissions(
        OpenIddictApplicationDescriptor descriptor,
        IReadOnlyList<string> grantTypes)
    {
        foreach (string grantType in grantTypes)
        {
            switch (grantType)
            {
                case OpenIddictConstants.GrantTypes.AuthorizationCode:
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
                    break;

                case OpenIddictConstants.GrantTypes.ClientCredentials:
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
                    break;

                case OpenIddictConstants.GrantTypes.RefreshToken:
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
                    break;

                case OpenIddictConstants.GrantTypes.DeviceCode:
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.DeviceCode);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.DeviceAuthorization);
                    descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
                    break;
            }
        }

        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Introspection);
        descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Revocation);

        if (grantTypes.Any(grantType => grantType.Equals(OpenIddictConstants.GrantTypes.AuthorizationCode, StringComparison.OrdinalIgnoreCase)))
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
        }
    }
}

internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to project application '{ClientId}' to OpenIddict.")]
    internal static partial void ApplicationProjectionFailed(ILogger logger, Exception ex, string clientId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete projected OpenIddict application for application '{ApplicationId}'.")]
    internal static partial void ApplicationProjectionDeleteFailed(ILogger logger, Exception ex, Guid applicationId);
}
