using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Audit;
using OpenIdentityStack.Infrastructure.Persistence;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Identity;

/// <summary>Persists a terminal session state and its audit entry in one database transaction.</summary>
public sealed class CredentialTerminationService : ICredentialTerminationService
{
    private readonly OpenIdentityStackDbContext dbContext;
    private readonly IDateTimeProvider dateTimeProvider;

    public CredentialTerminationService(OpenIdentityStackDbContext dbContext, IDateTimeProvider dateTimeProvider)
    {
        this.dbContext = dbContext;
        this.dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result> TerminateSessionAsync(SessionId sessionId, string actorId, string reason, bool isLogout = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(reason))
        {
            return DomainError.Validation("Session.TerminationMetadataRequired", "Actor and reason are required.");
        }

        await using IDbContextTransaction transaction = await this.dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            UserSession? session = await this.dbContext.UserSessions
                .Include(x => x.ClientSessions)
                .FirstOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
            if (session is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return SessionErrors.NotFound;
            }

            if (session.Status == SessionStatus.Active)
            {
                Result termination = isLogout ? session.Logout(this.dateTimeProvider) : session.Revoke(this.dateTimeProvider);
                if (termination.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return termination;
                }

                this.dbContext.AuditLogEntries.Add(new AuditLogEntry
                {
                    UserId = actorId,
                    Action = isLogout ? "Session.LoggedOut" : "Session.Revoked",
                    EntityType = "Session",
                    EntityId = sessionId.Value.ToString(),
                    Details = reason,
                    Timestamp = this.dateTimeProvider.UtcNow
                });
                await this.dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<Result<int>> TerminateAllSessionsAsync(UserId userId, string actorId, string reason, SessionId? excludeSessionId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actorId) || string.IsNullOrWhiteSpace(reason))
        {
            return DomainError.Validation("Session.TerminationMetadataRequired", "Actor and reason are required.");
        }

        await using IDbContextTransaction transaction = await this.dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            User? user = await this.dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
            if (user is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return UserErrors.NotFound;
            }

            List<UserSession> sessions = await this.dbContext.UserSessions
                .Include(x => x.ClientSessions)
                .Where(x => x.UserId == userId && x.Status == SessionStatus.Active)
                .ToListAsync(cancellationToken);

            user.AdvanceSecurityVersion(this.dateTimeProvider);
            int terminated = 0;
            foreach (UserSession session in sessions)
            {
                if (excludeSessionId.HasValue && session.Id == excludeSessionId.Value)
                {
                    session.UpdateUserSecurityVersion(user.SecurityVersion);
                    continue;
                }

                Result result = session.Revoke(this.dateTimeProvider);
                if (result.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return result.Error;
                }
                terminated++;
            }

            this.dbContext.AuditLogEntries.Add(new AuditLogEntry
            {
                UserId = actorId,
                Action = "Session.RevokedAll",
                EntityType = "User",
                EntityId = userId.Value.ToString(),
                Details = reason,
                Timestamp = this.dateTimeProvider.UtcNow
            });
            await this.dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return terminated;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
