using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Persistence.Federation;

public sealed class ProviderEmailTrustStore(OpenIdentityStackDbContext dbContext, IAuditLog auditLog) : IProviderEmailTrustStore
{
    public async Task<Result> SetAsync(UpstreamProviderId providerId, bool trusted, string actorId, CancellationToken cancellationToken)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        // Lock before reading affected users so evidence committed by an earlier login is included.
        int found = await dbContext.UpstreamProviders.Where(provider => provider.Id == providerId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(provider => provider.JitProvisioningEnabled,
                provider => provider.JitProvisioningEnabled), cancellationToken);
        if (found != 1) { return Result.Failure(UpstreamProviderErrors.NotFound); }
        UpstreamProvider? provider = await dbContext.UpstreamProviders.FirstOrDefaultAsync(p => p.Id == providerId, cancellationToken);
        if (provider is null)
        {
            return Result.Failure(UpstreamProviderErrors.NotFound);
        }

        await dbContext.Entry(provider).ReloadAsync(cancellationToken);
        provider.SetEmailVerificationTrust(trusted);
        if (!trusted)
        {
            DateTimeOffset withdrawnAt = DateTimeOffset.UtcNow;
            while (true)
            {
                // Saved withdrawals leave the predicate, so no population-sized ID list or offset is needed.
                List<User> affectedUsers = await dbContext.Users
                    .Where(u => u.EmailVerificationEvidence.Any(e => e.ProviderId == providerId.Value && e.WithdrawnAt == null))
                    .OrderBy(user => user.Id)
                    .Take(100)
                    .ToListAsync(cancellationToken);
                if (affectedUsers.Count == 0) { break; }
                foreach (User user in affectedUsers)
                {
                    user.WithdrawProviderEmailVerification(providerId.Value, withdrawnAt);
                }
                await dbContext.SaveChangesAsync(cancellationToken);
                // The transaction and provider lock survive detachment; previous batches cannot accumulate in memory.
                dbContext.ChangeTracker.Clear();
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync(actorId, "Provider.EmailVerificationTrustChanged", "UpstreamProvider",
            providerId.Value.ToString(), trusted ? "Trusted for verified-email evidence." : "Trust withdrawn; dependent evidence invalidated.", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
