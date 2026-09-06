using System.Security.Cryptography;
using System.Text;

namespace OpenIdentityStack.Infrastructure.Audit;

/// <summary>Bounds persisted and logged actor identifiers without truncating distinct client identities.</summary>
public static class AuditActorIdentifier
{
    public static string Normalize(string actorId) => actorId.Length <= 128 && !actorId.StartsWith("sha256:", StringComparison.Ordinal)
        ? actorId
        : "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(actorId))).ToLowerInvariant();
}
