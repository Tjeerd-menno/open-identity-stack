using System.Diagnostics.CodeAnalysis;
using SharedKernel;

namespace OpenIdentityStack.Domain.Applications;

/// <summary>
/// Strongly-typed identifier for an application.
/// </summary>
public readonly record struct ApplicationId(Guid Value) : IStronglyTypedId<ApplicationId>
{
    public static ApplicationId Empty => new(Guid.Empty);

    public static ApplicationId Create() => new(Guid.NewGuid());

    public static ApplicationId NewId() => new(Guid.NewGuid());

    public static ApplicationId Parse(string value) => new(Guid.Parse(value));

    public static bool TryParse(string? value, [NotNullWhen(true)] out ApplicationId result)
    {
        if (Guid.TryParse(value, out Guid guid))
        {
            result = new ApplicationId(guid);
            return true;
        }

        result = Empty;
        return false;
    }

    public override string ToString() => this.Value.ToString();
}
