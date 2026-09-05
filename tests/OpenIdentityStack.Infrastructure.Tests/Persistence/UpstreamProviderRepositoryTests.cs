using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Federation;
using OpenIdentityStack.Infrastructure.Tests.Common;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class UpstreamProviderRepositoryTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture _fixture;
    private OpenIdentityStackDbContext _dbContext = null!;
    private UpstreamProviderRepository _repository = null!;

    public UpstreamProviderRepositoryTests(SqliteTestFixture fixture)
    {
        this._fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        await this._fixture.ClearAllDataAsync();
        this._dbContext = this._fixture.CreateDbContext();
        this._repository = new UpstreamProviderRepository(this._dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await this._dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Migration_LocksLegacyProvidersWithoutBackfillingIssuerEvidence()
    {
        UpstreamProvider provider = await this.SeedAsync("legacy-provider", "Legacy Provider");
        OpenIdentityStack.Domain.Users.User user = OpenIdentityStack.Domain.Users.User.CreateFederated("legacy@example.com", "Legacy", provider.Id, provider.Name, "subject").Value;
        this._dbContext.Users.Add(user);
        await this._dbContext.SaveChangesAsync();
        await this._dbContext.Database.ExecuteSqlRawAsync("UPDATE upstream_providers SET identity_configuration_locked = FALSE");
        var migration = new OpenIdentityStack.Infrastructure.Persistence.Migrations.BindFederationIssuers();
        foreach (Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation operation in migration.UpOperations.OfType<Microsoft.EntityFrameworkCore.Migrations.Operations.SqlOperation>())
        {
            await this._dbContext.Database.ExecuteSqlRawAsync(operation.Sql);
        }

        this._dbContext.ChangeTracker.Clear();
        UpstreamProvider reloaded = (await this._repository.GetByIdAsync(provider.Id))!;
        reloaded.IdentityConfigurationLocked.ShouldBeTrue();
        reloaded.BoundIssuer.ShouldBeNull();
        OpenIdentityStack.Domain.Users.User reloadedUser = (await this._dbContext.Users.FindAsync(user.Id))!;
        reloadedUser.UpstreamIdentities.Single().Issuer.ShouldBeNull();
    }
    [Fact]
    public async Task FirstLink_RejectsConcurrentStaleAuthorityUpdate()
    {
        UpstreamProvider provider = await this.SeedAsync("racing-provider", "Racing Provider");
        await using OpenIdentityStackDbContext staleContext = this._fixture.CreateDbContext();
        UpstreamProvider staleProvider = (await staleContext.UpstreamProviders.FindAsync(provider.Id))!;
        staleProvider.UpdateAuthority("https://replacement.example.com").IsSuccess.ShouldBeTrue();
        OpenIdentityStack.Domain.Users.User user = OpenIdentityStack.Domain.Users.User.CreateFederated("race@example.com", "Race", provider.Id, provider.Name, "subject").Value;
        this._dbContext.Users.Add(user);
        await this._dbContext.SaveChangesAsync();
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => staleContext.SaveChangesAsync());
    }

    [Fact]
    public async Task AuthorityUpdate_RejectsConcurrentFirstLinkAndRollsBackUserInsertion()
    {
        UpstreamProvider provider = await this.SeedAsync("reverse-race", "Reverse Race");
        await using OpenIdentityStackDbContext linkingContext = this._fixture.CreateDbContext();
        UpstreamProvider linkingProvider = (await linkingContext.UpstreamProviders.FindAsync(provider.Id))!;
        linkingProvider.BindIssuer("https://issuer.example/", linkingProvider.Authority).IsSuccess.ShouldBeTrue();
        OpenIdentityStack.Domain.Users.User user = OpenIdentityStack.Domain.Users.User.CreateFederated("reverse@example.com", "Race", provider.Id, provider.Name, "subject", issuer: "https://issuer.example/").Value;
        linkingContext.Users.Add(user);
        provider.UpdateAuthority("https://replacement.example.com").IsSuccess.ShouldBeTrue();
        await this._dbContext.SaveChangesAsync();
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => linkingContext.SaveChangesAsync());
        this._dbContext.ChangeTracker.Clear();
        (await this._dbContext.Users.FindAsync(user.Id)).ShouldBeNull();
    }
    [Fact]
    public async Task LinkedProvider_AuthorityCannotChangeAfterReload()
    {
        UpstreamProvider provider = await this.SeedAsync("linked-provider", "Linked Provider");
        OpenIdentityStack.Domain.Users.User user = OpenIdentityStack.Domain.Users.User.CreateFederated("bound@example.com", "Bound user", provider.Id, provider.Name, "subject").Value;
        this._dbContext.Users.Add(user);
        await this._dbContext.SaveChangesAsync();
        this._dbContext.ChangeTracker.Clear();
        UpstreamProvider reloaded = (await this._repository.GetByIdAsync(provider.Id))!;
        reloaded.UpdateAuthority("https://replacement.example.com").IsFailure.ShouldBeTrue();
        reloaded.UpdateDisplayName("Renamed provider").IsSuccess.ShouldBeTrue();
    }
    [Fact]
    public async Task GetByIdAsync_ReturnsProvider_WhenExists()
    {
        UpstreamProvider provider = await this.SeedAsync("test-provider", "Test Provider");

        UpstreamProvider? result = await this._repository.GetByIdAsync(provider.Id);

        result.ShouldNotBeNull();
        result!.Name.ShouldBe("test-provider");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        UpstreamProvider? result = await this._repository.GetByIdAsync(UpstreamProviderId.Create());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsProvider_WhenExists()
    {
        await this.SeedAsync("test-provider", "Test Provider");

        UpstreamProvider? result = await this._repository.GetByNameAsync("test-provider");

        result.ShouldNotBeNull();
        result!.Name.ShouldBe("test-provider");
    }

    [Fact]
    public async Task GetByNameAsync_IsNormalized()
    {
        await this.SeedAsync("test-provider", "Test Provider");

        UpstreamProvider? result = await this._repository.GetByNameAsync("TEST-PROVIDER");

        result.ShouldNotBeNull();
        result!.Name.ShouldBe("test-provider");
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsNull_WhenNotExists()
    {
        UpstreamProvider? result = await this._repository.GetByNameAsync("missing");

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetActiveProvidersAsync_ReturnsOnlyActive()
    {
        await this.SeedAsync("active-provider", "Active Provider");
        UpstreamProvider inactive = await this.SeedAsync("inactive-provider", "Inactive Provider");
        inactive.Disable();
        await this._repository.SaveChangesAsync();

        IReadOnlyList<UpstreamProvider> result = await this._repository.GetActiveProvidersAsync();

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("active-provider");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllProviders()
    {
        await this.SeedAsync("provider1", "Provider 1");
        await this.SeedAsync("provider2", "Provider 2");
        UpstreamProvider inactive = await this.SeedAsync("provider3", "Provider 3");
        inactive.Disable();
        await this._repository.SaveChangesAsync();

        IReadOnlyList<UpstreamProvider> result = await this._repository.GetAllAsync();

        result.Count.ShouldBe(3);
    }

    [Fact]
    public async Task AddAsync_AddsProvider()
    {
        UpstreamProvider provider = CreateProvider("new-provider", "New Provider");

        await this._repository.AddAsync(provider);
        await this._repository.SaveChangesAsync();

        UpstreamProvider? saved = await this._dbContext.UpstreamProviders.FirstOrDefaultAsync(p => p.Name == "new-provider");
        saved.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetActiveProvidersAsync_OrdersByDisplayName()
    {
        await this.SeedAsync("zulu-provider", "Zulu Provider");
        await this.SeedAsync("alpha-provider", "Alpha Provider");

        IReadOnlyList<UpstreamProvider> result = await this._repository.GetActiveProvidersAsync();

        result[0].DisplayName.ShouldBe("Alpha Provider");
        result[1].DisplayName.ShouldBe("Zulu Provider");
    }

    [Fact]
    public async Task GetAllAsync_OrdersByDisplayName()
    {
        await this.SeedAsync("zulu-provider", "Zulu Provider");
        await this.SeedAsync("alpha-provider", "Alpha Provider");

        IReadOnlyList<UpstreamProvider> result = await this._repository.GetAllAsync();

        result[0].DisplayName.ShouldBe("Alpha Provider");
        result[1].DisplayName.ShouldBe("Zulu Provider");
    }

    private static UpstreamProvider CreateProvider(string name, string displayName)
    {
        return UpstreamProvider.Create(
            name,
            displayName,
            "https://example.com",
            "client-id").Value;
    }

    private async Task<UpstreamProvider> SeedAsync(string name, string displayName)
    {
        UpstreamProvider provider = CreateProvider(name, displayName);
        await this._repository.AddAsync(provider);
        await this._repository.SaveChangesAsync();
        return provider;
    }
}
