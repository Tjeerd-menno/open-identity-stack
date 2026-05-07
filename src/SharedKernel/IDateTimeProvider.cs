using SharedKernel;
namespace SharedKernel;

/// <summary>
/// Provides the current date and time.
/// This abstraction allows for testable time-dependent code.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Gets the current local date and time.
    /// </summary>
    DateTimeOffset Now { get; }
}
