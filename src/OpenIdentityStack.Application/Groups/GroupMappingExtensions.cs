using OpenIdentityStack.Domain.Groups;

namespace OpenIdentityStack.Application.Groups;

public static class GroupMappingExtensions
{
    public static Guid GetDeterministicId(this GroupMapping mapping)
    {
        string content = $"{mapping.Type}:{mapping.Target}:{mapping.Value ?? ""}:{mapping.TokenTarget}";
        // Use SHA256 as MD5 is considered broken (CA5351)
        byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content));
        // Take first 16 bytes for Guid
        byte[] guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);
        return new Guid(guidBytes);
    }
}
