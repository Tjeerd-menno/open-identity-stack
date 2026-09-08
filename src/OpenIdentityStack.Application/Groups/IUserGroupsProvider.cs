using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Application.Groups;

/// <summary>
/// Resolves the direct group memberships of a user for read-only projections (effective roles,
/// group claims). Implementations may memoize within a DI scope so that the several handlers that
/// run during a single token issuance share one database round trip.
/// </summary>
public interface IUserGroupsProvider
{
    Task<IReadOnlyList<Group>> GetGroupsForUserAsync(UserId userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Scoped <see cref="IUserGroupsProvider"/> that loads a user's groups at most once per scope.
/// Intended for read paths only; mutation flows should keep using <see cref="IGroupRepository"/>.
/// </summary>
public sealed class RequestScopedUserGroupsProvider : IUserGroupsProvider
{
    private readonly IGroupRepository groupRepository;
    private readonly Dictionary<UserId, Task<IReadOnlyList<Group>>> cache = [];

    public RequestScopedUserGroupsProvider(IGroupRepository groupRepository)
    {
        this.groupRepository = groupRepository;
    }

    public Task<IReadOnlyList<Group>> GetGroupsForUserAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        lock (this.cache)
        {
            if (this.cache.TryGetValue(userId, out Task<IReadOnlyList<Group>>? pending)
                && !pending.IsFaulted
                && !pending.IsCanceled)
            {
                return pending;
            }

            Task<IReadOnlyList<Group>> loading = this.groupRepository.GetGroupsForUserAsync(userId, cancellationToken);
            this.cache[userId] = loading;
            return loading;
        }
    }
}
