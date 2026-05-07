using System.Diagnostics.CodeAnalysis;

using SharedKernel;
namespace SharedKernel;

/// <summary>
/// Interface for strongly-typed IDs.
/// </summary>
/// <typeparam name="TSelf">The implementing type.</typeparam>
/// <typeparam name="TValue">The underlying value type.</typeparam>
public interface IStronglyTypedId<TSelf, TValue>
    where TSelf : struct, IStronglyTypedId<TSelf, TValue>
    where TValue : notnull
{
    /// <summary>
    /// Gets the underlying value.
    /// </summary>
    TValue Value { get; }
}

/// <summary>
/// Backward-compatible strongly-typed ID interface for Guid-based IDs.
/// </summary>
/// <typeparam name="TSelf">The implementing type.</typeparam>
public interface IStronglyTypedId<TSelf> : IStronglyTypedId<TSelf, Guid>
    where TSelf : struct, IStronglyTypedId<TSelf>
{
}

/// <summary>
/// Strongly-typed ID for User entities.
/// </summary>
public readonly record struct UserId(Guid Value) : IStronglyTypedId<UserId>
{
    public static UserId Empty => new(Guid.Empty);
    public static UserId Create() => new(Guid.NewGuid());

    public static bool TryParse(string? value, [NotNullWhen(true)] out UserId result)
    {
        if (Guid.TryParse(value, out Guid guid))
        {
            result = new UserId(guid);
            return true;
        }

        result = Empty;
        return false;
    }

    public override string ToString() => this.Value.ToString();
}
