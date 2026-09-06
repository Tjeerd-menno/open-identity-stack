using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Settings.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Settings;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Persistence.Settings;

/// <summary>
/// Implementation of the set default provider use case.
/// </summary>
public sealed class SetDefaultProviderUseCase : ISetDefaultProviderUseCase
{
    private readonly IAuthenticationSettingsRepository settingsRepository;
    private readonly IUpstreamProviderRepository providerRepository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;
    private readonly IAdministrativeActorContext actorContext;

    public SetDefaultProviderUseCase(
        IAuthenticationSettingsRepository settingsRepository,
        IUpstreamProviderRepository providerRepository,
        IDateTimeProvider dateTimeProvider,
        IAuditLog auditLog,
        IAdministrativeActorContext actorContext)
    {
        this.settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
        this.providerRepository = providerRepository ?? throw new ArgumentNullException(nameof(providerRepository));
        this.dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        this.auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
        this.actorContext = actorContext ?? throw new ArgumentNullException(nameof(actorContext));
    }

    /// <inheritdoc />
    public async Task<Result<SetDefaultProviderResult>> ExecuteAsync(
        SetDefaultProviderCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.ProviderId))
        {
            return AuthenticationSettingsErrors.InvalidProviderId;
        }

        AuthenticationSettings settings = await this.settingsRepository.GetOrCreateAsync(cancellationToken);
        string previousProvider = settings.DefaultProviderId;

        Result result;
        if (command.ProviderId.Equals(AuthenticationSettings.LocalProviderId, StringComparison.OrdinalIgnoreCase))
        {
            result = settings.SetDefaultToLocal(this.dateTimeProvider);
        }
        else
        {
            // Validate that the provider exists and is active
            if (!Guid.TryParse(command.ProviderId, out Guid providerGuid))
            {
                return AuthenticationSettingsErrors.InvalidProviderId;
            }

            var providerId = UpstreamProviderId.From(providerGuid);
            UpstreamProvider? provider = await this.providerRepository.GetByIdAsync(providerId, cancellationToken);

            if (provider is null)
            {
                return AuthenticationSettingsErrors.ProviderNotFound;
            }

            if (!provider.IsActive)
            {
                return AuthenticationSettingsErrors.ProviderDisabled;
            }

            result = settings.SetDefaultProvider(providerId, this.dateTimeProvider);
        }

        if (result.IsFailure)
        {
            return result.Error;
        }

        await this.settingsRepository.SaveChangesAsync(cancellationToken);

        // Audit the change
        await this.auditLog.LogChangeAsync(
            this.actorContext.AuditActorId,
            "SetDefaultProvider",
            "AuthenticationSettings",
            settings.Id.Value.ToString(),
            previousProvider,
            settings.DefaultProviderId,
            cancellationToken);

        return new SetDefaultProviderResult(
            settings.DefaultProviderId,
            settings.IsLocalDefault,
            settings.UpdatedAt);
    }
}
