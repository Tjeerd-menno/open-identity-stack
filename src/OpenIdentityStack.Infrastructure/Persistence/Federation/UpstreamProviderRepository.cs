using Microsoft.EntityFrameworkCore;

using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;

namespace OpenIdentityStack.Infrastructure.Persistence.Federation;

/// <summary>
/// EF Core implementation of IUpstreamProviderRepository.
/// </summary>
public sealed class UpstreamProviderRepository : IUpstreamProviderRepository
{
    private readonly OpenIdentityStackDbContext dbContext;

    public UpstreamProviderRepository(OpenIdentityStackDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<UpstreamProvider?> GetByIdAsync(
        UpstreamProviderId id,
        CancellationToken cancellationToken = default)
    {
        return await this.dbContext.UpstreamProviders
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UpstreamProvider?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        string normalizedName = name.Trim().ToLowerInvariant();
        return await this.dbContext.UpstreamProviders
            .FirstOrDefaultAsync(p => p.Name == normalizedName, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UpstreamProvider>> GetActiveProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        return await this.dbContext.UpstreamProviders
            .Where(p => p.Status == ProviderStatus.Active)
            .OrderBy(p => p.DisplayName)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UpstreamProvider>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await this.dbContext.UpstreamProviders
            .OrderBy(p => p.DisplayName)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAsync(
        UpstreamProvider provider,
        CancellationToken cancellationToken = default)
    {
        await this.dbContext.UpstreamProviders.AddAsync(provider, cancellationToken);
    }

    /// <inheritdoc />
    public void RequireProvisioningPolicyWrite(UpstreamProvider provider) =>
        this.dbContext.Entry(provider).Property(value => value.JitProvisioningEnabled).IsModified = true;

    /// <inheritdoc />
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return this.dbContext.SaveChangesAsync(cancellationToken);
    }
}
