
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
    public async Task<IReadOnlyList<Role>> GetByNamesAsync(IEnumerable<string> names, CancellationToken cancellationToken = default)
    {
        // Chunking keeps each IN clause well within provider parameter limits.
        const int chunkSize = 500;
        var normalizedNames = names
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalizedNames.Count == 0)
        {
            return [];
        }

        var roles = new List<Role>(normalizedNames.Count);
        foreach (string[] chunk in normalizedNames.Chunk(chunkSize))
        {
            List<Role> batch = await this.dbContext.Roles
                .AsNoTracking()
                .Where(r => chunk.Contains(r.Name))
                .ToListAsync(cancellationToken);
            roles.AddRange(batch);
        }

        return roles;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Role>> GetByIdsAsync(IEnumerable<RoleId> ids, CancellationToken cancellationToken = default)
    {
        const int chunkSize = 500;
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return [];
        }

        var roles = new List<Role>(distinctIds.Count);
        foreach (RoleId[] chunk in distinctIds.Chunk(chunkSize))
        {
            List<Role> batch = await this.dbContext.Roles
                .AsNoTracking()
                .Where(r => chunk.Contains(r.Id))
                .ToListAsync(cancellationToken);
            roles.AddRange(batch);
        }

        return roles;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Role>> GetAllAsync(
        bool includeInactive = false,
        int skip = 0,
        int take = 100,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Role> query = this.dbContext.Roles.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string searchLower = search.Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // EF Core LINQ expression - ToLower() translates to SQL LOWER function
            // Role.Name is persisted lower-cased, so it is compared directly (keeps the index usable).
            query = query.Where(r =>
                r.Name.Contains(searchLower) ||
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
        IQueryable<Role> query = this.dbContext.Roles.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string searchLower = search.Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // EF Core LINQ expression - ToLower() translates to SQL LOWER function
            query = query.Where(r =>
                r.Name.Contains(searchLower) ||
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
        IQueryable<Role> query = from ra in this.dbContext.RoleAssignments
                join r in this.dbContext.Roles on ra.RoleId equals r.Id
                    where ra.UserId == userId
                    select r;

        if (activeOnly)
        {
            query = query.Where(r => r.IsActive);
        }

        return await query
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await this.dbContext.SaveChangesAsync(cancellationToken);
    }
}
