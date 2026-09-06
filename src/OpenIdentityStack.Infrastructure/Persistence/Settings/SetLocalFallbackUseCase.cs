using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Settings.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Settings;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Persistence.Settings;

/// <summary>
/// Implementation of the set local fallback use case.
/// </summary>
public sealed class SetLocalFallbackUseCase : ISetLocalFallbackUseCase
{
    private readonly IAuthenticationSettingsRepository settingsRepository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;
    private readonly IAdministrativeActorContext actorContext;

    public SetLocalFallbackUseCase(
        IAuthenticationSettingsRepository settingsRepository,
        IDateTimeProvider dateTimeProvider,
        IAuditLog auditLog,
        IAdministrativeActorContext actorContext)
    {
        this.settingsRepository = settingsRepository ?? throw new ArgumentNullException(nameof(settingsRepository));
        this.dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        this.auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
        this.actorContext = actorContext ?? throw new ArgumentNullException(nameof(actorContext));
    }

    /// <inheritdoc />
    public async Task<Result<SetLocalFallbackResult>> ExecuteAsync(
        SetLocalFallbackCommand command,
        CancellationToken cancellationToken = default)
    {
        AuthenticationSettings settings = await this.settingsRepository.GetOrCreateAsync(cancellationToken);
        bool previousValue = settings.LocalFallbackEnabled;

        Result result = command.Enabled
            ? settings.EnableLocalFallback(this.dateTimeProvider)
            : settings.DisableLocalFallback(this.dateTimeProvider);

        if (result.IsFailure)
        {
            return result.Error;
        }

        await this.settingsRepository.SaveChangesAsync(cancellationToken);

        // Audit the change
        await this.auditLog.LogChangeAsync(
            this.actorContext.AuditActorId,
            "SetLocalFallback",
            "AuthenticationSettings",
            settings.Id.Value.ToString(),
            previousValue.ToString(),
            settings.LocalFallbackEnabled.ToString(),
            cancellationToken);

        return new SetLocalFallbackResult(
            settings.LocalFallbackEnabled,
            settings.UpdatedAt);
    }
}
