using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Applications;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Application.Applications.Commands;

public sealed class ApplicationLifecycleUseCases :
    ICreateApplicationUseCase,
    IUpdateApplicationMetadataUseCase,
    IConfigureApplicationOAuthUseCase,
    IEnableApplicationUseCase,
    IDisableApplicationUseCase,
    IDeleteApplicationUseCase
{
    private readonly IApplicationRepository repository;
    private readonly IApplicationProtocolProjection projection;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;

    public ApplicationLifecycleUseCases(
        IApplicationRepository repository,
        IApplicationProtocolProjection projection,
        IDateTimeProvider dateTimeProvider,
        IAuditLog auditLog)
    {
        this.repository = repository;
        this.projection = projection;
        this.dateTimeProvider = dateTimeProvider;
        this.auditLog = auditLog;
    }

    public async Task<Result<ApplicationCommandResult>> ExecuteAsync(
        CreateApplicationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!ApplicationProfilePolicyCatalog.GetPolicy(command.Profile).IsSelectable)
        {
            return ApplicationErrors.ProfileNotAvailable;
        }

        if (await this.repository.ExistsByClientIdAsync(command.ClientId, cancellationToken))
        {
            return ApplicationErrors.ClientIdExists;
        }

        Result<DomainApplication> createResult = DomainApplication.Create(
            command.ClientId,
            command.DisplayName,
            command.Description,
            command.Profile,
            command.ClientType,
            command.AllowedGrantTypes,
            command.AllowedScopes,
            command.RedirectUris,
            command.PostLogoutRedirectUris,
            command.RequirePkce,
            command.RequireConsent,
            this.dateTimeProvider);
        if (createResult.IsFailure)
        {
            return createResult.Error;
        }

        DomainApplication application = createResult.Value;
        await this.repository.AddAsync(application, cancellationToken);

        Result projectionResult = await this.projection.UpsertAsync(application, cancellationToken);
        if (projectionResult.IsFailure)
        {
            return projectionResult.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken);
        await this.auditLog.LogAsync(
            "system",
            "Application.Created",
            "Application",
            application.Id.Value.ToString(),
            $"ClientId: {application.ClientId}, DisplayName: {application.DisplayName}",
            cancellationToken);

        return ToCommandResult(application);
    }

    public async Task<Result<ApplicationCommandResult>> ExecuteAsync(
        UpdateApplicationMetadataCommand command,
        CancellationToken cancellationToken = default)
    {
        DomainApplication? application = await this.repository.GetByIdAsync(command.ApplicationId, cancellationToken);
        if (application is null)
        {
            return ApplicationErrors.NotFound;
        }

        Result updateResult = application.UpdateMetadata(command.DisplayName, command.Description, this.dateTimeProvider);
        if (updateResult.IsFailure)
        {
            return updateResult.Error;
        }

        Result projectionResult = await this.projection.UpsertAsync(application, cancellationToken);
        if (projectionResult.IsFailure)
        {
            return projectionResult.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken);
        await this.AuditAsync("Application.Updated", application, cancellationToken);

        return ToCommandResult(application);
    }

    public async Task<Result<ApplicationCommandResult>> ExecuteAsync(
        ConfigureApplicationOAuthCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!ApplicationProfilePolicyCatalog.GetPolicy(command.Profile).IsSelectable)
        {
            return ApplicationErrors.ProfileNotAvailable;
        }

        DomainApplication? application = await this.repository.GetByIdAsync(command.ApplicationId, cancellationToken);
        if (application is null)
        {
            return ApplicationErrors.NotFound;
        }

        Result configureResult = application.ConfigureOAuth(
            command.Profile,
            command.ClientType,
            command.AllowedGrantTypes,
            command.AllowedScopes,
            command.RedirectUris,
            command.PostLogoutRedirectUris,
            command.RequirePkce,
            command.RequireConsent,
            this.dateTimeProvider);
        if (configureResult.IsFailure)
        {
            return configureResult.Error;
        }

        Result projectionResult = await this.projection.UpsertAsync(application, cancellationToken);
        if (projectionResult.IsFailure)
        {
            return projectionResult.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken);
        await this.AuditAsync("Application.OAuthConfigured", application, cancellationToken);

        return ToCommandResult(application);
    }

    public async Task<Result> ExecuteAsync(DisableApplicationCommand command, CancellationToken cancellationToken = default)
    {
        DomainApplication? application = await this.repository.GetByIdAsync(command.ApplicationId, cancellationToken);
        if (application is null)
        {
            return ApplicationErrors.NotFound;
        }

        Result result = application.Disable(this.dateTimeProvider);
        if (result.IsFailure)
        {
            return result.Error;
        }

        Result projectionResult = await this.projection.UpsertAsync(application, cancellationToken);
        if (projectionResult.IsFailure)
        {
            return projectionResult.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken);
        await this.AuditAsync("Application.Disabled", application, cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ExecuteAsync(EnableApplicationCommand command, CancellationToken cancellationToken = default)
    {
        DomainApplication? application = await this.repository.GetByIdAsync(command.ApplicationId, cancellationToken);
        if (application is null)
        {
            return ApplicationErrors.NotFound;
        }

        Result result = application.Enable(this.dateTimeProvider);
        if (result.IsFailure)
        {
            return result.Error;
        }

        Result projectionResult = await this.projection.UpsertAsync(application, cancellationToken);
        if (projectionResult.IsFailure)
        {
            return projectionResult.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken);
        await this.AuditAsync("Application.Enabled", application, cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ExecuteAsync(DeleteApplicationCommand command, CancellationToken cancellationToken = default)
    {
        DomainApplication? application = await this.repository.GetByIdAsync(command.ApplicationId, cancellationToken);
        if (application is null)
        {
            return ApplicationErrors.NotFound;
        }

        Result result = application.Delete(this.dateTimeProvider);
        if (result.IsFailure)
        {
            return result.Error;
        }

        Result projectionResult = await this.projection.DeleteAsync(application.Id, cancellationToken);
        if (projectionResult.IsFailure)
        {
            return projectionResult.Error;
        }

        this.repository.Remove(application);
        await this.repository.SaveChangesAsync(cancellationToken);
        await this.AuditAsync("Application.Deleted", application, cancellationToken);

        return Result.Success();
    }

    private static ApplicationCommandResult ToCommandResult(DomainApplication application) =>
        new(
            application.Id,
            application.ClientId,
            application.DisplayName,
            application.Profile,
            application.ClientType,
            application.Status);

    private Task AuditAsync(string action, DomainApplication application, CancellationToken cancellationToken) =>
        this.auditLog.LogAsync(
            "system",
            action,
            "Application",
            application.Id.Value.ToString(),
            $"ClientId: {application.ClientId}",
            cancellationToken);
}

public sealed class ApplicationCredentialUseCases :
    IAddApplicationSecretUseCase,
    IAddApplicationCertificateUseCase,
    IRevokeApplicationCredentialUseCase
{
    private readonly IApplicationRepository repository;
    private readonly IApplicationProtocolProjection projection;
    private readonly IPasswordHasher passwordHasher;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;

    public ApplicationCredentialUseCases(
        IApplicationRepository repository,
        IApplicationProtocolProjection projection,
        IPasswordHasher passwordHasher,
        IDateTimeProvider dateTimeProvider,
        IAuditLog auditLog)
    {
        this.repository = repository;
        this.projection = projection;
        this.passwordHasher = passwordHasher;
        this.dateTimeProvider = dateTimeProvider;
        this.auditLog = auditLog;
    }

    public async Task<Result<ApplicationCredentialCommandResult>> ExecuteAsync(
        AddApplicationSecretCommand command,
        CancellationToken cancellationToken = default)
    {
        DomainApplication? application = await this.repository.GetByIdAsync(command.ApplicationId, cancellationToken);
        if (application is null)
        {
            return ApplicationErrors.NotFound;
        }

        if (command.RevokeExisting)
        {
            foreach (ApplicationCredential credential in application.Credentials.Where(credential =>
                         credential.Type == ApplicationCredentialType.ClientSecret &&
                         credential.IsActiveAt(this.dateTimeProvider.UtcNow)))
            {
                Result revokeResult = application.RevokeCredential(credential.Id, this.dateTimeProvider);
                if (revokeResult.IsFailure)
                {
                    return revokeResult.Error;
                }
            }
        }

        string oneTimeSecret = GenerateSecret();
        string secretHash = this.passwordHasher.HashPassword(oneTimeSecret);
        Result<ApplicationCredential> addResult = application.AddSecret(
            secretHash,
            command.Description,
            command.ExpiresAt,
            this.dateTimeProvider);
        if (addResult.IsFailure)
        {
            return addResult.Error;
        }

        Result projectionResult = await this.projection.UpsertAsync(application, oneTimeSecret, cancellationToken);
        if (projectionResult.IsFailure)
        {
            return projectionResult.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken);
        await this.auditLog.LogAsync(
            "system",
            "ApplicationCredential.SecretAdded",
            "Application",
            application.Id.Value.ToString(),
            $"CredentialId: {addResult.Value.Id}, RevokeExisting: {command.RevokeExisting}",
            cancellationToken);

        return new ApplicationCredentialCommandResult(
            addResult.Value.Id,
            application.Id,
            addResult.Value.Type,
            oneTimeSecret);
    }

    public async Task<Result<ApplicationCredentialCommandResult>> ExecuteAsync(
        AddApplicationCertificateCommand command,
        CancellationToken cancellationToken = default)
    {
        DomainApplication? application = await this.repository.GetByIdAsync(command.ApplicationId, cancellationToken);
        if (application is null)
        {
            return ApplicationErrors.NotFound;
        }

        Result<ApplicationCredential> addResult = application.AddCertificate(
            command.Thumbprint,
            command.Subject,
            command.Description,
            command.ExpiresAt,
            this.dateTimeProvider);
        if (addResult.IsFailure)
        {
            return addResult.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken);
        await this.auditLog.LogAsync(
            "system",
            "ApplicationCredential.CertificateAdded",
            "Application",
            application.Id.Value.ToString(),
            $"CredentialId: {addResult.Value.Id}, Thumbprint: {addResult.Value.Thumbprint}",
            cancellationToken);

        return new ApplicationCredentialCommandResult(
            addResult.Value.Id,
            application.Id,
            addResult.Value.Type,
            OneTimeSecret: null);
    }

    public async Task<Result> ExecuteAsync(
        RevokeApplicationCredentialCommand command,
        CancellationToken cancellationToken = default)
    {
        DomainApplication? application = await this.repository.GetByIdAsync(command.ApplicationId, cancellationToken);
        if (application is null)
        {
            return ApplicationErrors.NotFound;
        }

        Result revokeResult = application.RevokeCredential(command.CredentialId, this.dateTimeProvider);
        if (revokeResult.IsFailure)
        {
            return revokeResult;
        }

        // Revoking a credential must not rotate the live client secret; sync state without changing it.
        Result projectionResult = await this.projection.UpsertAsync(application, cancellationToken);
        if (projectionResult.IsFailure)
        {
            return projectionResult.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken);
        await this.auditLog.LogAsync(
            "system",
            "ApplicationCredential.Revoked",
            "Application",
            application.Id.Value.ToString(),
            $"CredentialId: {command.CredentialId}",
            cancellationToken);

        return Result.Success();
    }

    private static string GenerateSecret()
    {
        byte[] bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}

public sealed class ApplicationCredentialValidationUseCases :
    IValidateApplicationClientCredentialsUseCase,
    IValidateApplicationCertificateUseCase
{
    private readonly IApplicationRepository repository;
    private readonly IPasswordHasher passwordHasher;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;

    public ApplicationCredentialValidationUseCases(
        IApplicationRepository repository,
        IPasswordHasher passwordHasher,
        IDateTimeProvider dateTimeProvider,
        IAuditLog auditLog)
    {
        this.repository = repository;
        this.passwordHasher = passwordHasher;
        this.dateTimeProvider = dateTimeProvider;
        this.auditLog = auditLog;
    }

    public async Task<Result<ValidateApplicationCredentialsResult>> ExecuteAsync(
        ValidateApplicationClientCredentialsCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.ClientId) || string.IsNullOrWhiteSpace(command.ClientSecret))
        {
            return ApplicationErrors.InvalidCredentials;
        }

        DomainApplication? application = await this.repository.GetByClientIdAsync(command.ClientId, cancellationToken);
        if (application is null)
        {
            return ApplicationErrors.InvalidCredentials;
        }

        Result activeResult = EnsureApplicationActive(application);
        if (activeResult.IsFailure)
        {
            return activeResult.Error;
        }

        ApplicationCredential? credential = application.Credentials.FirstOrDefault(credential =>
            credential.Type == ApplicationCredentialType.ClientSecret &&
            credential.SecretHash is not null &&
            credential.IsActiveAt(this.dateTimeProvider.UtcNow) &&
            this.passwordHasher.VerifyPassword(credential.SecretHash, command.ClientSecret));
        if (credential is null)
        {
            return ApplicationErrors.InvalidCredentials;
        }

        Result markUsedResult = credential.MarkUsed(this.dateTimeProvider);
        if (markUsedResult.IsFailure)
        {
            return markUsedResult.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken);
        await this.AuditUsedAsync(application, credential, cancellationToken);
        return ToValidationResult(application);
    }

    public async Task<Result<ValidateApplicationCredentialsResult>> ExecuteAsync(
        ValidateApplicationCertificateCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.ClientId) || string.IsNullOrWhiteSpace(command.CertificateThumbprint))
        {
            return ApplicationErrors.InvalidCredentials;
        }

        DomainApplication? application = await this.repository.GetByClientIdAsync(command.ClientId, cancellationToken);
        if (application is null)
        {
            return ApplicationErrors.InvalidCredentials;
        }

        Result activeResult = EnsureApplicationActive(application);
        if (activeResult.IsFailure)
        {
            return activeResult.Error;
        }

        ApplicationCredential? credential = application.Credentials.FirstOrDefault(credential =>
            credential.Type == ApplicationCredentialType.X509Certificate &&
            credential.Thumbprint is not null &&
            credential.IsActiveAt(this.dateTimeProvider.UtcNow) &&
            credential.Thumbprint.Equals(command.CertificateThumbprint, StringComparison.OrdinalIgnoreCase));
        if (credential is null)
        {
            return ApplicationErrors.InvalidCredentials;
        }

        Result markUsedResult = credential.MarkUsed(this.dateTimeProvider);
        if (markUsedResult.IsFailure)
        {
            return markUsedResult.Error;
        }

        await this.repository.SaveChangesAsync(cancellationToken);
        await this.AuditUsedAsync(application, credential, cancellationToken);
        return ToValidationResult(application);
    }

    private static Result EnsureApplicationActive(DomainApplication application) =>
        application.Status == ApplicationStatus.Active
            ? Result.Success()
            : ApplicationErrors.Disabled;

    private static ValidateApplicationCredentialsResult ToValidationResult(DomainApplication application) =>
        new(
            application.Id,
            application.ClientId,
            application.DisplayName,
            application.AllowedScopes,
            application.AllowedGrantTypes);

    private Task AuditUsedAsync(
        DomainApplication application,
        ApplicationCredential credential,
        CancellationToken cancellationToken) =>
        this.auditLog.LogAsync(
            "system",
            "ApplicationCredential.Used",
            "Application",
            application.Id.Value.ToString(),
            $"CredentialId: {credential.Id}, Type: {credential.Type}",
            cancellationToken);
}
