using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Settings;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Persistence.Settings;

/// <summary>
/// EF Core implementation of the IAuthenticationSettingsRepository interface.
/// </summary>
public sealed class AuthenticationSettingsRepository : IAuthenticationSettingsRepository
{
    private readonly OpenIdentityStackDbContext dbContext;
    private readonly IDateTimeProvider dateTimeProvider;

    public AuthenticationSettingsRepository(
        OpenIdentityStackDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        this.dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        this.dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
    }

    /// <inheritdoc />
    public async Task<AuthenticationSettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        return await this.dbContext.AuthenticationSettings
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AuthenticationSettings> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        AuthenticationSettings? settings = await this.GetAsync(cancellationToken);
        
        if (settings is null)
        {
            settings = AuthenticationSettings.CreateDefault(this.dateTimeProvider);
            await this.AddAsync(settings, cancellationToken);
            await this.SaveChangesAsync(cancellationToken);
        }

        return settings;
    }

    /// <inheritdoc />
    public async Task AddAsync(AuthenticationSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await this.dbContext.AuthenticationSettings.AddAsync(settings, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await this.dbContext.SaveChangesAsync(cancellationToken);
    }
}
