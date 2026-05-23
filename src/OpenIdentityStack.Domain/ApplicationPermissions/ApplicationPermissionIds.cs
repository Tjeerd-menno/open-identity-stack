using System.Diagnostics.CodeAnalysis;
using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Domain.ApplicationPermissions;

public readonly record struct RegisteredApplicationId(Guid Value) : IStronglyTypedId<RegisteredApplicationId>
{
    public static RegisteredApplicationId Empty => new(Guid.Empty);

    public static RegisteredApplicationId Create() => new(Guid.NewGuid());

    public static bool TryParse(string? value, [NotNullWhen(true)] out RegisteredApplicationId result)
    {
        if (Guid.TryParse(value, out Guid guid))
        {
            result = new RegisteredApplicationId(guid);
            return true;
        }

        result = Empty;
        return false;
    }

    public override string ToString() => this.Value.ToString();
}

public readonly record struct ApplicationPermissionId(Guid Value) : IStronglyTypedId<ApplicationPermissionId>
{
    public static ApplicationPermissionId Empty => new(Guid.Empty);

    public static ApplicationPermissionId Create() => new(Guid.NewGuid());

    public static bool TryParse(string? value, [NotNullWhen(true)] out ApplicationPermissionId result)
    {
        if (Guid.TryParse(value, out Guid guid))
        {
            result = new ApplicationPermissionId(guid);
            return true;
        }

        result = Empty;
        return false;
    }

    public override string ToString() => this.Value.ToString();
}

public readonly record struct DelegatedMaintainerId(Guid Value) : IStronglyTypedId<DelegatedMaintainerId>
{
    public static DelegatedMaintainerId Empty => new(Guid.Empty);

    public static DelegatedMaintainerId Create() => new(Guid.NewGuid());

    public static bool TryParse(string? value, [NotNullWhen(true)] out DelegatedMaintainerId result)
    {
        if (Guid.TryParse(value, out Guid guid))
        {
            result = new DelegatedMaintainerId(guid);
            return true;
        }

        result = Empty;
        return false;
    }

    public override string ToString() => this.Value.ToString();
}
