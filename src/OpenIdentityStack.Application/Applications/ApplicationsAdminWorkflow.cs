using OpenIdentityStack.Application.Applications.Commands;
using OpenIdentityStack.Application.Applications.Queries;
using OpenIdentityStack.Application.Common;
using OpenIdentityStack.Domain.Applications;
using SharedKernel;

namespace OpenIdentityStack.Application.Applications;

public sealed class ApplicationsAdminWorkflow : IApplicationsAdminWorkflow
{
    private readonly ApplicationLifecycleUseCases lifecycleUseCases;
    private readonly ApplicationCredentialUseCases credentialUseCases;
    private readonly ListApplicationsQueryHandler listApplicationsQueryHandler;
    private readonly GetApplicationQueryHandler getApplicationQueryHandler;
    private readonly ListApplicationCredentialsQueryHandler listApplicationCredentialsQueryHandler;
    private readonly ListApplicationProfilePoliciesQueryHandler listApplicationProfilePoliciesQueryHandler;

    public ApplicationsAdminWorkflow(
        ApplicationLifecycleUseCases lifecycleUseCases,
        ApplicationCredentialUseCases credentialUseCases,
        ListApplicationsQueryHandler listApplicationsQueryHandler,
        GetApplicationQueryHandler getApplicationQueryHandler,
        ListApplicationCredentialsQueryHandler listApplicationCredentialsQueryHandler,
        ListApplicationProfilePoliciesQueryHandler listApplicationProfilePoliciesQueryHandler)
    {
        this.lifecycleUseCases = lifecycleUseCases;
        this.credentialUseCases = credentialUseCases;
        this.listApplicationsQueryHandler = listApplicationsQueryHandler;
        this.getApplicationQueryHandler = getApplicationQueryHandler;
        this.listApplicationCredentialsQueryHandler = listApplicationCredentialsQueryHandler;
        this.listApplicationProfilePoliciesQueryHandler = listApplicationProfilePoliciesQueryHandler;
    }

    public Task<Result<PagedResult<ApplicationSummary>>> ListAsync(
        ListApplicationsAdminWorkflowRequest request,
        CancellationToken cancellationToken = default) =>
        this.listApplicationsQueryHandler.HandleAsync(
            request.Page,
            request.PageSize,
            request.Profile,
            request.Status,
            request.ClientType,
            request.SearchTerm,
            cancellationToken);

    public Task<Result<ApplicationDetails>> GetDetailsAsync(
        GetApplicationAdminWorkflowRequest request,
        CancellationToken cancellationToken = default) =>
        this.getApplicationQueryHandler.HandleAsync(request.ApplicationId, cancellationToken);

