using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Persistence.Sessions;

/// <summary>
/// EF Core implementation of the session repository.
/// </summary>
public sealed class SessionRepository : ISessionRepository
{
    private readonly OpenIdentityStackDbContext context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionRepository"/> class.
    /// </summary>
    public SessionRepository(OpenIdentityStackDbContext context)
    {
        this.context = context;
    }

    /// <inheritdoc/>
    public async Task<UserSession?> GetByIdAsync(SessionId id, CancellationToken cancellationToken = default)
    {
        return await this.context.UserSessions
            .Include(s => s.ClientSessions)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserSession>> GetByUserIdAsync(
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        return await this.context.UserSessions
            .Include(s => s.ClientSessions)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<UserSession>> GetActiveByUserIdAsync(
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        return await this.context.UserSessions
            .Include(s => s.ClientSessions)
            .Where(s => s.UserId == userId && s.Status == SessionStatus.Active)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AddAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        await this.context.UserSessions.AddAsync(session, cancellationToken);
        await this.context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        this.context.UserSessions.Update(session);
        await this.context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<UserSession> Sessions, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        UserId? userIdFilter = null,
        SessionStatus? statusFilter = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<UserSession> query = this.context.UserSessions
            .Include(s => s.ClientSessions)
            .AsQueryable();

        if (userIdFilter.HasValue)
        {
            query = query.Where(s => s.UserId == userIdFilter.Value);
        }

        if (statusFilter.HasValue)
        {
            query = query.Where(s => s.Status == statusFilter.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // Parameterless ToLower translates to SQL for both PostgreSQL and SQLite.
            query = query.Where(s => s.IpAddress.ToLower().Contains(term) || s.UserAgent.ToLower().Contains(term));
#pragma warning restore CA1304, CA1311, CA1862
        }

        int totalCount = await query.CountAsync(cancellationToken);

        List<UserSession> sessions = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (sessions, totalCount);
    }
}
