using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Clients;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Infrastructure.Clients;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Tests.Common;

using SharedKernel;
#pragma warning disable IDE0007 // Use var instead of explicit type
#pragma warning disable IDE0008 // Use explicit type instead of var

namespace OpenIdentityStack.Infrastructure.Tests.Clients;

public sealed class ClientRepositoryTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture _fixture;
    private OpenIdentityStackDbContext _dbContext = null!;
    private ClientRepository _repository = null!;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly DateTimeOffset _now = new(2026, 2, 3, 12, 0, 0, TimeSpan.Zero);

    public ClientRepositoryTests(SqliteTestFixture fixture)
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
        this._repository = new ClientRepository(this._dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await this._dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsClient_WhenExists()
    {
        Client client = await this.SeedClientAsync("test-client", "Test Client");

        Client? result = await this._repository.GetByIdAsync(client.Id);

        result.ShouldNotBeNull();
        result!.ClientIdValue.ShouldBe("test-client");
        result.DisplayName.ShouldBe("Test Client");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        Client? result = await this._repository.GetByIdAsync(ClientId.NewId());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByClientIdAsync_ReturnsClient_WhenExists()
    {
        await this.SeedClientAsync("test-client", "Test Client");

        Client? result = await this._repository.GetByClientIdAsync("test-client");

        result.ShouldNotBeNull();
        result!.ClientIdValue.ShouldBe("test-client");
    }

    [Fact]
    public async Task GetByClientIdAsync_ReturnsNull_WhenNotExists()
    {
        Client? result = await this._repository.GetByClientIdAsync("missing-client");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ListAsync_ReturnsClients_WithPagination()
    {
        await this.SeedClientAsync("client1", "Client 1");
        await this.SeedClientAsync("client2", "Client 2");
        await this.SeedClientAsync("client3", "Client 3");

        (IReadOnlyList<Client> items, int totalCount) = await this._repository.ListAsync(1, 2);

        totalCount.ShouldBe(3);
        items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListAsync_ReturnsEmptyList_WhenNoClients()
    {
        (IReadOnlyList<Client> items, int totalCount) = await this._repository.ListAsync(1, 10);

        totalCount.ShouldBe(0);
        items.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListAsync_ReturnsCorrectPage()
    {
        await this.SeedClientAsync("client1", "Client 1");
        await this.SeedClientAsync("client2", "Client 2");
        await this.SeedClientAsync("client3", "Client 3");

        (IReadOnlyList<Client> items, int totalCount) = await this._repository.ListAsync(2, 2);

        totalCount.ShouldBe(3);
        items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AddAsync_PersistsClient()
    {
        Result<Client> clientResult = Client.Create(
            "new-client",
            "New Client",
            ClientType.Confidential,
            "A test client",
            ["https://example.com/callback"],
            ["https://example.com/logout"],
            ["openid", "profile"],
            ["authorization_code"],
            true,
            false,
            this._dateTimeProvider);

        clientResult.IsSuccess.ShouldBeTrue();
        Client client = clientResult.Value;

        await this._repository.AddAsync(client);
        await this._repository.SaveChangesAsync();

        // Verify with fresh context
        await using OpenIdentityStackDbContext freshContext = this._fixture.CreateDbContext();
        Client? persisted = await freshContext.Clients.FirstOrDefaultAsync(c => c.Id == client.Id);

        persisted.ShouldNotBeNull();
        persisted!.ClientIdValue.ShouldBe("new-client");
        persisted.DisplayName.ShouldBe("New Client");
        persisted.ClientType.ShouldBe(ClientType.Confidential);
        persisted.Description.ShouldBe("A test client");
        persisted.RedirectUris.ShouldContain("https://example.com/callback");
        persisted.PostLogoutRedirectUris.ShouldContain("https://example.com/logout");
        persisted.AllowedScopes.ShouldContain("openid");
        persisted.AllowedGrantTypes.ShouldContain("authorization_code");
        persisted.RequirePkce.ShouldBeTrue();
        persisted.RequireConsent.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_RemovesClient()
    {
        Client client = await this.SeedClientAsync("delete-me", "Delete Me");

        await this._repository.DeleteAsync(client);
        await this._repository.SaveChangesAsync();

        // Verify with fresh context
        await using OpenIdentityStackDbContext freshContext = this._fixture.CreateDbContext();
        Client? deleted = await freshContext.Clients.FirstOrDefaultAsync(c => c.Id == client.Id);

        deleted.ShouldBeNull();
    }

    [Fact]
    public async Task ListAsync_OrdersByCreatedAtDescending()
    {
        // Create clients with different timestamps by adjusting the mock
        IDateTimeProvider earlier = Substitute.For<IDateTimeProvider>();
        earlier.UtcNow.Returns(this._now.AddHours(-2));
        IDateTimeProvider middle = Substitute.For<IDateTimeProvider>();
        middle.UtcNow.Returns(this._now.AddHours(-1));
        IDateTimeProvider latest = Substitute.For<IDateTimeProvider>();
        latest.UtcNow.Returns(this._now);

        await this.SeedClientAsync("client-old", "Old Client", earlier);
        await this.SeedClientAsync("client-mid", "Middle Client", middle);
        await this.SeedClientAsync("client-new", "New Client", latest);

        (IReadOnlyList<Client> items, int _) = await this._repository.ListAsync(1, 10);

        items[0].ClientIdValue.ShouldBe("client-new");
        items[1].ClientIdValue.ShouldBe("client-mid");
        items[2].ClientIdValue.ShouldBe("client-old");
    }

    [Fact]
    public async Task AddAsync_PersistsPublicClient()
    {
        Result<Client> clientResult = Client.Create(
            "public-client",
            "Public Client",
            ClientType.Public,
            null,
            ["https://example.com/callback"],
            [],
            ["openid"],
            ["authorization_code"],
            true,
            false,
            this._dateTimeProvider);

        clientResult.IsSuccess.ShouldBeTrue();
        await this._repository.AddAsync(clientResult.Value);
        await this._repository.SaveChangesAsync();

        await using OpenIdentityStackDbContext freshContext = this._fixture.CreateDbContext();
        Client? persisted = await freshContext.Clients.FirstOrDefaultAsync(c => c.Id == clientResult.Value.Id);

        persisted.ShouldNotBeNull();
        persisted!.ClientType.ShouldBe(ClientType.Public);
    }

    private async Task<Client> SeedClientAsync(string clientId, string displayName, IDateTimeProvider? dateTimeProvider = null)
    {
        Result<Client> result = Client.Create(
            clientId,
            displayName,
            ClientType.Confidential,
            null,
            ["https://example.com/callback"],
            [],
            ["openid", "profile"],
            ["authorization_code"],
            true,
            false,
            dateTimeProvider ?? this._dateTimeProvider);

        result.IsSuccess.ShouldBeTrue();
        Client client = result.Value;

        await this._dbContext.Clients.AddAsync(client);
        await this._dbContext.SaveChangesAsync();

        return client;
    }
}
