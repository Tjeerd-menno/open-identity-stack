using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Settings.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Settings;
using OpenIdentityStack.Infrastructure.Persistence.Settings;

using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence.Settings;

public sealed class GetAuthenticationSettingsQueryHandlerTests
{
    private readonly IAuthenticationSettingsRepository settingsRepository;
    private readonly GetAuthenticationSettingsQueryHandler handler;

    public GetAuthenticationSettingsQueryHandlerTests()
    {
        this.settingsRepository = Substitute.For<IAuthenticationSettingsRepository>();
        this.handler = new GetAuthenticationSettingsQueryHandler(this.settingsRepository);
    }

    [Fact]
    public void Constructor_WithNullSettingsRepository_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => new GetAuthenticationSettingsQueryHandler(null!));
    }

    [Fact]
    public async Task HandleAsync_WithDefaultSettings_ReturnsLocalAuthenticationSettings()
    {
        var now = new DateTimeOffset(2026, 5, 10, 7, 40, 0, TimeSpan.Zero);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(now);
        var settings = AuthenticationSettings.CreateDefault(dateTimeProvider);
        CancellationToken cancellationToken = new CancellationTokenSource().Token;

        this.settingsRepository.GetOrCreateAsync(cancellationToken).Returns(settings);

        Result<AuthenticationSettingsDto> result = await this.handler.HandleAsync(new GetAuthenticationSettingsQuery(), cancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.DefaultProviderId.ShouldBe(AuthenticationSettings.LocalProviderId);
        result.Value.IsLocalDefault.ShouldBeTrue();
        result.Value.LocalFallbackEnabled.ShouldBeTrue();
        result.Value.UpdatedAt.ShouldBe(now);
        await this.settingsRepository.Received(1).GetOrCreateAsync(cancellationToken);
    }

    [Fact]
    public async Task HandleAsync_WithExternalDefaultAndFallbackDisabled_ReturnsCurrentSettings()
    {
        var createdAt = new DateTimeOffset(2026, 5, 10, 7, 40, 0, TimeSpan.Zero);
        DateTimeOffset updatedAt = createdAt.AddMinutes(5);
        IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(createdAt);
        var settings = AuthenticationSettings.CreateDefault(dateTimeProvider);
        var providerId = UpstreamProviderId.Create();
        dateTimeProvider.UtcNow.Returns(updatedAt);
        settings.SetDefaultProvider(providerId, dateTimeProvider).IsSuccess.ShouldBeTrue();
        settings.DisableLocalFallback(dateTimeProvider).IsSuccess.ShouldBeTrue();

        this.settingsRepository.GetOrCreateAsync(Arg.Any<CancellationToken>()).Returns(settings);

        Result<AuthenticationSettingsDto> result = await this.handler.HandleAsync(new GetAuthenticationSettingsQuery());

        result.IsSuccess.ShouldBeTrue();
        result.Value.DefaultProviderId.ShouldBe(providerId.Value.ToString());
        result.Value.IsLocalDefault.ShouldBeFalse();
        result.Value.LocalFallbackEnabled.ShouldBeFalse();
        result.Value.UpdatedAt.ShouldBe(updatedAt);
    }
}
