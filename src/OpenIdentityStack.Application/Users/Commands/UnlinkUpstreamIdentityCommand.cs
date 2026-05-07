using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Command for unlinking an upstream identity from a user.
/// </summary>
/// <param name="UserId">The user ID to unlink the identity from.</param>
/// <param name="ProviderId">The upstream provider ID to unlink.</param>
public sealed record UnlinkUpstreamIdentityCommand(
    UserId UserId,
    UpstreamProviderId ProviderId);

/// <summary>
/// Result of unlinking an upstream identity.
/// </summary>
/// <param name="UserId">The user ID.</param>
/// <param name="ProviderId">The unlinked provider ID.</param>
public sealed record UnlinkUpstreamIdentityResult(
    UserId UserId,
    UpstreamProviderId ProviderId);

/// <summary>
/// Interface for unlinking upstream identity use case.
/// </summary>
public interface IUnlinkUpstreamIdentityUseCase
{
    /// <summary>
    /// Unlinks an upstream identity from a user.
    /// </summary>
    Task<Result<UnlinkUpstreamIdentityResult>> ExecuteAsync(
        UnlinkUpstreamIdentityCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of unlinking upstream identity use case.
/// </summary>
public sealed class UnlinkUpstreamIdentityUseCase : IUnlinkUpstreamIdentityUseCase
{
    private readonly IUserRepository userRepository;

    public UnlinkUpstreamIdentityUseCase(IUserRepository userRepository)
    {
        this.userRepository = userRepository;
    }

    /// <inheritdoc />
    public async Task<Result<UnlinkUpstreamIdentityResult>> ExecuteAsync(
        UnlinkUpstreamIdentityCommand command,
        CancellationToken cancellationToken = default)
    {
        User? user = await this.userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        // Use the provider's Name if we had it, but domain logic requires ID usually.
        // Actually Unlink only needs ID? Checking Entity... "UnlinkUpstreamIdentity(UpstreamProviderId providerId)"
        // Yes.

        Result result = user.UnlinkUpstreamIdentity(command.ProviderId);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await this.userRepository.SaveChangesAsync(cancellationToken);

        return new UnlinkUpstreamIdentityResult(command.UserId, command.ProviderId);
    }
}
