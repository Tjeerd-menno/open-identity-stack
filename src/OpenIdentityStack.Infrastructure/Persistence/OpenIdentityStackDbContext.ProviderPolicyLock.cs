using Microsoft.EntityFrameworkCore;

namespace OpenIdentityStack.Infrastructure.Persistence;

public partial class OpenIdentityStackDbContext
{
    /// <summary>Establishes authority-before-provider lock ordering for compound security mutations.</summary>
    internal async Task LockAuthorityBeforeProviderAsync(CancellationToken cancellationToken)
    {
        // The caller owns the transaction. SaveChanges still validates and increments the authority fence.
        if (this.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Provider policy serialization requires an active transaction.");
        }
        if (await this.Set<AdministrativeAuthorityRevision>().Where(value => value.Id == 1)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Revision, value => value.Revision), cancellationToken) != 1)
        {
            throw new InvalidOperationException("Administrative authority revision is unavailable.");
        }
    }
}
