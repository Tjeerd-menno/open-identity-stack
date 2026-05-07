using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Settings;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Settings;

/// <summary>
/// Tests for AuthenticationSettings entity.
/// </summary>
public sealed class AuthenticationSettingsTests
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly DateTimeOffset _now = new(2026, 1, 18, 12, 0, 0, TimeSpan.Zero);

    public AuthenticationSettingsTests()
    {
        this._dateTimeProvider = Substitute.For<IDateTimeProvider>();
        this._dateTimeProvider.UtcNow.Returns(this._now);
    }

    #region CreateDefault Tests

    [Fact]
    public void CreateDefault_ReturnsSettings_WithLocalAsDefault()
    {
        // Act
        var settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);

        // Assert
        settings.DefaultProviderId.ShouldBe(AuthenticationSettings.LocalProviderId);
        settings.IsLocalDefault.ShouldBeTrue();
        settings.LocalFallbackEnabled.ShouldBeTrue();
        settings.CreatedAt.ShouldBe(this._now);
        settings.UpdatedAt.ShouldBe(this._now);
    }

    #endregion

    #region SetDefaultToLocal Tests

    [Fact]
    public void SetDefaultToLocal_SetsLocalAsDefault()
    {
        // Arrange
        var settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        var providerId = UpstreamProviderId.Create();
        settings.SetDefaultProvider(providerId, this._dateTimeProvider);

        IDateTimeProvider laterProvider = Substitute.For<IDateTimeProvider>();
        laterProvider.UtcNow.Returns(this._now.AddMinutes(5));

        // Act
        Result result = settings.SetDefaultToLocal(laterProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        settings.DefaultProviderId.ShouldBe(AuthenticationSettings.LocalProviderId);
        settings.IsLocalDefault.ShouldBeTrue();
        settings.UpdatedAt.ShouldBe(this._now.AddMinutes(5));
    }

    #endregion

    #region SetDefaultProvider Tests

    [Fact]
    public void SetDefaultProvider_WithValidProviderId_SetsExternalDefault()
    {
        // Arrange
        var settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        var providerId = UpstreamProviderId.Create();

        IDateTimeProvider laterProvider = Substitute.For<IDateTimeProvider>();
        laterProvider.UtcNow.Returns(this._now.AddMinutes(5));

        // Act
        Result result = settings.SetDefaultProvider(providerId, laterProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        settings.DefaultProviderId.ShouldBe(providerId.Value.ToString());
        settings.IsLocalDefault.ShouldBeFalse();
        settings.UpdatedAt.ShouldBe(this._now.AddMinutes(5));
    }

    [Fact]
    public void SetDefaultProvider_WithEmptyProviderId_ReturnsError()
    {
        // Arrange
        var settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        var emptyProviderId = new UpstreamProviderId(Guid.Empty);

        // Act
        Result result = settings.SetDefaultProvider(emptyProviderId, this._dateTimeProvider);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AuthenticationSettingsErrors.InvalidProviderId);
    }

    #endregion

    #region LocalFallback Tests

    [Fact]
    public void EnableLocalFallback_SetsLocalFallbackEnabled()
    {
        // Arrange
        var settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        settings.DisableLocalFallback(this._dateTimeProvider);

        IDateTimeProvider laterProvider = Substitute.For<IDateTimeProvider>();
        laterProvider.UtcNow.Returns(this._now.AddMinutes(5));

        // Act
        Result result = settings.EnableLocalFallback(laterProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        settings.LocalFallbackEnabled.ShouldBeTrue();
        settings.UpdatedAt.ShouldBe(this._now.AddMinutes(5));
    }

    [Fact]
    public void DisableLocalFallback_SetsLocalFallbackDisabled()
    {
        // Arrange
        var settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);

        IDateTimeProvider laterProvider = Substitute.For<IDateTimeProvider>();
        laterProvider.UtcNow.Returns(this._now.AddMinutes(5));

        // Act
        Result result = settings.DisableLocalFallback(laterProvider);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        settings.LocalFallbackEnabled.ShouldBeFalse();
        settings.UpdatedAt.ShouldBe(this._now.AddMinutes(5));
    }

    #endregion

    #region CanAuthenticateLocally Tests

    [Fact]
    public void CanAuthenticateLocally_WhenLocalIsDefault_ReturnsTrue_ForAnyUser()
    {
        // Arrange
        var settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);

        // Act & Assert - both admin and non-admin can authenticate locally
        settings.CanAuthenticateLocally(isIamAdmin: true).ShouldBeTrue();
        settings.CanAuthenticateLocally(isIamAdmin: false).ShouldBeTrue();
    }

    [Fact]
    public void CanAuthenticateLocally_WhenExternalIsDefault_AndFallbackEnabled_ReturnsTrue_ForAdmin()
    {
        // Arrange
        var settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        var providerId = UpstreamProviderId.Create();
        settings.SetDefaultProvider(providerId, this._dateTimeProvider);
        // LocalFallbackEnabled is true by default

        // Act & Assert
        settings.CanAuthenticateLocally(isIamAdmin: true).ShouldBeTrue();
    }

    [Fact]
    public void CanAuthenticateLocally_WhenExternalIsDefault_AndFallbackEnabled_ReturnsFalse_ForNonAdmin()
    {
        // Arrange
        var settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        var providerId = UpstreamProviderId.Create();
        settings.SetDefaultProvider(providerId, this._dateTimeProvider);
        // LocalFallbackEnabled is true by default

        // Act & Assert
        settings.CanAuthenticateLocally(isIamAdmin: false).ShouldBeFalse();
    }

    [Fact]
    public void CanAuthenticateLocally_WhenExternalIsDefault_AndFallbackDisabled_ReturnsFalse_ForAll()
    {
        // Arrange
        var settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        var providerId = UpstreamProviderId.Create();
        settings.SetDefaultProvider(providerId, this._dateTimeProvider);
        settings.DisableLocalFallback(this._dateTimeProvider);

        // Act & Assert - neither admin nor non-admin can authenticate locally
        settings.CanAuthenticateLocally(isIamAdmin: true).ShouldBeFalse();
        settings.CanAuthenticateLocally(isIamAdmin: false).ShouldBeFalse();
    }

    #endregion
}
