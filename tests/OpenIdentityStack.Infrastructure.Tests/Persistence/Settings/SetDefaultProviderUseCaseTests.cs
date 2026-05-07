using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Settings.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Settings;
using OpenIdentityStack.Infrastructure.Persistence.Settings;

using SharedKernel;
#pragma warning disable IDE0007 // Use var instead of explicit type
#pragma warning disable IDE0008 // Use explicit type instead of var

namespace OpenIdentityStack.Infrastructure.Tests.Persistence.Settings;

/// <summary>
/// Tests for SetDefaultProviderUseCase.
/// </summary>
public sealed class SetDefaultProviderUseCaseTests
{
    private readonly IAuthenticationSettingsRepository _settingsRepository;
    private readonly IUpstreamProviderRepository _providerRepository;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAuditLog _auditLog;
    private readonly SetDefaultProviderUseCase _useCase;
    private readonly DateTimeOffset _now = new(2026, 2, 3, 12, 0, 0, TimeSpan.Zero);

    public SetDefaultProviderUseCaseTests()
    {
        this._settingsRepository = Substitute.For<IAuthenticationSettingsRepository>();
        this._providerRepository = Substitute.For<IUpstreamProviderRepository>();
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._auditLog = Substitute.For<IAuditLog>();
        this._dateTimeProvider.UtcNow.Returns(this._now);

        this._useCase = new SetDefaultProviderUseCase(
            this._settingsRepository,
            this._providerRepository,
            this._dateTimeProvider,
            this._auditLog);
    }

    #region Success Cases

    [Fact]
    public async Task ExecuteAsync_SetToLocal_ReturnsSuccess()
    {
        // Arrange
        AuthenticationSettings settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        this._settingsRepository.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(settings);

        var command = new SetDefaultProviderCommand("local");

        // Act
        Result<SetDefaultProviderResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.IsLocalDefault.ShouldBeTrue();
        result.Value.DefaultProviderId.ShouldBe(AuthenticationSettings.LocalProviderId);
    }

    [Fact]
    public async Task ExecuteAsync_SetToLocalCaseInsensitive_ReturnsSuccess()
    {
        // Arrange
        AuthenticationSettings settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        this._settingsRepository.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(settings);

        var command = new SetDefaultProviderCommand("LOCAL");

        // Act
        Result<SetDefaultProviderResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.IsLocalDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_SetToExternalProvider_ReturnsSuccess()
    {
        // Arrange
        AuthenticationSettings settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        this._settingsRepository.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(settings);

        var providerId = UpstreamProviderId.Create();
        UpstreamProvider provider = CreateTestProvider(providerId, isActive: true);

        this._providerRepository.GetByIdAsync(providerId, Arg.Any<CancellationToken>())
            .Returns(provider);

        var command = new SetDefaultProviderCommand(providerId.Value.ToString());

        // Act
        Result<SetDefaultProviderResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.IsLocalDefault.ShouldBeFalse();
        result.Value.DefaultProviderId.ShouldBe(providerId.Value.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_SavesChanges()
    {
        // Arrange
        AuthenticationSettings settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        this._settingsRepository.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(settings);

        var command = new SetDefaultProviderCommand("local");

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

        var command = new SetDefaultProviderCommand("local");

        // Act
        await this._useCase.ExecuteAsync(command);

        // Assert
        await this._auditLog.Received(1).LogChangeAsync(
            "system",
            "SetDefaultProvider",
            "AuthenticationSettings",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Failure Cases

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ExecuteAsync_WithEmptyProviderId_ReturnsError(string? providerId)
    {
        // Arrange
        var command = new SetDefaultProviderCommand(providerId!);

        // Act
        Result<SetDefaultProviderResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthenticationSettingsErrors.InvalidProviderId);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidGuid_ReturnsError()
    {
        // Arrange
        AuthenticationSettings settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        this._settingsRepository.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(settings);

        var command = new SetDefaultProviderCommand("not-a-guid");

        // Act
        Result<SetDefaultProviderResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthenticationSettingsErrors.InvalidProviderId);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentProvider_ReturnsError()
    {
        // Arrange
        AuthenticationSettings settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        this._settingsRepository.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(settings);

        var providerId = UpstreamProviderId.Create();
        this._providerRepository.GetByIdAsync(providerId, Arg.Any<CancellationToken>())
            .Returns((UpstreamProvider?)null);

        var command = new SetDefaultProviderCommand(providerId.Value.ToString());

        // Act
        Result<SetDefaultProviderResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthenticationSettingsErrors.ProviderNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_WithDisabledProvider_ReturnsError()
    {
        // Arrange
        AuthenticationSettings settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        this._settingsRepository.GetOrCreateAsync(Arg.Any<CancellationToken>())
            .Returns(settings);

        var providerId = UpstreamProviderId.Create();
        UpstreamProvider provider = CreateTestProvider(providerId, isActive: false);

        this._providerRepository.GetByIdAsync(providerId, Arg.Any<CancellationToken>())
            .Returns(provider);

        var command = new SetDefaultProviderCommand(providerId.Value.ToString());

        // Act
        Result<SetDefaultProviderResult> result = await this._useCase.ExecuteAsync(command);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthenticationSettingsErrors.ProviderDisabled);
    }

    #endregion

    #region Constructor Validation

    [Fact]
    public void Constructor_ThrowsForNullSettingsRepository()
    {
        Should.Throw<ArgumentNullException>(() =>
            new SetDefaultProviderUseCase(null!, this._providerRepository, this._dateTimeProvider, this._auditLog));
    }

    [Fact]
    public void Constructor_ThrowsForNullProviderRepository()
    {
        Should.Throw<ArgumentNullException>(() =>
            new SetDefaultProviderUseCase(this._settingsRepository, null!, this._dateTimeProvider, this._auditLog));
    }

    [Fact]
    public void Constructor_ThrowsForNullDateTimeProvider()
    {
        Should.Throw<ArgumentNullException>(() =>
            new SetDefaultProviderUseCase(this._settingsRepository, this._providerRepository, null!, this._auditLog));
    }

    [Fact]
    public void Constructor_ThrowsForNullAuditLog()
    {
        Should.Throw<ArgumentNullException>(() =>
            new SetDefaultProviderUseCase(this._settingsRepository, this._providerRepository, this._dateTimeProvider, null!));
    }

    #endregion

    private static UpstreamProvider CreateTestProvider(UpstreamProviderId id, bool isActive)
    {
        Result<UpstreamProvider> result = UpstreamProvider.Create(
            "test-provider",
            "Test Provider",
            "https://example.com",
            "client-id");

        result.IsSuccess.ShouldBeTrue();
        UpstreamProvider provider = result.Value;

        // Use reflection to set the ID since we can't construct directly
        var idProperty = typeof(UpstreamProvider).GetProperty("Id");
        idProperty!.SetValue(provider, id);

        if (!isActive)
        {
            provider.Disable();
        }

        return provider;
    }
}
