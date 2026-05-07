using System.Diagnostics.CodeAnalysis;

using SharedKernel;

namespace OpenIdentityStack.Domain.Common;

/// <summary>
/// Strongly-typed ID for Role entities.
/// </summary>
public readonly record struct RoleId(Guid Value) : IStronglyTypedId<RoleId>
{
    public static RoleId Empty => new(Guid.Empty);
    public static RoleId Create() => new(Guid.NewGuid());

    public static bool TryParse(string? value, [NotNullWhen(true)] out RoleId result)
    {
        if (Guid.TryParse(value, out Guid guid))
        {
            result = new RoleId(guid);
            return true;
        }
        result = Empty;
        return false;
    }

    public override string ToString() => this.Value.ToString();
}

/// <summary>
/// Strongly-typed ID for Group entities.
/// </summary>
public readonly record struct GroupId(Guid Value) : IStronglyTypedId<GroupId>
{
    public static GroupId Empty => new(Guid.Empty);
    public static GroupId Create() => new(Guid.NewGuid());

    public static bool TryParse(string? value, [NotNullWhen(true)] out GroupId result)
    {
        if (Guid.TryParse(value, out Guid guid))
        {
            result = new GroupId(guid);
            return true;
        }
        result = Empty;
        return false;
    }

    public override string ToString() => this.Value.ToString();
}

/// <summary>
/// Strongly-typed ID for ServiceAccount entities.
/// </summary>
public readonly record struct ServiceAccountId(Guid Value) : IStronglyTypedId<ServiceAccountId>
{
    public static ServiceAccountId Empty => new(Guid.Empty);
    public static ServiceAccountId Create() => new(Guid.NewGuid());

    public static bool TryParse(string? value, [NotNullWhen(true)] out ServiceAccountId result)
    {
        if (Guid.TryParse(value, out Guid guid))
        {
            result = new ServiceAccountId(guid);
            return true;
        }
        result = Empty;
        return false;
    }

    public override string ToString() => this.Value.ToString();
}

/// <summary>
/// Strongly-typed ID for UserSession entities.
/// </summary>
public readonly record struct SessionId(Guid Value) : IStronglyTypedId<SessionId>
{
    public static SessionId Empty => new(Guid.Empty);
    public static SessionId Create() => new(Guid.NewGuid());

    public static bool TryParse(string? value, [NotNullWhen(true)] out SessionId result)
    {
        if (Guid.TryParse(value, out Guid guid))
        {
            result = new SessionId(guid);
            return true;
        }
        result = Empty;
        return false;
    }

    public override string ToString() => this.Value.ToString();
}

/// <summary>
/// Strongly-typed ID for UpstreamProvider entities.
/// </summary>
public readonly record struct ProviderId(Guid Value) : IStronglyTypedId<ProviderId>
{
    public static ProviderId Empty => new(Guid.Empty);
    public static ProviderId Create() => new(Guid.NewGuid());

    public static bool TryParse(string? value, [NotNullWhen(true)] out ProviderId result)
    {
        if (Guid.TryParse(value, out Guid guid))
        {
            result = new ProviderId(guid);
            return true;
        }
        result = Empty;
        return false;
    }

    public override string ToString() => this.Value.ToString();
}
