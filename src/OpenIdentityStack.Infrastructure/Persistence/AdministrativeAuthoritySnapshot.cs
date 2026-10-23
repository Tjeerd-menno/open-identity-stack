using OpenIdentityStack.Application.Abstractions;

namespace OpenIdentityStack.Infrastructure.Persistence;

public sealed class AdministrativeAuthoritySnapshot(OpenIdentityStackDbContext db) : IAdministrativeAuthoritySnapshot
{
    public Task<long> CaptureAsync(CancellationToken cancellationToken = default) => db.CaptureAuthoritySnapshotAsync(cancellationToken);
}
