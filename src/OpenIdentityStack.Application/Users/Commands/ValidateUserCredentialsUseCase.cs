using Microsoft.Extensions.Logging;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Common.Logging;
using OpenIdentityStack.Domain.Settings;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Implementation of the validate user credentials use case.
/// </summary>
public sealed class ValidateUserCredentialsUseCase : IValidateUserCredentialsUseCase
{
    private readonly IUserRepository userRepository;
    private readonly IPasswordHasher passwordHasher;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuthenticationSettingsRepository authSettingsRepository;
    private readonly IPermissionChecker permissionChecker;
    private readonly IAuditLog auditLog;
    private readonly ILogger<ValidateUserCredentialsUseCase> logger;

    public ValidateUserCredentialsUseCase(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IDateTimeProvider dateTimeProvider,
        IAuthenticationSettingsRepository authSettingsRepository,
        IPermissionChecker permissionChecker,
        IAuditLog auditLog,
        ILogger<ValidateUserCredentialsUseCase> logger)
    {
        this.userRepository = userRepository;
        this.passwordHasher = passwordHasher;
        this.dateTimeProvider = dateTimeProvider;
        this.authSettingsRepository = authSettingsRepository;
        this.permissionChecker = permissionChecker;
        this.auditLog = auditLog;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ValidateUserCredentialsResult>> ExecuteAsync(
        ValidateUserCredentialsCommand command,
        CancellationToken cancellationToken = default)
    {
        // Find the user by email
        User? user = await this.userRepository.GetByEmailAsync(command.Email, cancellationToken);
        if (user is null)
        {
            this.logger.LogCredentialsValidationFailed(command.Email, "UserNotFound");
            return UserErrors.InvalidCredentials;
        }

        // Check if user has a password (not federated-only)
        if (!user.HasPassword())
        {
            this.logger.LogCredentialsValidationFailed(command.Email, "NoPassword");
            return UserErrors.InvalidCredentials;
        }

        // Check authentication settings to see if local auth is permitted
        AuthenticationSettings authSettings = await this.authSettingsRepository.GetOrCreateAsync(cancellationToken);
        
        if (!authSettings.IsLocalDefault)
        {
            // External provider is default - check if user is an IAM admin
            bool isIamAdmin = await this.IsIamAdminAsync(user.Id, cancellationToken);
            
            if (!authSettings.CanAuthenticateLocally(isIamAdmin))
            {
                // Log the denied attempt
                await this.auditLog.LogAsync(
                    user.Id.Value.ToString(),
                    "LocalAuthDenied",
                    "User",
                    user.Id.Value.ToString(),
                    $"Local authentication denied - external provider is default and user is not an IAM admin",
                    cancellationToken);

                this.logger.LogCredentialsValidationFailed(command.Email, "LocalAuthNotPermitted");
                
                // Return a generic error to avoid disclosing role membership (NFR-050)
                return AuthenticationSettingsErrors.LocalAuthNotPermitted;
            }
        }

        // Verify the password
        if (!this.passwordHasher.VerifyPassword(user.PasswordHash!, command.Password))
        {
            this.logger.LogCredentialsValidationFailed(command.Email, "InvalidPassword");
            return UserErrors.InvalidCredentials;
        }

        // Check user status
        if (user.Status == UserStatus.Disabled)
        {
            this.logger.LogCredentialsValidationFailed(command.Email, "AccountDisabled");
            return UserErrors.AccountDisabled;
        }

        if (user.Status == UserStatus.PendingVerification)
        {
            this.logger.LogCredentialsValidationFailed(command.Email, "AccountNotVerified");
            return UserErrors.AccountNotVerified;
        }

        // Record login and save
        user.RecordLogin(this.dateTimeProvider);
        await this.userRepository.UpdateAsync(user, cancellationToken);
        await this.userRepository.SaveChangesAsync(cancellationToken);

        // Audit local fallback usage if external provider is default (NFR-051)
        if (!authSettings.IsLocalDefault)
        {
            await this.auditLog.LogAsync(
                user.Id.Value.ToString(),
                "LocalFallbackAuthentication",
                "User",
                user.Id.Value.ToString(),
                $"IAM admin used local fallback authentication while external provider '{authSettings.DefaultProviderId}' is default",
                cancellationToken);
        }

        this.logger.LogCredentialsValidated(user.Id.Value);

        return new ValidateUserCredentialsResult(
            user.Id,
            user.Email,
            user.DisplayName);
    }

    /// <summary>
    /// Checks if a user is an IAM administrator.
    /// An IAM admin is a user with the super-admin role or system management permissions.
    /// </summary>
    private async Task<bool> IsIamAdminAsync(UserId userId, CancellationToken cancellationToken)
    {
        // Check for full wildcard permission (super-admin) or system settings permission
        return await this.permissionChecker.HasAnyPermissionAsync(
            userId,
            [Permissions.All, Permissions.System.All, Permissions.System.ManageSettings],
            cancellationToken);
    }
}
