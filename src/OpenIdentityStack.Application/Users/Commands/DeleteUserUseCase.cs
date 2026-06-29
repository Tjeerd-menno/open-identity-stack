using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Implementation of the delete user use case.
/// </summary>
public sealed class DeleteUserUseCase : IDeleteUserUseCase
{
    private readonly IUserRepository userRepository;
    private readonly IAuditLog auditLog;

    public DeleteUserUseCase(IUserRepository userRepository, IAuditLog auditLog)
    {
        this.userRepository = userRepository;
        this.auditLog = auditLog;
    }

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(
        DeleteUserCommand command,
        CancellationToken cancellationToken = default)
    {
        User? user = await this.userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        await this.userRepository.DeleteAsync(user, cancellationToken);
        await this.userRepository.SaveChangesAsync(cancellationToken);

        await this.auditLog.LogAsync(
            command.ActorId,
            "User.Deleted",
            "User",
            user.Id.Value.ToString(),
            $"Email: {user.Email}",
            cancellationToken);

        return Result.Success();
    }
}
