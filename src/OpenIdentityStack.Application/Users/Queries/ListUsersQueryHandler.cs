using OpenIdentityStack.Application.Abstractions;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Queries;

/// <summary>
/// Implementation of the list users query handler.
/// </summary>
public sealed class ListUsersQueryHandler : IListUsersQueryHandler
{
    private readonly IUserRepository userRepository;

    public ListUsersQueryHandler(IUserRepository userRepository)
    {
        this.userRepository = userRepository;
    }

    /// <inheritdoc />
    public async Task<Result<ListUsersResult>> HandleAsync(
        ListUsersQuery query,
        CancellationToken cancellationToken = default)
    {
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);

        (IReadOnlyList<Domain.Users.User>? users, int totalCount) = await this.userRepository.ListAsync(
            page,
            pageSize,
            query.Search,
            cancellationToken);

        var items = users.Select(u => new UserListItem(
            u.Id,
            u.Email,
            u.DisplayName,
            u.Status,
            u.CreatedAt)).ToList();

        return new ListUsersResult(items, totalCount, page, pageSize);
    }
}
