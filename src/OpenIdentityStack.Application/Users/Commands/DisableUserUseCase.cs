using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Implementation of the disable user use case.
/// </summary>
public sealed class DisableUserUseCase : IDisableUserUseCase
{
    private readonly IUserRepository userRepository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;
    private readonly ICredentialLifecycleTransactionRunner transactionRunner;
    private readonly ISessionRepository sessionRepository;

    public DisableUserUseCase(
        IUserRepository userRepository,
        IDateTimeProvider dateTimeProvider,
        IAuditLog auditLog,
        ICredentialLifecycleTransactionRunner transactionRunner,
        ISessionRepository sessionRepository)
    {
        this.userRepository = userRepository;
        this.dateTimeProvider = dateTimeProvider;
        this.auditLog = auditLog;
        this.transactionRunner = transactionRunner;
        this.sessionRepository = sessionRepository;
    }

    /// <inheritdoc />
    public async Task<Result<DisableUserResult>> ExecuteAsync(
        DisableUserCommand command,
        CancellationToken cancellationToken = default)
    {
        return await this.transactionRunner.ExecuteAsync<DisableUserResult>(async transactionCancellationToken =>
        {
            User? user = await this.userRepository.GetByIdAsync(command.UserId, transactionCancellationToken);
            if (user is null)
            {
                return UserErrors.NotFound;
            }

            Result result = user.Disable(command.Reason, this.dateTimeProvider);
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

            await this.auditLog.LogAsync(
                command.ActorId,
                "User.Disabled",
                "User",
                user.Id.Value.ToString(),
                $"Reason: {command.Reason}",
                transactionCancellationToken);

            return new DisableUserResult(user.Id, this.dateTimeProvider.UtcNow);
        }, cancellationToken);
    }

}
