using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Queries;

/// <summary>
/// Query for finding a user by their upstream identity.
/// </summary>
/// <param name="ProviderId">The upstream provider ID.</param>
/// <param name="SubjectId">The subject ID from the upstream provider.</param>
public sealed record FindUserByUpstreamIdentityQuery(
    UpstreamProviderId ProviderId,
    string SubjectId);

/// <summary>
/// Result of finding a user by upstream identity.
/// </summary>
/// <param name="UserId">The user's ID.</param>
/// <param name="Email">The user's email.</param>
/// <param name="DisplayName">The user's display name.</param>
/// <param name="Status">The user's status.</param>
/// <param name="LinkedAt">When the upstream identity was linked.</param>
public sealed record FindUserByUpstreamIdentityResult(
    UserId UserId,
    string? Email,
    string DisplayName,
    UserStatus Status,
    DateTimeOffset LinkedAt);

/// <summary>
/// Interface for finding user by upstream identity query handler.
/// </summary>
public interface IFindUserByUpstreamIdentityQueryHandler
{
    /// <summary>
    /// Finds a user by their upstream identity.
    /// </summary>
    Task<Result<FindUserByUpstreamIdentityResult>> HandleAsync(
        FindUserByUpstreamIdentityQuery query,
        CancellationToken cancellationToken = default);
}
