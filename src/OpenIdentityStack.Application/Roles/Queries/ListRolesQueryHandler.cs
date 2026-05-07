
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Application.Roles.Queries;
/// <summary>
/// Use case interface for listing roles.
/// </summary>
public interface IListRolesQueryHandler
{
    /// <summary>
    /// Executes the list roles query.
    /// </summary>
    /// <param name="query">The query parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the paginated list or an error.</returns>
    Task<Result<ListRolesResponse>> HandleAsync(ListRolesQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handler for the ListRolesQuery.
/// </summary>
public sealed class ListRolesQueryHandler : IListRolesQueryHandler
{
    private readonly IRoleRepository roleRepository;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListRolesQueryHandler"/> class.
    /// </summary>
    /// <param name="roleRepository">The role repository.</param>
    public ListRolesQueryHandler(IRoleRepository roleRepository)
    {
        this.roleRepository = roleRepository;
    }

    /// <inheritdoc />
    public async Task<Result<ListRolesResponse>> HandleAsync(
        ListRolesQuery query,
        CancellationToken cancellationToken = default)
    {
        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 100);
        int skip = (page - 1) * pageSize;

        IReadOnlyList<Role> roles = await this.roleRepository.GetAllAsync(
            query.IncludeInactive,
            skip,
            pageSize,
            query.Search,
            cancellationToken);

        int totalCount = await this.roleRepository.GetCountAsync(
            query.IncludeInactive,
            query.Search,
            cancellationToken);

        var items = roles.Select(r => new RoleDto(
            r.Id.Value,
            r.Name,
            r.DisplayName,
            r.Description,
            r.IsSystemRole,
            r.IsActive,
            r.Permissions)).ToList();

        return new ListRolesResponse(items, totalCount, page, pageSize);
    }
}
