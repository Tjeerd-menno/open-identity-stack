using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Clients;
using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Infrastructure.Persistence;

namespace OpenIdentityStack.Infrastructure.Clients;

/// <summary>
/// EF Core implementation of IClientRepository.
/// Stores Client entities separately from OpenIddict applications for domain-driven design.
/// </summary>
public sealed class ClientRepository : IClientRepository
{
    private readonly OpenIdentityStackDbContext dbContext;

    public ClientRepository(OpenIdentityStackDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Client?> GetByIdAsync(ClientId id, CancellationToken cancellationToken = default)
    {
        return await this.dbContext.Clients
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Client?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
    {
        return await this.dbContext.Clients
            .FirstOrDefaultAsync(c => c.ClientIdValue == clientId, cancellationToken);
    }

    public async Task<(IReadOnlyList<Client> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Client> query = this.dbContext.Clients
            .AsNoTracking();

        int totalCount = await query.CountAsync(cancellationToken);

        List<Client> items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(Client client, CancellationToken cancellationToken = default)
    {
        await this.dbContext.Clients.AddAsync(client, cancellationToken);
    }

    public Task DeleteAsync(Client client, CancellationToken cancellationToken = default)
    {
        this.dbContext.Clients.Remove(client);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await this.dbContext.SaveChangesAsync(cancellationToken);
    }
}
