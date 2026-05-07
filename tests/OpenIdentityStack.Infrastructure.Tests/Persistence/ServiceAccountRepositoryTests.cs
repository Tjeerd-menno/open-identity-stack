
using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.ServiceAccounts;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.ServiceAccounts;
using OpenIdentityStack.Infrastructure.Tests.Common;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class ServiceAccountRepositoryTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture _fixture;
    private OpenIdentityStackDbContext _dbContext = null!;
    private ServiceAccountRepository _repository = null!;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly DateTimeOffset _now = new(2026, 1, 18, 12, 0, 0, TimeSpan.Zero);

    public ServiceAccountRepositoryTests(SqliteTestFixture fixture)
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
        this._repository = new ServiceAccountRepository(this._dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await this._dbContext.DisposeAsync();
    }

    [Fact]
    public async Task AddAsync_AddsServiceAccountToDatabase()
    {
        ServiceAccount account = this.CreateServiceAccount("client-1");

        await this._repository.AddAsync(account);
        await this._repository.SaveChangesAsync();

        ServiceAccount? saved = await this._dbContext.ServiceAccounts.FirstOrDefaultAsync(s => s.ClientId == "client-1");
        saved.ShouldNotBeNull();
        saved!.ClientId.ShouldBe("client-1");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsAccount_WhenExists()
    {
        ServiceAccount account = await this.SeedAsync("client-2");

        ServiceAccount? result = await this._repository.GetByIdAsync(account.Id);

        result.ShouldNotBeNull();
        result!.ClientId.ShouldBe("client-2");
    }

    [Fact]
    public async Task GetByClientIdAsync_ReturnsAccount_WhenExists()
    {
        await this.SeedAsync("client-3");

        ServiceAccount? result = await this._repository.GetByClientIdAsync("client-3");

        result.ShouldNotBeNull();
        result!.ClientId.ShouldBe("client-3");
    }

    [Fact]
    public async Task ExistsByClientIdAsync_ReturnsTrue_WhenExists()
    {
        await this.SeedAsync("client-4");

        bool exists = await this._repository.ExistsByClientIdAsync("client-4");

        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsByClientIdAsync_ReturnsFalse_WhenMissing()
    {
        bool exists = await this._repository.ExistsByClientIdAsync("missing");

        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task ListAsync_FiltersByStatus_AndSearch()
    {
        ServiceAccount active = this.CreateServiceAccount("active-client");
        ServiceAccount disabled = this.CreateServiceAccount("disabled-client");
        disabled.Disable(this._dateTimeProvider);

        await this._repository.AddAsync(active);
        await this._repository.AddAsync(disabled);
        await this._repository.SaveChangesAsync();

        (List<ServiceAccount>? items, int total) = await this._repository.ListAsync(1, 10, ServiceAccountStatus.Disabled, "disabled", CancellationToken.None);

        total.ShouldBe(1);
        items.Count.ShouldBe(1);
        items[0].ClientId.ShouldBe("disabled-client");
    }

    [Fact]
    public async Task Update_UpdatesServiceAccount()
    {
        ServiceAccount account = await this.SeedAsync("client-update");
        account.UpdateDisplayName("Updated", this._dateTimeProvider);

        this._repository.Update(account);
        await this._repository.SaveChangesAsync();

        ServiceAccount? saved = await this._repository.GetByClientIdAsync("client-update");
        saved!.DisplayName.ShouldBe("Updated");
    }

    [Fact]
    public async Task Remove_RemovesServiceAccount()
    {
        ServiceAccount account = await this.SeedAsync("client-remove");

        this._repository.Remove(account);
        await this._repository.SaveChangesAsync();

        ServiceAccount? saved = await this._repository.GetByClientIdAsync("client-remove");
        saved.ShouldBeNull();
    }

    private ServiceAccount CreateServiceAccount(string clientId)
    {
        return ServiceAccount.Create(
            clientId,
            $"{clientId} display",
            new List<string> { "api" },
            new List<string> { "client_credentials" },
            this._dateTimeProvider).Value;
    }

    private async Task<ServiceAccount> SeedAsync(string clientId)
    {
        ServiceAccount account = this.CreateServiceAccount(clientId);
        await this._repository.AddAsync(account);
        await this._repository.SaveChangesAsync();
        return account;
    }
}
