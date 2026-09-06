using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Settings.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Settings;
using OpenIdentityStack.Infrastructure.Persistence.Settings;

using SharedKernel;
#pragma warning disable IDE0007 // Use var instead of explicit type
#pragma warning disable IDE0008 // Use explicit type instead of var

namespace OpenIdentityStack.Infrastructure.Tests.Persistence.Settings;

/// <summary>
/// Tests for SetLocalFallbackUseCase.
/// </summary>
public sealed class SetLocalFallbackUseCaseTests
{
    private readonly IAuthenticationSettingsRepository _settingsRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLog _auditLog;
    private readonly IAdministrativeActorContext _actorContext;
    private readonly SetLocalFallbackUseCase _useCase;
    private readonly DateTimeOffset _now = new(2026, 2, 3, 12, 0, 0, TimeSpan.Zero);

    public SetLocalFallbackUseCaseTests()
    {
        this._settingsRepository = Substitute.For<IAuthenticationSettingsRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._auditLog = Substitute.For<IAuditLog>();
        this._actorContext = Substitute.For<IAdministrativeActorContext>();
        this._actorContext.AuditActorId.Returns("user:settings-admin");
        this._dateTimeProvider.UtcNow.Returns(this._now);

        this._useCase = new SetLocalFallbackUseCase(
            this._settingsRepository,
            this._dateTimeProvider,
            this._auditLog,
            this._actorContext);
    }

    #region Success Cases

    [Fact]
    public async Task ExecuteAsync_EnableLocalFallback_ReturnsSuccess()
    {
        // Arrange
        AuthenticationSettings settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        settings.DisableLocalFallback(this._dateTimeProvider);

        this._settingsRepository.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(settings);

        var command = new SetLocalFallbackCommand(true);

        // Act
        Result<SetLocalFallbackResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.LocalFallbackEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_DisableLocalFallback_ReturnsSuccess()
    {
        // Arrange
        AuthenticationSettings settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);

        this._settingsRepository.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(settings);

        var command = new SetLocalFallbackCommand(false);

        // Act
        Result<SetLocalFallbackResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.LocalFallbackEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_SavesChanges()
    {
        // Arrange
        AuthenticationSettings settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        this._settingsRepository.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(settings);

        var command = new SetLocalFallbackCommand(true);

        // Act
        await this._useCase.ExecuteAsync(command);

        // Assert
        await this._settingsRepository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_LogsAuditEntry()
    {
        // Arrange
        AuthenticationSettings settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        this._settingsRepository.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(settings);

        var command = new SetLocalFallbackCommand(false);

        // Act
        await this._useCase.ExecuteAsync(command);

        // Assert
        await this._auditLog.Received(1).LogChangeAsync(
            "user:settings-admin",
            "SetLocalFallback",
            "AuthenticationSettings",
            Arg.Any<string>(),
            "True",
            "False",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsUpdatedTimestamp()
    {
        // Arrange
        AuthenticationSettings settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        this._settingsRepository.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(settings);

        var command = new SetLocalFallbackCommand(true);

        // Act
        Result<SetLocalFallbackResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.Value.UpdatedAt.ShouldBe(this._now);
    }

    #endregion

    #region Constructor Validation

    [Fact]
    public void Constructor_ThrowsForNullSettingsRepository()
    {
        Should.Throw<ArgumentNullException>(() =>
            new SetLocalFallbackUseCase(null!, this._dateTimeProvider, this._auditLog, this._actorContext));
    }

    [Fact]
    public void Constructor_ThrowsForNullDateTimeProvider()
    {
        Should.Throw<ArgumentNullException>(() =>
            new SetLocalFallbackUseCase(this._settingsRepository, null!, this._auditLog, this._actorContext));
    }

    [Fact]
    public void Constructor_ThrowsForNullAuditLog()
    {
        Should.Throw<ArgumentNullException>(() =>
            new SetLocalFallbackUseCase(this._settingsRepository, this._dateTimeProvider, null!, this._actorContext));
    }

    [Fact]
    public void Constructor_ThrowsForNullActorContext()
    {
        Should.Throw<ArgumentNullException>(() =>
            new SetLocalFallbackUseCase(this._settingsRepository, this._dateTimeProvider, this._auditLog, null!));
    }

    #endregion
}
