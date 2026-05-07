using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Application.Settings.Commands;

/// <summary>
/// Command to enable or disable local fallback authentication.
/// </summary>
/// <param name="Enabled">Whether local fallback should be enabled.</param>
public sealed record SetLocalFallbackCommand(bool Enabled);

/// <summary>
/// Result of setting local fallback.
/// </summary>
public sealed record SetLocalFallbackResult(
    bool LocalFallbackEnabled,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Use case for enabling/disabling local fallback authentication.
/// </summary>
public interface ISetLocalFallbackUseCase
{
    /// <summary>
    /// Executes the use case to set local fallback.
    /// </summary>
    Task<Result<SetLocalFallbackResult>> ExecuteAsync(
        SetLocalFallbackCommand command,
        CancellationToken cancellationToken = default);
}
