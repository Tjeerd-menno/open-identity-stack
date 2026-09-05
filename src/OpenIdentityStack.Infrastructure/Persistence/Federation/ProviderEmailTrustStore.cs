using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Persistence.Federation;

public sealed class ProviderEmailTrustStore(OpenIdentityStackDbContext dbContext, IAuditLog auditLog, IEmailTrustCredentialInvalidator invalidator) : IProviderEmailTrustStore
{
    public async Task<Result> SetAsync(UpstreamProviderId providerId, bool trusted, string actorId, CancellationToken cancellationToken)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.LockAuthorityBeforeProviderAsync(cancellationToken);
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
        if (provider.TrustEmailVerification == trusted)
        {
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }

        provider.SetEmailVerificationTrust(trusted);
        if (!trusted)
        {
            DateTimeOffset withdrawnAt = DateTimeOffset.UtcNow;
            Guid? afterUserId = null;
            while (true)
            {
                // Seek the active provider index, not the Users table. A cursor also skips retired
                // index entries still visible to PostgreSQL until this atomic transaction commits.
                FormattableString pageQuery = afterUserId is { } after
                    ? (FormattableString)$"""SELECT DISTINCT "UserId" AS "Value" FROM "UserEmailVerificationEvidence" WHERE "ProviderId" = {providerId.Value} AND "WithdrawnAt" IS NULL AND "UserId" > {after}"""
                    : (FormattableString)$"""SELECT DISTINCT "UserId" AS "Value" FROM "UserEmailVerificationEvidence" WHERE "ProviderId" = {providerId.Value} AND "WithdrawnAt" IS NULL""";
                List<Guid> page = await dbContext.Database.SqlQuery<Guid>(pageQuery)
                    .TagWith("ProviderEmailTrust:active-evidence-batch").OrderBy(id => id).Take(100).ToListAsync(cancellationToken);
                if (page.Count == 0) { break; }
                afterUserId = page[^1];
                UserId[] userIds = page.Select(id => new UserId(id)).ToArray();
                List<User> affectedUsers = await dbContext.Users.Where(user => userIds.Contains(user.Id)).ToListAsync(cancellationToken);
                foreach (User user in affectedUsers)
                {
                    if (user.WithdrawProviderEmailVerification(providerId.Value, withdrawnAt))
                    {
                        EmailTrustCredentialInvalidation revoked = await invalidator.RevokeAsync(user.Id, cancellationToken);
                        await auditLog.LogAsync(actorId, "Provider.EmailTrustCredentialsRevoked", "User", user.Id.Value.ToString(),
                            $"Provider {providerId.Value}: {revoked.Tokens} tokens, {revoked.Authorizations} authorizations, {revoked.Sessions} sessions revoked.", cancellationToken);
                    }
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
