using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Infrastructure.Persistence;

public partial class OpenIdentityStackDbContext
{
    /// <summary>Serializes credential issuance and provider policy mutations at the authority boundary.</summary>
    internal async Task LockCredentialBoundaryAsync(CancellationToken cancellationToken)
    {
        // The caller owns the transaction. SaveChanges still validates and increments the authority fence.
        if (this.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Credential-boundary serialization requires an active transaction.");
        }
        if (await this.Set<AdministrativeAuthorityRevision>().Where(value => value.Id == 1)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Revision, value => value.Revision), cancellationToken) != 1)
        {
            throw new InvalidOperationException("Administrative authority revision is unavailable.");
        }
    }

    /// <summary>Serializes issuance only with mutations of the same user's credential boundary.</summary>
    internal async Task LockUserCredentialBoundaryAsync(UserId userId, CancellationToken cancellationToken)
    {
        if (this.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Subject credential-boundary serialization requires an active transaction.");
        }

        // A no-op update takes the same row lock as a withdrawal's credential-revision update.
        // Missing users are handled by the subsequent current-revision validation.
        await this.Users.Where(user => user.Id == userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.CredentialRevision, user => user.CredentialRevision), cancellationToken);
    }
}
