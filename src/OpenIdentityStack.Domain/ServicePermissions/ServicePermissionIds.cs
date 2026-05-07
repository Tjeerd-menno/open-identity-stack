using System.Diagnostics.CodeAnalysis;
using OpenIdentityStack.Domain.Common;

namespace OpenIdentityStack.Domain.ServicePermissions;

public readonly record struct RegisteredServiceId(Guid Value) : IStronglyTypedId<RegisteredServiceId>
{
    public static RegisteredServiceId Empty => new(Guid.Empty);

    public static RegisteredServiceId Create() => new(Guid.NewGuid());

    public static bool TryParse(string? value, [NotNullWhen(true)] out RegisteredServiceId result)
    {
        if (Guid.TryParse(value, out Guid guid))
        {
            result = new RegisteredServiceId(guid);
            return true;
        }

        result = Empty;
        return false;
    }

    public override string ToString() => this.Value.ToString();
}

public readonly record struct ServicePermissionId(Guid Value) : IStronglyTypedId<ServicePermissionId>
{
    public static ServicePermissionId Empty => new(Guid.Empty);

    public static ServicePermissionId Create() => new(Guid.NewGuid());

    public static bool TryParse(string? value, [NotNullWhen(true)] out ServicePermissionId result)
    {
        if (Guid.TryParse(value, out Guid guid))
        {
            result = new ServicePermissionId(guid);
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
