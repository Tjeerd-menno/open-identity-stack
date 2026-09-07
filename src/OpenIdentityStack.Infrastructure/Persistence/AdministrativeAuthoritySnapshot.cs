using OpenIdentityStack.Application.Abstractions;

namespace OpenIdentityStack.Infrastructure.Persistence;

public sealed class AdministrativeAuthoritySnapshot(OpenIdentityStackDbContext db) : IAdministrativeAuthoritySnapshot
{
    public Task CaptureAsync(CancellationToken cancellationToken = default) => db.CaptureAuthoritySnapshotAsync(cancellationToken);
}
