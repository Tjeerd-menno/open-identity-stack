using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Persistence.Users;

/// <summary>
/// EF Core implementation of the IUserRepository interface.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly OpenIdentityStackDbContext dbContext;

    public UserRepository(OpenIdentityStackDbContext dbContext)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task<User?> GetByIdAsync(UserId id, CancellationToken cancellationToken = default)
    {
        return await this.dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<User>> GetByIdsAsync(IEnumerable<UserId> ids, CancellationToken cancellationToken = default)
    {
        // Distinct guards against duplicate identifiers; chunking keeps each IN clause
        // well within provider parameter limits for large pages.
        const int chunkSize = 500;
        var distinctIds = ids.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return [];
        }

        var users = new List<User>(distinctIds.Count);
        foreach (UserId[] chunk in distinctIds.Chunk(chunkSize))
        {
            List<User> batch = await this.dbContext.Users
                .Where(u => chunk.Contains(u.Id))
                .ToListAsync(cancellationToken);
            users.AddRange(batch);
        }

        return users;
    }

    /// <inheritdoc />
    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        string normalizedEmail = email.Trim().ToUpperInvariant();
        return await this.dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User?> GetByPreferredUsernameAsync(string preferredUsername, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(preferredUsername))
        {
            return null;
        }

        string normalizedPreferredUsername = preferredUsername.Trim().ToUpperInvariant();
        return await this.dbContext.Users
            .FirstOrDefaultAsync(
                u => u.NormalizedPreferredUsername == normalizedPreferredUsername,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        string normalizedEmail = email.Trim().ToUpperInvariant();
        return await this.dbContext.Users
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        await this.dbContext.Users.AddAsync(user, cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        this.dbContext.Users.Update(user);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        this.dbContext.Users.Remove(user);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<User> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<User> query = this.dbContext.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string normalizedSearch = search.Trim().ToUpperInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // Parameterless ToUpper translates to SQL for both PostgreSQL and SQLite.
            query = query.Where(u =>
                u.NormalizedEmail.Contains(normalizedSearch) ||
                u.DisplayName.ToUpper().Contains(normalizedSearch));
#pragma warning restore CA1304, CA1311, CA1862
        }

        int totalCount = await query.CountAsync(cancellationToken);

        List<User> items = await query
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await this.dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<User?> FindByUpstreamIdentityAsync(
        UpstreamProviderId providerId,
        string subjectId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subjectId))
        {
            return null;
        }

        // Query users that have an upstream identity matching the provider and subject
        return await this.dbContext.Users
            .FirstOrDefaultAsync(u => u.UpstreamIdentities
                .Any(i => i.ProviderId == providerId && i.SubjectId == subjectId), cancellationToken);
    }
}
