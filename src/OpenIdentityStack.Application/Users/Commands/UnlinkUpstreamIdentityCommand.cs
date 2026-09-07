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
/// <param name="ActorId">The ID of the admin performing the action (for auditing).</param>
public sealed record UnlinkUpstreamIdentityCommand(
    UserId UserId,
    UpstreamProviderId ProviderId,
    string ActorId);

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
    private readonly IAuditLog auditLog;

    public UnlinkUpstreamIdentityUseCase(IUserRepository userRepository, IAuditLog auditLog)
    {
        this.userRepository = userRepository;
        this.auditLog = auditLog;
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
            if (result.Error == UpstreamIdentityErrors.QuarantineRetentionRequired)
            {
                await this.auditLog.LogAsync(command.ActorId, "User.QuarantinedIdentityUnlinkDenied", "User", user.Id.Value.ToString(), "Retained association evidence cannot be erased by ordinary unlink.", cancellationToken);
            }
            return result.Error;
        }

        await this.userRepository.SaveChangesAsync(cancellationToken);

        await this.auditLog.LogAsync(
            command.ActorId,
            "User.UpstreamIdentityUnlinked",
            "User",
            user.Id.Value.ToString(),
            $"ProviderId: {command.ProviderId.Value}",
            cancellationToken);

        return new UnlinkUpstreamIdentityResult(command.UserId, command.ProviderId);
    }
}
