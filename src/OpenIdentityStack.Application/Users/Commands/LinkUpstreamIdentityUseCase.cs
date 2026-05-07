using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Implementation of linking upstream identity use case.
/// </summary>
public sealed class LinkUpstreamIdentityUseCase : ILinkUpstreamIdentityUseCase
{
    private readonly IUserRepository userRepository;
    private readonly IUpstreamProviderRepository providerRepository;
    private readonly IDateTimeProvider dateTimeProvider;

    public LinkUpstreamIdentityUseCase(
        IUserRepository userRepository,
        IUpstreamProviderRepository providerRepository,
        IDateTimeProvider dateTimeProvider)
    {
        this.userRepository = userRepository;
        this.providerRepository = providerRepository;
        this.dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<Result<LinkUpstreamIdentityResult>> ExecuteAsync(
        LinkUpstreamIdentityCommand command,
        CancellationToken cancellationToken = default)
    {
        User? user = await this.userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        // Verify provider exists
        UpstreamProvider? provider = await this.providerRepository.GetByIdAsync(command.ProviderId, cancellationToken);
        if (provider is null)
        {
            return Domain.Federation.UpstreamProviderErrors.NotFound;
        }

        // Check if this upstream identity is already linked to another user
        User? existingUser = await this.userRepository.FindByUpstreamIdentityAsync(
            command.ProviderId,
            command.SubjectId,
            cancellationToken);

        if (existingUser is not null && existingUser.Id != command.UserId)
        {
            return UpstreamIdentityErrors.AlreadyLinked;
        }

        // Use the provider's name from the database, not the command
        Result result = user.LinkUpstreamIdentity(
            command.ProviderId,
            provider.Name,
            command.SubjectId,
            command.Email);

        if (result.IsFailure)
        {
            return result.Error;
        }

        await this.userRepository.SaveChangesAsync(cancellationToken);

        return new LinkUpstreamIdentityResult(
            user.Id,
            command.ProviderId,
            this.dateTimeProvider.UtcNow);
    }
}
