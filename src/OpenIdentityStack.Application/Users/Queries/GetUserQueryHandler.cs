using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Queries;

/// <summary>
/// Implementation of the get user query handler.
/// </summary>
public sealed class GetUserQueryHandler : IGetUserQueryHandler
{
    private readonly IUserRepository userRepository;

    internal GetUserQueryHandler(IUserRepository userRepository)
    {
        this.userRepository = userRepository;
    }

    /// <inheritdoc />
    public async Task<Result<GetUserResult>> HandleAsync(
        GetUserQuery query,
        CancellationToken cancellationToken = default)
    {
        User? user = await this.userRepository.GetReadOnlyByIdAsync(query.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        return GetUserResult.From(user);
    }
}
