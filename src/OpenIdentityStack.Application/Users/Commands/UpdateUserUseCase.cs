using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Implementation of the update user use case.
/// </summary>
public sealed class UpdateUserUseCase : IUpdateUserUseCase
{
    private readonly IUserRepository userRepository;
    private readonly IDateTimeProvider dateTimeProvider;

    public UpdateUserUseCase(
        IUserRepository userRepository,
        IDateTimeProvider dateTimeProvider)
    {
        this.userRepository = userRepository;
        this.dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<Result<UpdateUserResult>> ExecuteAsync(
        UpdateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        User? user = await this.userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        if (!string.IsNullOrWhiteSpace(command.DisplayName))
        {
            Result result = user.UpdateDisplayName(command.DisplayName, this.dateTimeProvider);
            if (result.IsFailure)
            {
                return result.Error;
            }
        }

        await this.userRepository.SaveChangesAsync(cancellationToken);

        return new UpdateUserResult(user.Id, this.dateTimeProvider.UtcNow);
    }
}
