using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Application.Settings.Queries;

/// <summary>
/// Query for getting authentication settings.
/// </summary>
public sealed record GetAuthenticationSettingsQuery;

/// <summary>
/// Result of the authentication settings query.
/// </summary>
public sealed record AuthenticationSettingsDto(
    string DefaultProviderId,
    bool IsLocalDefault,
    bool LocalFallbackEnabled,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Handler for the authentication settings query.
/// </summary>
public interface IGetAuthenticationSettingsQueryHandler
{
    /// <summary>
    /// Handles the query to get authentication settings.
    /// </summary>
    Task<Result<AuthenticationSettingsDto>> HandleAsync(
        GetAuthenticationSettingsQuery query,
        CancellationToken cancellationToken = default);
}
