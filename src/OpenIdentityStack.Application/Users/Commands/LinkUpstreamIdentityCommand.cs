using OpenIdentityStack.Domain.Federation;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Command for linking an upstream identity to an existing user.
/// </summary>
/// <param name="UserId">The user ID to link the identity to.</param>
/// <param name="ProviderId">The upstream provider ID.</param>
/// <param name="ProviderName">The upstream provider name.</param>
/// <param name="SubjectId">The subject ID from the upstream provider.</param>
/// <param name="Email">The email from the upstream claims (optional).</param>
public sealed record LinkUpstreamIdentityCommand(
    UserId UserId,
    UpstreamProviderId ProviderId,
    string ProviderName,
    string SubjectId,
    string? Email);

/// <summary>
/// Result of linking an upstream identity.
/// </summary>
/// <param name="UserId">The user ID.</param>
/// <param name="ProviderId">The linked provider ID.</param>
/// <param name="LinkedAt">When the identity was linked.</param>
public sealed record LinkUpstreamIdentityResult(
    UserId UserId,
    UpstreamProviderId ProviderId,
    DateTimeOffset LinkedAt);

/// <summary>
/// Interface for linking upstream identity use case.
/// </summary>
public interface ILinkUpstreamIdentityUseCase
{
    /// <summary>
    /// Links an upstream identity to an existing user.
    /// </summary>
    Task<Result<LinkUpstreamIdentityResult>> ExecuteAsync(
        LinkUpstreamIdentityCommand command,
        CancellationToken cancellationToken = default);
}
