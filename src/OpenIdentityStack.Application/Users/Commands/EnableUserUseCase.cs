using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Implementation of the enable user use case.
/// </summary>
public sealed class EnableUserUseCase : IEnableUserUseCase
{
    private readonly IUserRepository userRepository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;
    private readonly IAdministrativeApproval approval;

    public EnableUserUseCase(
        IUserRepository userRepository,
        IDateTimeProvider dateTimeProvider,
        IAuditLog auditLog,
        IAdministrativeApproval approval)
    {
        this.userRepository = userRepository;
        this.dateTimeProvider = dateTimeProvider;
        this.auditLog = auditLog;
        this.approval = approval;
    }

    /// <inheritdoc />
    public async Task<Result<EnableUserResult>> ExecuteAsync(
        EnableUserCommand command,
        CancellationToken cancellationToken = default)
    {
        await this.approval.CaptureAuthorityAsync(cancellationToken);
        User? user = await this.userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        Result approvalResult = await this.approval.RequireForUserAccessAsync(user.Id, "User.EnableUserUnrestricted", cancellationToken);
        if (approvalResult.IsFailure) { return approvalResult.Error; }

        Result result = user.Enable(this.dateTimeProvider);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await this.userRepository.SaveChangesAsync(cancellationToken);
        await this.approval.RecordOutcomeAsync(true, cancellationToken);

        await this.auditLog.LogAsync(
            command.ActorId,
            "User.Enabled",
            "User",
            user.Id.Value.ToString(),
            $"Email: {user.Email}",
            cancellationToken);

        return new EnableUserResult(user.Id, this.dateTimeProvider.UtcNow);
    }
}
