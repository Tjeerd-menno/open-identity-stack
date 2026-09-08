using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Implementation of the reset password use case.
/// </summary>
public sealed class ResetPasswordUseCase : IResetPasswordUseCase
{
    private readonly IUserRepository userRepository;
    private readonly IPasswordHasher passwordHasher;
    private readonly IPasswordPolicyValidator passwordPolicyValidator;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;
    private readonly ICredentialLifecycleTransactionRunner transactionRunner;
    private readonly ISessionRepository sessionRepository;

    public ResetPasswordUseCase(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IPasswordPolicyValidator passwordPolicyValidator,
        IDateTimeProvider dateTimeProvider,
        IAuditLog auditLog,
        ICredentialLifecycleTransactionRunner transactionRunner,
        ISessionRepository sessionRepository)
    {
        this.userRepository = userRepository;
        this.passwordHasher = passwordHasher;
        this.passwordPolicyValidator = passwordPolicyValidator;
        this.dateTimeProvider = dateTimeProvider;
        this.auditLog = auditLog;
        this.transactionRunner = transactionRunner;
        this.sessionRepository = sessionRepository;
    }

    /// <inheritdoc />
    public async Task<Result<ResetPasswordResult>> ExecuteAsync(
        ResetPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        // Validate password policy
        Result passwordValidation = this.passwordPolicyValidator.ValidatePassword(command.NewPassword);
        if (passwordValidation.IsFailure)
        {
            return passwordValidation.Error;
        }

        return await this.transactionRunner.ExecuteAsync<ResetPasswordResult>(async transactionCancellationToken =>
        {
            User? user = await this.userRepository.GetByIdAsync(command.UserId, transactionCancellationToken);
            if (user is null)
            {
                return UserErrors.NotFound;
            }

            string hashedPassword = this.passwordHasher.HashPassword(command.NewPassword);
            Result result = user.SetPassword(hashedPassword, this.dateTimeProvider);
            if (result.IsFailure)
            {
                return result.Error;
            }

            IReadOnlyList<UserSession> sessions = await this.sessionRepository.GetActiveByUserIdAsync(user.Id, transactionCancellationToken);
            foreach (UserSession session in sessions)
            {
                Result revoke = session.Revoke(this.dateTimeProvider);
                if (revoke.IsFailure)
                {
                    return revoke.Error;
                }
                await this.sessionRepository.UpdateAsync(session, transactionCancellationToken);
            }

            await this.userRepository.SaveChangesAsync(transactionCancellationToken);

        // Never include the password (plain or hashed) in the audit detail.
        await this.auditLog.LogAsync(
            command.ActorId,
            "User.PasswordReset",
            "User",
            user.Id.Value.ToString(),
            $"Email: {user.Email}",
            transactionCancellationToken);

            return new ResetPasswordResult(user.Id, this.dateTimeProvider.UtcNow);
        }, cancellationToken);
    }

}
