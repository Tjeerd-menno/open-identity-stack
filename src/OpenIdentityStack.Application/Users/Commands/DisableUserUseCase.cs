using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Users;

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

    public DisableUserUseCase(
        IUserRepository userRepository,
        IDateTimeProvider dateTimeProvider,
        IAuditLog auditLog)
    {
        this.userRepository = userRepository;
        this.dateTimeProvider = dateTimeProvider;
        this.auditLog = auditLog;
    }

    /// <inheritdoc />
    public async Task<Result<DisableUserResult>> ExecuteAsync(
        DisableUserCommand command,
        CancellationToken cancellationToken = default)
    {
        User? user = await this.userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        Result result = user.Disable(command.Reason, this.dateTimeProvider);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await this.userRepository.SaveChangesAsync(cancellationToken);

        await this.auditLog.LogAsync(
            command.ActorId,
            "User.Disabled",
            "User",
            user.Id.Value.ToString(),
            $"Reason: {command.Reason}",
            cancellationToken);

        return new DisableUserResult(user.Id, this.dateTimeProvider.UtcNow);
    }
}
