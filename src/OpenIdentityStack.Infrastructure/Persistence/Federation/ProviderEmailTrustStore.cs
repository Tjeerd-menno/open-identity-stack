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
        UpstreamProvider? provider = await dbContext.UpstreamProviders.FirstOrDefaultAsync(p => p.Id == providerId, cancellationToken);
        if (provider is null)
        {
            return Result.Failure(UpstreamProviderErrors.NotFound);
        }

        provider.SetEmailVerificationTrust(trusted);
        if (!trusted)
        {
            DateTimeOffset withdrawnAt = DateTimeOffset.UtcNow;
            List<User> affectedUsers = await dbContext.Users
                .Where(u => u.EmailVerificationEvidence.Any(e => e.ProviderId == providerId.Value && e.WithdrawnAt == null))
                .ToListAsync(cancellationToken);
            foreach (User user in affectedUsers)
            {
                user.WithdrawProviderEmailVerification(providerId.Value, withdrawnAt);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await auditLog.LogAsync(actorId, "Provider.EmailVerificationTrustChanged", "UpstreamProvider",
            providerId.Value.ToString(), trusted ? "Trusted for verified-email evidence." : "Trust withdrawn; dependent evidence invalidated.", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result.Success();
    }
}
