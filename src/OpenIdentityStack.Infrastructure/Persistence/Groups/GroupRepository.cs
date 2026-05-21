using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Persistence.Groups;

internal sealed class GroupRepository : IGroupRepository
{
    private readonly OpenIdentityStackDbContext dbContext;

    public GroupRepository(OpenIdentityStackDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Group?> GetByIdAsync(GroupId id, CancellationToken cancellationToken = default)
    {
        // We include Mappings and Memberships? Use Cases might need them.
        // AddUserToGroup needs memberships to check overlap (though aggregate handles it, we should load it).
        // Group.AddMember -> checks _memberships.Any.
        // So we MUST include Memberships.
        
        return await this.dbContext.Groups
            .Include(g => g.Memberships)
            .Include(g => g.Mappings)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<Group?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        // Case-insensitive comparison? 
        return await this.dbContext.Groups
            .Include(g => g.Memberships)
            .Include(g => g.Mappings)
            .FirstOrDefaultAsync(g => g.Name == name, cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await this.dbContext.Groups.AnyAsync(g => g.Name == name, cancellationToken);
    }

    public async Task<IReadOnlyList<Group>> GetGroupsForUserAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        return await this.dbContext.Groups
            .Include(g => g.Mappings) // Needed for token generation/effective roles
            .Where(g => g.Memberships.Any(m => m.UserId == userId))
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Group> Items, int TotalCount)> ListAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default)
    {
        IQueryable<Group> query = this.dbContext.Groups;

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(g => g.Name.Contains(search));
        }

        int totalCount = await query.CountAsync(cancellationToken);

        List<Group> items = await query
            .OrderBy(g => g.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(Group group, CancellationToken cancellationToken = default)
    {
        await this.dbContext.Groups.AddAsync(group, cancellationToken);
    }

    public void Remove(Group group)
    {
        this.dbContext.Groups.Remove(group);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await this.dbContext.SaveChangesAsync(cancellationToken);
    }
}
