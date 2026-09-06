using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Users;
using OpenIdentityStack.Infrastructure.Tests.Common;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Tests.Users;

/// <summary>
/// Integration tests for UserRepository.
/// </summary>
public sealed class UserRepositoryTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture _fixture;
    private OpenIdentityStackDbContext _dbContext = null!;
    private UserRepository _repository = null!;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly DateTimeOffset _now = new(2026, 1, 18, 12, 0, 0, TimeSpan.Zero);

    public UserRepositoryTests(SqliteTestFixture fixture)
    {
        this._fixture = fixture;
        IDateTimeProvider mockProvider = Substitute.For<IDateTimeProvider>();
        mockProvider.UtcNow.Returns(this._now);
        this._dateTimeProvider = mockProvider;
    }

    public async ValueTask InitializeAsync()
    {
        await this._fixture.ClearAllDataAsync();
        this._dbContext = this._fixture.CreateDbContext();
        this._repository = new UserRepository(this._dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await this._dbContext.DisposeAsync();
    }

    #region AddAsync Tests

    [Fact]
    public async Task UpdateAsync_StaleProfileCannotOverwriteCommittedDisablement()
    {
        User user = this.CreateUser("concurrent@example.com", "Before", "hashed_password");
        user.VerifyEmail(this._dateTimeProvider).IsSuccess.ShouldBeTrue();
        await this._repository.AddAsync(user);
        await this._repository.SaveChangesAsync();

        await using OpenIdentityStackDbContext administratorContext = this._fixture.CreateDbContext();
        var administratorRepository = new UserRepository(administratorContext);
        User administratorUser = (await administratorRepository.GetByIdAsync(user.Id))!;
        administratorUser.Disable("Administrative disablement", this._dateTimeProvider).IsSuccess.ShouldBeTrue();
        await administratorRepository.UpdateAsync(administratorUser);
        await administratorRepository.SaveChangesAsync();

        user.UpdateDisplayName("Stale upstream profile", this._dateTimeProvider).IsSuccess.ShouldBeTrue();
        await this._repository.UpdateAsync(user);
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => this._repository.SaveChangesAsync());

        await using OpenIdentityStackDbContext verificationContext = this._fixture.CreateDbContext();
        User persisted = (await new UserRepository(verificationContext).GetByIdAsync(user.Id))!;
        persisted.Status.ShouldBe(UserStatus.Disabled);
        persisted.DisplayName.ShouldBe("Before");
    }

    [Fact]
    public async Task IdentityInventory_RoundTripsEvidenceAndPaginatesWithoutLosingQuarantinedRecords()
    {
        var providerId = OpenIdentityStack.Domain.Federation.UpstreamProviderId.Create();
        User proven = User.ProvisionFederated("proven@example.com", "Proven", providerId, "provider", "proven", "https://issuer.example").Value;
        User unproven = User.CreateFederated("unproven@example.com", "Unproven", providerId, "provider", "unproven", issuer: "https://issuer.example").Value;
        await this._repository.AddAsync(proven);
        await this._repository.AddAsync(unproven);
        await this._repository.SaveChangesAsync();
        this._dbContext.ChangeTracker.Clear();
        User reloaded = (await this._repository.GetByIdAsync(proven.Id))!;
        reloaded.UpstreamIdentities.Single().IsQuarantined.ShouldBeFalse();
        User legacy = (await this._repository.GetByIdAsync(unproven.Id))!;
        legacy.UpstreamIdentities.Single().IsQuarantined.ShouldBeTrue();
        (IReadOnlyList<User> first, int count) = await this._repository.ListWithUpstreamIdentitiesAsync(1, 1, providerId);
        (IReadOnlyList<User> second, _) = await this._repository.ListWithUpstreamIdentitiesAsync(2, 1, providerId);
        (IReadOnlyList<User> repeated, _) = await this._repository.ListWithUpstreamIdentitiesAsync(1, 1, providerId);
        count.ShouldBe(2);
        first.Single().Id.ShouldNotBe(second.Single().Id);
        repeated.Single().Id.ShouldBe(first.Single().Id);
        (await this._repository.FindByUpstreamIdentityAsync(providerId, "unproven"))!.Id.ShouldBe(unproven.Id);
    }
    [Fact]
    public async Task AddAsync_AddsUserToDatabase()
    {
        // Arrange
        User user = this.CreateUser("test@example.com", "Test User", "hashed_password");

        // Act
        await this._repository.AddAsync(user);
        await this._repository.SaveChangesAsync();

        // Assert
        User? savedUser = await this._dbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        savedUser.ShouldNotBeNull();
        savedUser.Email.ShouldBe("test@example.com");
    }

    [Fact]
    public async Task AddAsync_ThrowsOnNullUser()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() => this._repository.AddAsync(null!));
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ReturnsUserWhenExists()
    {
        // Arrange
        User user = await this.SeedUserAsync("test@example.com", "Test User", "hashed_password");

        // Act
        User? result = await this._repository.GetByIdAsync(user.Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(user.Id);
        result.Email.ShouldBe("test@example.com");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNullWhenNotExists()
    {
        // Arrange
        var nonExistentId = UserId.Create();

        // Act
        User? result = await this._repository.GetByIdAsync(nonExistentId);

        // Assert
        result.ShouldBeNull();
    }

    #endregion

    #region GetByIdsAsync Tests

    [Fact]
    public async Task GetByIdsAsync_ReturnsOnlyRequestedUsers()
    {
        // Arrange
        User user1 = await this.SeedUserAsync("user1@example.com", "User One", "hash1");
        User user2 = await this.SeedUserAsync("user2@example.com", "User Two", "hash2");
        await this.SeedUserAsync("user3@example.com", "User Three", "hash3");

        // Act
        IReadOnlyList<User> result = await this._repository.GetByIdsAsync([user1.Id, user2.Id]);

        // Assert
        result.Count.ShouldBe(2);
        result.Select(u => u.Id).ShouldBe([user1.Id, user2.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task GetByIdsAsync_SkipsMissingIds()
    {
        // Arrange
        User user1 = await this.SeedUserAsync("user1@example.com", "User One", "hash1");
        var missingId = UserId.Create();

        // Act
        IReadOnlyList<User> result = await this._repository.GetByIdsAsync([user1.Id, missingId]);

        // Assert
        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe(user1.Id);
    }

    [Fact]
    public async Task GetByIdsAsync_EmptyInput_ReturnsEmpty()
    {
        // Act
        IReadOnlyList<User> result = await this._repository.GetByIdsAsync([]);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByIdsAsync_DeduplicatesRepeatedIds()
    {
        // Arrange
        User user1 = await this.SeedUserAsync("user1@example.com", "User One", "hash1");

        // Act
        IReadOnlyList<User> result = await this._repository.GetByIdsAsync([user1.Id, user1.Id]);

        // Assert
        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe(user1.Id);
    }

    #endregion

    #region GetByEmailAsync Tests

    [Fact]
    public async Task GetByEmailAsync_ReturnsUserWhenExists()
    {
        // Arrange
        await this.SeedUserAsync("test@example.com", "Test User", "hashed_password");

        // Act
        User? result = await this._repository.GetByEmailAsync("test@example.com");

        // Assert
        result.ShouldNotBeNull();
        result.Email.ShouldBe("test@example.com");
    }

    [Fact]
    public async Task GetByEmailAsync_IsCaseInsensitive()
    {
        // Arrange
        await this.SeedUserAsync("test@example.com", "Test User", "hashed_password");

        // Act
        User? result = await this._repository.GetByEmailAsync("TEST@EXAMPLE.COM");

        // Assert
        result.ShouldNotBeNull();
        result.Email.ShouldBe("test@example.com");
    }

    [Fact]
    public async Task GetByEmailAsync_ReturnsNullWhenNotExists()
    {
        // Act
        User? result = await this._repository.GetByEmailAsync("notfound@example.com");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByEmailAsync_ReturnsNullForEmptyEmail()
    {
        // Act
        User? result = await this._repository.GetByEmailAsync("");

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByEmailAsync_ReturnsNullForWhitespaceEmail()
    {
        // Act
        User? result = await this._repository.GetByEmailAsync("   ");

        // Assert
        result.ShouldBeNull();
    }

    #endregion

    #region ExistsByEmailAsync Tests

    [Fact]
    public async Task ExistsByEmailAsync_ReturnsTrueWhenExists()
    {
        // Arrange
        await this.SeedUserAsync("test@example.com", "Test User", "hashed_password");

        // Act
        bool result = await this._repository.ExistsByEmailAsync("test@example.com");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsByEmailAsync_IsCaseInsensitive()
    {
        // Arrange
        await this.SeedUserAsync("test@example.com", "Test User", "hashed_password");

        // Act
        bool result = await this._repository.ExistsByEmailAsync("TEST@EXAMPLE.COM");

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsByEmailAsync_ReturnsFalseWhenNotExists()
    {
        // Act
        bool result = await this._repository.ExistsByEmailAsync("notfound@example.com");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsByEmailAsync_ReturnsFalseForEmptyEmail()
    {
        // Act
        bool result = await this._repository.ExistsByEmailAsync("");

        // Assert
        result.ShouldBeFalse();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_UpdatesUser()
    {
        // Arrange
        User user = await this.SeedUserAsync("test@example.com", "Test User", "hashed_password");
        user.UpdateDisplayName("Updated Name", this._dateTimeProvider);

        // Act
        await this._repository.UpdateAsync(user);
        await this._repository.SaveChangesAsync();

        // Assert
        User? updatedUser = await this._dbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        updatedUser.ShouldNotBeNull();
        updatedUser.DisplayName.ShouldBe("Updated Name");
    }

    [Fact]
    public async Task UpdateAsync_ThrowsOnNullUser()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(() => this._repository.UpdateAsync(null!));
    }

    #endregion

    #region SaveChangesAsync Tests

    [Fact]
    public async Task SaveChangesAsync_ReturnsNumberOfChanges()
    {
        // Arrange
        User user1 = this.CreateUser("user1@example.com", "User 1", "password1");
        User user2 = this.CreateUser("user2@example.com", "User 2", "password2");
        await this._repository.AddAsync(user1);
        await this._repository.AddAsync(user2);

        // Act
        int result = await this._repository.SaveChangesAsync();

        // Assert
        result.ShouldBe(2);
    }

    #endregion

    #region Helper Methods

    private User CreateUser(string email, string displayName, string passwordHash)
    {
        return User.CreateLocal(email, displayName, passwordHash, this._dateTimeProvider).Value;
    }

    private async Task<User> SeedUserAsync(string email, string displayName, string passwordHash)
    {
        User user = this.CreateUser(email, displayName, passwordHash);
        await this._repository.AddAsync(user);
        await this._repository.SaveChangesAsync();
        return user;
    }

    #endregion
}
