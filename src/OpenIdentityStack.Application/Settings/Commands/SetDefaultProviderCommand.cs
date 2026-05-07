using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Application.Settings.Commands;

/// <summary>
/// Command to set the default authentication provider.
/// </summary>
/// <param name="ProviderId">The provider ID. Use "local" for local accounts, or a GUID string for external providers.</param>
public sealed record SetDefaultProviderCommand(string ProviderId);

/// <summary>
/// Result of setting the default provider.
/// </summary>
public sealed record SetDefaultProviderResult(
    string DefaultProviderId,
    bool IsLocalDefault,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Use case for setting the default authentication provider.
/// </summary>
public interface ISetDefaultProviderUseCase
{
    /// <summary>
    /// Executes the use case to set the default authentication provider.
    /// </summary>
    Task<Result<SetDefaultProviderResult>> ExecuteAsync(
        SetDefaultProviderCommand command,
        CancellationToken cancellationToken = default);
}
