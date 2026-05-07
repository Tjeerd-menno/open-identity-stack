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

    public DeleteUserUseCase(IUserRepository userRepository)
    {
        this.userRepository = userRepository;
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

        return Result.Success();
    }
}
