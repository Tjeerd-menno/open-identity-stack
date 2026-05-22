
using Microsoft.EntityFrameworkCore;

using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Persistence.Roles;
/// <summary>
/// EF Core implementation of the role repository.
/// </summary>
internal sealed class RoleRepository : IRoleRepository
{
    private readonly OpenIdentityStackDbContext dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public RoleRepository(OpenIdentityStackDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<Role?> GetByIdAsync(RoleId id, CancellationToken cancellationToken = default)
    {
        return await this.dbContext.Roles
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        string normalizedName = name.Trim().ToLowerInvariant();
        return await this.dbContext.Roles
            .FirstOrDefaultAsync(r => r.Name == normalizedName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Role>> GetAllAsync(
        bool includeInactive = false,
        int skip = 0,
        int take = 100,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Role> query = this.dbContext.Roles;

        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string searchLower = search.Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // EF Core LINQ expression - ToLower() translates to SQL LOWER function
            query = query.Where(r =>
                r.Name.ToLower().Contains(searchLower) ||
                r.DisplayName.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311, CA1862
        }

        return await query
            .OrderBy(r => r.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> GetCountAsync(bool includeInactive = false, string? search = null, CancellationToken cancellationToken = default)
    {
        IQueryable<Role> query = this.dbContext.Roles;

        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string searchLower = search.Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // EF Core LINQ expression - ToLower() translates to SQL LOWER function
            query = query.Where(r =>
                r.Name.ToLower().Contains(searchLower) ||
                r.DisplayName.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311, CA1862
        }

        return await query.CountAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(Role role, CancellationToken cancellationToken = default)
    {
        await this.dbContext.Roles.AddAsync(role, cancellationToken);
    }

    /// <inheritdoc />
    public void Remove(Role role)
    {
        this.dbContext.Roles.Remove(role);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        string normalizedName = name.Trim().ToLowerInvariant();
        return await this.dbContext.Roles
            .AnyAsync(r => r.Name == normalizedName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AssignRoleAsync(RoleAssignment assignment, CancellationToken cancellationToken = default)
    {
        await this.dbContext.RoleAssignments.AddAsync(assignment, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveRoleAssignmentAsync(UserId userId, RoleId roleId, CancellationToken cancellationToken = default)
    {
        RoleAssignment? assignment = await this.dbContext.RoleAssignments
            .FirstOrDefaultAsync(ra => ra.UserId == userId && ra.RoleId == roleId, cancellationToken);

        if (assignment is not null)
        {
            this.dbContext.RoleAssignments.Remove(assignment);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsRoleAssignedAsync(UserId userId, RoleId roleId, CancellationToken cancellationToken = default)
    {
        return await this.dbContext.RoleAssignments
            .AnyAsync(ra => ra.UserId == userId && ra.RoleId == roleId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Role>> GetUserRolesAsync(
        UserId userId,
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        // Use method syntax to avoid anonymous-type Expression.New which is RequiresUnreferencedCode.
        IQueryable<Role> query = this.dbContext.Roles
            .Where(r => this.dbContext.RoleAssignments.Any(ra => ra.UserId == userId && ra.RoleId == r.Id));

        if (activeOnly)
        {
            query = query.Where(r => r.IsActive);
        }

        return await query
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await this.dbContext.SaveChangesAsync(cancellationToken);
    }
}
