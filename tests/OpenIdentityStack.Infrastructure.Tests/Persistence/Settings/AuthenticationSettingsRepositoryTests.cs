using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Settings;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Settings;
using OpenIdentityStack.Infrastructure.Tests.Common;

using SharedKernel;
#pragma warning disable IDE0007 // Use var instead of explicit type
#pragma warning disable IDE0008 // Use explicit type instead of var

namespace OpenIdentityStack.Infrastructure.Tests.Persistence.Settings;

public sealed class AuthenticationSettingsRepositoryTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture _fixture;
    private OpenIdentityStackDbContext _dbContext = null!;
    private AuthenticationSettingsRepository _repository = null!;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly DateTimeOffset _now = new(2026, 2, 3, 12, 0, 0, TimeSpan.Zero);

    public AuthenticationSettingsRepositoryTests(SqliteTestFixture fixture)
    {
        this._fixture = fixture;
        IDateTimeProvider provider = Substitute.For<IDateTimeProvider>();
        provider.UtcNow.Returns(this._now);
        this._dateTimeProvider = provider;
    }

    public async ValueTask InitializeAsync()
    {
        await this._fixture.ClearAllDataAsync();
        this._dbContext = this._fixture.CreateDbContext();
        this._repository = new AuthenticationSettingsRepository(this._dbContext, this._dateTimeProvider);
    }

    public async ValueTask DisposeAsync()
    {
        await this._dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenNoSettingsExist()
    {
        AuthenticationSettings? result = await this._repository.GetAsync();

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_ReturnsSettings_WhenExist()
    {
        await this.SeedSettingsAsync();

        AuthenticationSettings? result = await this._repository.GetAsync();

        result.ShouldNotBeNull();
        result!.DefaultProviderId.ShouldBe(AuthenticationSettings.LocalProviderId);
    }

    [Fact]
    public async Task GetOrCreateAsync_CreatesDefaultSettings_WhenNoneExist()
    {
        AuthenticationSettings result = await this._repository.GetOrCreateAsync();

        result.ShouldNotBeNull();
        result.DefaultProviderId.ShouldBe(AuthenticationSettings.LocalProviderId);
        result.LocalFallbackEnabled.ShouldBeTrue();
        result.IsLocalDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task GetOrCreateAsync_ReturnsExistingSettings_WhenExist()
    {
        AuthenticationSettings seeded = await this.SeedSettingsAsync();

        AuthenticationSettings result = await this._repository.GetOrCreateAsync();

        result.ShouldNotBeNull();
        result.Id.ShouldBe(seeded.Id);
    }

    [Fact]
    public async Task GetOrCreateAsync_PersistsCreatedSettings()
    {
        AuthenticationSettings result = await this._repository.GetOrCreateAsync();

        // Verify with fresh context
        await using OpenIdentityStackDbContext freshContext = this._fixture.CreateDbContext();
        AuthenticationSettings? persisted = await freshContext.AuthenticationSettings.FirstOrDefaultAsync();

        persisted.ShouldNotBeNull();
        persisted!.Id.ShouldBe(result.Id);
    }

    [Fact]
    public async Task AddAsync_PersistsSettings()
    {
        AuthenticationSettings settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);

        await this._repository.AddAsync(settings);
        await this._repository.SaveChangesAsync();

        // Verify with fresh context
        await using OpenIdentityStackDbContext freshContext = this._fixture.CreateDbContext();
        AuthenticationSettings? persisted = await freshContext.AuthenticationSettings.FirstOrDefaultAsync();

        persisted.ShouldNotBeNull();
        persisted!.DefaultProviderId.ShouldBe(AuthenticationSettings.LocalProviderId);
        persisted.LocalFallbackEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsModifications()
    {
        AuthenticationSettings settings = await this.SeedSettingsAsync();

        // Modify the settings
        settings.DisableLocalFallback(this._dateTimeProvider);
        await this._repository.SaveChangesAsync();

        // Verify with fresh context
        await using OpenIdentityStackDbContext freshContext = this._fixture.CreateDbContext();
        AuthenticationSettings? updated = await freshContext.AuthenticationSettings.FirstOrDefaultAsync();

        updated.ShouldNotBeNull();
        updated!.LocalFallbackEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task AddAsync_ThrowsForNullSettings()
    {
        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await this._repository.AddAsync(null!));
    }

    [Fact]
    public void Constructor_ThrowsForNullDbContext()
    {
        Should.Throw<ArgumentNullException>(() =>
            new AuthenticationSettingsRepository(null!, this._dateTimeProvider));
    }

    [Fact]
    public void Constructor_ThrowsForNullDateTimeProvider()
    {
        Should.Throw<ArgumentNullException>(() =>
            new AuthenticationSettingsRepository(this._dbContext, null!));
    }

    private async Task<AuthenticationSettings> SeedSettingsAsync()
    {
        AuthenticationSettings settings = AuthenticationSettings.CreateDefault(this._dateTimeProvider);
        await this._dbContext.AuthenticationSettings.AddAsync(settings);
        await this._dbContext.SaveChangesAsync();
        return settings;
    }
}
