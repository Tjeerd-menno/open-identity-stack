using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Queries;

/// <summary>
/// Handler for finding user by upstream identity query.
/// </summary>
public sealed class FindUserByUpstreamIdentityQueryHandler : IFindUserByUpstreamIdentityQueryHandler
{
    private readonly IUserRepository userRepository;

    public FindUserByUpstreamIdentityQueryHandler(IUserRepository userRepository)
    {
        this.userRepository = userRepository;
    }

    /// <inheritdoc />
    public async Task<Result<FindUserByUpstreamIdentityResult>> HandleAsync(
        FindUserByUpstreamIdentityQuery query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.SubjectId))
        {
            return UpstreamIdentityErrors.SubjectIdRequired;
        }

        User? user = await this.userRepository.FindByUpstreamIdentityAsync(
            query.ProviderId,
            query.SubjectId,
            cancellationToken);

        if (user is null)
        {
            return UserErrors.NotFound;
        }

        UpstreamIdentity? upstreamIdentity = user.GetUpstreamIdentity(query.ProviderId);
        if (upstreamIdentity is null)
        {
            return UpstreamIdentityErrors.NotLinked;
        }

        return new FindUserByUpstreamIdentityResult(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Status,
            upstreamIdentity.LinkedAt);
    }
}
