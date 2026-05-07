using SharedKernel;
using System.Diagnostics.CodeAnalysis;

namespace OpenIdentityStack.Domain.Clients;

/// <summary>
/// Strongly-typed identifier for a Client.
/// </summary>
public readonly record struct ClientId(Guid Value) : IStronglyTypedId<ClientId>
{
    public static ClientId Empty => new(Guid.Empty);
    
    public static ClientId Create() => new(Guid.NewGuid());

    public static ClientId NewId() => new(Guid.NewGuid());

    public static ClientId Parse(string value) => new(Guid.Parse(value));

    public static bool TryParse(string? value, [NotNullWhen(true)] out ClientId result)
    {
        if (Guid.TryParse(value, out Guid guid))
        {
            result = new ClientId(guid);
            return true;
        }

        result = Empty;
        return false;
    }

    public override string ToString() => this.Value.ToString();
}
