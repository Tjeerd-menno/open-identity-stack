using SharedKernel;
namespace SharedKernel;

/// <summary>
/// Default implementation of <see cref="IDateTimeProvider"/> that uses the system clock.
/// </summary>
public sealed class DateTimeProvider : IDateTimeProvider
{
    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public DateTimeOffset Now => DateTimeOffset.Now;
}
