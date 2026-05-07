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

    public EnableUserUseCase(
        IUserRepository userRepository,
        IDateTimeProvider dateTimeProvider)
    {
        this.userRepository = userRepository;
        this.dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<Result<EnableUserResult>> ExecuteAsync(
        EnableUserCommand command,
        CancellationToken cancellationToken = default)
    {
        User? user = await this.userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        Result result = user.Enable(this.dateTimeProvider);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await this.userRepository.SaveChangesAsync(cancellationToken);

        return new EnableUserResult(user.Id, this.dateTimeProvider.UtcNow);
    }
}
