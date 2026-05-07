using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Federation.Commands;

/// <summary>
/// Implementation of JIT provisioning use case.
/// </summary>
public sealed class JitProvisionUserUseCase : IJitProvisionUserUseCase
{
    private readonly IUserRepository userRepository;
    private readonly IUpstreamProviderRepository providerRepository;
    private readonly IDateTimeProvider dateTimeProvider;

    public JitProvisionUserUseCase(
        IUserRepository userRepository,
        IUpstreamProviderRepository providerRepository,
        IDateTimeProvider dateTimeProvider)
    {
        this.userRepository = userRepository;
        this.providerRepository = providerRepository;
        this.dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<Result<JitProvisionUserResult>> ExecuteAsync(
        JitProvisionUserCommand command,
        CancellationToken cancellationToken = default)
    {
        // Validate subject ID
        if (string.IsNullOrWhiteSpace(command.SubjectId))
        {
            return UpstreamIdentityErrors.SubjectIdRequired;
        }

        // Find the upstream provider
        UpstreamProvider? provider = await this.providerRepository.GetByIdAsync(command.ProviderId, cancellationToken);
        if (provider is null)
        {
            return UpstreamProviderErrors.NotFound;
        }

        if (!provider.IsActive)
        {
            return UpstreamProviderErrors.ProviderDisabled;
        }

        // Check if user already exists with this upstream identity
        User? existingUser = await this.userRepository.FindByUpstreamIdentityAsync(
            command.ProviderId,
            command.SubjectId,
            cancellationToken);

        if (existingUser is not null)
        {
            // User already linked - just return
            return new JitProvisionUserResult(
                existingUser.Id,
                IsNewUser: false,
                existingUser.Email,
                existingUser.DisplayName);
        }

        // Check if a user exists with the same email
        if (!string.IsNullOrWhiteSpace(command.Email))
        {
            User? userByEmail = await this.userRepository.GetByEmailAsync(command.Email, cancellationToken);
            if (userByEmail is not null)
            {
                // Link the upstream identity to existing user
                Result linkResult = userByEmail.LinkUpstreamIdentity(
                    command.ProviderId,
                    provider.Name,
                    command.SubjectId,
                    command.Email);

                if (linkResult.IsFailure)
                {
                    return linkResult.Error;
                }

                await this.userRepository.SaveChangesAsync(cancellationToken);

                return new JitProvisionUserResult(
                    userByEmail.Id,
                    IsNewUser: false,
                    userByEmail.Email,
                    userByEmail.DisplayName);
            }
        }

        // Create a new federated user
        string displayName = !string.IsNullOrWhiteSpace(command.DisplayName)
            ? command.DisplayName
            : command.Email?.Split('@')[0] ?? "User";

        Result<User> userResult = User.CreateFederated(
            command.Email ?? string.Empty,
            displayName,
            command.ProviderId,
            provider.Name,
            command.SubjectId);

        if (userResult.IsFailure)
        {
            return userResult.Error;
        }

        await this.userRepository.AddAsync(userResult.Value, cancellationToken);
        await this.userRepository.SaveChangesAsync(cancellationToken);

        return new JitProvisionUserResult(
            userResult.Value.Id,
            IsNewUser: true,
            userResult.Value.Email,
            userResult.Value.DisplayName);
    }
}
