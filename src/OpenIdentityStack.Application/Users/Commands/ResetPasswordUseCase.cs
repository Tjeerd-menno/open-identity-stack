using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Users;

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

    public ResetPasswordUseCase(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IPasswordPolicyValidator passwordPolicyValidator,
        IDateTimeProvider dateTimeProvider)
    {
        this.userRepository = userRepository;
        this.passwordHasher = passwordHasher;
        this.passwordPolicyValidator = passwordPolicyValidator;
        this.dateTimeProvider = dateTimeProvider;
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

        User? user = await this.userRepository.GetByIdAsync(command.UserId, cancellationToken);
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

        await this.userRepository.SaveChangesAsync(cancellationToken);

        return new ResetPasswordResult(user.Id, this.dateTimeProvider.UtcNow);
    }
}
