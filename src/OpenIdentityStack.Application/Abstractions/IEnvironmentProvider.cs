namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Provides information about the current environment.
/// </summary>
public interface IEnvironmentProvider
{
    /// <summary>
    /// Gets a value indicating whether the application is running in development mode.
    /// </summary>
    bool IsDevelopment { get; }
}
