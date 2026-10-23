namespace OpenIdentityStack.Application.Resources;

/// <summary>
/// Process-local administrative projections for one authority revision.
/// That revision advances in the same transaction as role, group, grant, credential, client, and resource changes,
/// so a committed withdrawal is not served from an older entry. A time-to-live cache would delay revocation.
/// Token permission claims are a ceiling, not the cache key: trusting them would keep a removed role until refresh.
/// </summary>
internal sealed class AdministrativeAccessDecisionCache
{
    internal const int MaximumEntries = 1024;

    private readonly object gate = new();
    private readonly Dictionary<string, ResourceTokenProjection> entries = new(StringComparer.Ordinal);
    private long revision = long.MinValue;

    internal ResourceTokenProjection? Find(long revision, string fingerprint)
    {
        lock (this.gate)
        {
            if (this.revision == revision && this.entries.TryGetValue(fingerprint, out ResourceTokenProjection? projection))
            {
                return projection;
            }
        }

        return null;
    }

    internal void Store(long revision, string fingerprint, ResourceTokenProjection projection)
    {
        lock (this.gate)
        {
            // An in-flight evaluation that captured an older revision must not rewind a newer decision.
            if (revision < this.revision)
            {
                return;
            }

            if (revision > this.revision)
            {
                this.entries.Clear();
                this.revision = revision;
            }
            else if (this.entries.Count >= MaximumEntries && !this.entries.ContainsKey(fingerprint))
            {
                this.entries.Clear();
            }

            this.entries[fingerprint] = projection;
        }
    }

    internal void Evict(long revision, string fingerprint)
    {
        lock (this.gate)
        {
            if (this.revision == revision)
            {
                this.entries.Remove(fingerprint);
            }
        }
    }
}
