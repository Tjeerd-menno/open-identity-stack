using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Settings.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Settings;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Persistence.Settings;

/// <summary>
/// Implementation of the authentication settings query handler.
/// </summary>
public sealed class GetAuthenticationSettingsQueryHandler : IGetAuthenticationSettingsQueryHandler
{
    private readonly IAuthenticationSettingsRepository settingsRepository;

    public GetAuthenticationSettingsQueryHandler(IAuthenticationSettingsRepository settingsRepository)
    {
        this.settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
    }

    /// <inheritdoc />
    public async Task<Result<AuthenticationSettingsDto>> HandleAsync(
        GetAuthenticationSettingsQuery query,
        CancellationToken cancellationToken = default)
    {
        AuthenticationSettings settings = await this.settingsRepository.GetOrCreateAsync(cancellationToken);

        return new AuthenticationSettingsDto(
            settings.DefaultProviderId,
            settings.IsLocalDefault,
            settings.LocalFallbackEnabled,
            settings.UpdatedAt);
    }
}
