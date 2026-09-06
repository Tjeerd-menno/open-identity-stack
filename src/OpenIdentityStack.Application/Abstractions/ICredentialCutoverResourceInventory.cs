namespace OpenIdentityStack.Application.Abstractions;

public sealed record CutoverProtectedResource(Guid Id, string DisplayName, string Audience, string Scope, long Revision);
public sealed record CutoverResourceInventory(IReadOnlyList<CutoverAdministrativeClient> AdministrativeClients,
    IReadOnlyList<CutoverProtectedResource> BusinessResources, IReadOnlyList<CutoverBlocker> Blockers);

/// <summary>Reads current resource mappings and administrative client preparation for the cutover gate.</summary>
public interface ICredentialCutoverResourceInventory
{
    Task<CutoverResourceInventory> ReadAsync(CancellationToken cancellationToken = default);
}
