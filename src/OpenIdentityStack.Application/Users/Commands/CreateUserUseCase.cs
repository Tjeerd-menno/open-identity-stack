using Microsoft.Extensions.Logging;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Common.Logging;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Implementation of the create user use case.
/// </summary>
public sealed class CreateUserUseCase : ICreateUserUseCase
{
    private readonly IUserRepository userRepository;
    private readonly IPasswordHasher passwordHasher;
    private readonly IPasswordPolicyValidator passwordPolicyValidator;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly ILogger<CreateUserUseCase> logger;

    public CreateUserUseCase(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IPasswordPolicyValidator passwordPolicyValidator,
        IDateTimeProvider dateTimeProvider,
        ILogger<CreateUserUseCase> logger)
    {
        this.userRepository = userRepository;
        this.passwordHasher = passwordHasher;
        this.passwordPolicyValidator = passwordPolicyValidator;
        this.dateTimeProvider = dateTimeProvider;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<CreateUserResult>> ExecuteAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        // Validate password policy
        Result passwordValidation = this.passwordPolicyValidator.ValidatePassword(command.Password);
        if (passwordValidation.IsFailure)
        {
            this.logger.LogUserOperationFailed("CreateUser", passwordValidation.Error.Code);
            return passwordValidation.Error;
        }

        // Check if email is already in use
        bool emailExists = await this.userRepository.ExistsByEmailAsync(command.Email, cancellationToken);
        if (emailExists)
        {
            this.logger.LogUserOperationFailed("CreateUser", UserErrors.EmailAlreadyExists.Code);
            return UserErrors.EmailAlreadyExists;
        }

        // Check if preferred username is already in use
        if (!string.IsNullOrWhiteSpace(command.Profile?.PreferredUsername))
        {
            User? existingUser = await this.userRepository.GetByPreferredUsernameAsync(
                command.Profile.PreferredUsername,
                cancellationToken);
            if (existingUser is not null)
            {
                this.logger.LogUserOperationFailed("CreateUser", UserErrors.PreferredUsernameAlreadyExists.Code);
                return UserErrors.PreferredUsernameAlreadyExists;
            }
        }

        // Hash the password
        string passwordHash = this.passwordHasher.HashPassword(command.Password);

        // Create the user
        Result<User> userResult = User.CreateLocal(
            command.Email,
            command.DisplayName,
            passwordHash,
            this.dateTimeProvider,
            command.Profile);

        if (userResult.IsFailure)
        {
            this.logger.LogUserOperationFailed("CreateUser", userResult.Error.Code);
            return userResult.Error;
        }

        User user = userResult.Value;

        // Persist the user
        await this.userRepository.AddAsync(user, cancellationToken);
        await this.userRepository.SaveChangesAsync(cancellationToken);

        this.logger.LogUserCreated(user.Id.Value, user.Email);

        return new CreateUserResult(
            user.Id,
            user.Email,
            user.DisplayName);
    }
}