    public async Task<Result<ApplicationCreateOperationResult>> CreateAsync(
        CreateApplicationAdminWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        Result<ApplicationCredentialType?> initialCredentialTypeResult = ValidateInitialCredential(request);
        if (initialCredentialTypeResult.IsFailure)
        {
            return initialCredentialTypeResult.Error;
        }

        Result<ApplicationCreateCommandResult> result = await this.lifecycleUseCases.ExecuteCreateAsync(
            new CreateApplicationCommand(
                request.ClientId,
                request.DisplayName,
                request.Description,
                request.Profile,
                request.ClientType,
                request.AllowedGrantTypes,
                request.AllowedScopes,
                request.RedirectUris,
                request.PostLogoutRedirectUris,
                request.RequirePkce,
                request.RequireConsent),
            initialCredentialTypeResult.Value == ApplicationCredentialType.ClientSecret && request.InitialCredential is not null
                ? new CreateApplicationInitialSecretCommand(
                    request.InitialCredential.Description,
                    request.InitialCredential.ExpiresAt)
                : null,
            cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        return new ApplicationCreateOperationResult(
            result.Value.Application.Details,
            result.Value.InitialSecret);
    }

    public async Task<Result<ApplicationDetails>> UpdateMetadataAsync(
        UpdateApplicationMetadataAdminWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        Result<ApplicationCommandResult> result = await this.lifecycleUseCases.ExecuteAsync(
            new UpdateApplicationMetadataCommand(
                request.ApplicationId,
                request.DisplayName,
                request.Description),
            cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        return result.Value.Details;
    }

    public async Task<Result<ApplicationDetails>> ConfigureOAuthAsync(
        ConfigureApplicationOAuthAdminWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        Result<ApplicationCommandResult> result = await this.lifecycleUseCases.ExecuteAsync(
            new ConfigureApplicationOAuthCommand(
                request.ApplicationId,
                request.Profile,
                request.ClientType,
                request.AllowedGrantTypes,
                request.AllowedScopes,
                request.RedirectUris,
                request.PostLogoutRedirectUris,
                request.RequirePkce,
                request.RequireConsent),
            cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        return result.Value.Details;
    }

    public async Task<Result<ApplicationDetails>> EnableAsync(
        EnableApplicationAdminWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        Result<ApplicationCommandResult> result = await this.lifecycleUseCases.ExecuteWithDetailsAsync(
            new EnableApplicationCommand(request.ApplicationId),
            cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        return result.Value.Details;
    }

    public async Task<Result<ApplicationDetails>> DisableAsync(
        DisableApplicationAdminWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        Result<ApplicationCommandResult> result = await this.lifecycleUseCases.ExecuteWithDetailsAsync(
            new DisableApplicationCommand(request.ApplicationId),
            cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        return result.Value.Details;
    }

    public Task<Result> DeleteAsync(
        DeleteApplicationAdminWorkflowRequest request,
        CancellationToken cancellationToken = default) =>
        this.lifecycleUseCases.ExecuteAsync(new DeleteApplicationCommand(request.ApplicationId), cancellationToken);

    public Task<Result<IReadOnlyList<ApplicationCredentialDetails>>> ListCredentialsAsync(
        ListApplicationCredentialsAdminWorkflowRequest request,
        CancellationToken cancellationToken = default) =>
        this.listApplicationCredentialsQueryHandler.HandleAsync(
            new ListApplicationCredentialsQuery(request.ApplicationId),
            cancellationToken);

    public async Task<Result<ApplicationSecretOperationResult>> AddSecretAsync(
        AddApplicationSecretAdminWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        Result<ApplicationCredentialCommandResult> result = await this.credentialUseCases.ExecuteAsync(
            new AddApplicationSecretCommand(
                request.ApplicationId,
                request.Description,
                request.ExpiresAt,
                request.RevokeExisting),
            cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        if (string.IsNullOrWhiteSpace(result.Value.OneTimeSecret))
        {
            return DomainError.Failure(
                "ApplicationCredential.OneTimeSecretMissing",
                "Adding an application secret must return a one-time secret.");
        }

        return new ApplicationSecretOperationResult(
            result.Value.CredentialId,
            result.Value.OneTimeSecret);
    }

    public Task<Result<ApplicationCredentialCommandResult>> AddCertificateAsync(
        AddApplicationCertificateAdminWorkflowRequest request,
        CancellationToken cancellationToken = default) =>
        this.credentialUseCases.ExecuteAsync(
            new AddApplicationCertificateCommand(
                request.ApplicationId,
                request.Thumbprint,
                request.Subject,
                request.Description,
                request.ExpiresAt),
            cancellationToken);

    public Task<Result> RevokeCredentialAsync(
        RevokeApplicationCredentialAdminWorkflowRequest request,
        CancellationToken cancellationToken = default) =>
        this.credentialUseCases.ExecuteAsync(
            new RevokeApplicationCredentialCommand(request.ApplicationId, request.CredentialId),
            cancellationToken);

    public Task<Result<IReadOnlyList<ApplicationProfilePolicyDetails>>> ListProfilePoliciesAsync(
        ListApplicationProfilePoliciesAdminWorkflowRequest request,
        CancellationToken cancellationToken = default) =>
        this.listApplicationProfilePoliciesQueryHandler.HandleAsync(cancellationToken);

    private static Result<ApplicationCredentialType?> ValidateInitialCredential(
        CreateApplicationAdminWorkflowRequest request)
    {
        if (request.InitialCredential is null)
        {
            return (ApplicationCredentialType?)null;
        }

        if (string.IsNullOrWhiteSpace(request.InitialCredential.Type) ||
            !TryParseApplicationCredentialType(request.InitialCredential.Type, out ApplicationCredentialType type))
        {
            return DomainError.Validation(
                "ApplicationCredential.InitialCredentialTypeInvalid",
                "Initial credential type is invalid.");
        }

        if (type != ApplicationCredentialType.ClientSecret)
        {
            return DomainError.Validation(
                "ApplicationCredential.InitialCredentialTypeUnsupported",
                "Only client secret initial credentials are supported.");
        }

        if (request.ClientType == OAuthClientType.Public)
        {
            return ApplicationErrors.CredentialsNotAllowedForPublic;
        }

        return type;
    }

    private static bool TryParseApplicationCredentialType(string value, out ApplicationCredentialType type)
    {
        if (Enum.TryParse(value, ignoreCase: true, out type))
        {
            return true;
        }

        string normalized = value.Replace("-", string.Empty, StringComparison.Ordinal);
        return Enum.TryParse(normalized, ignoreCase: true, out type);
    }
}
