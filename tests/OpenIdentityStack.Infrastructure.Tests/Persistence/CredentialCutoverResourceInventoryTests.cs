using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OpenIdentityStack.Application.AdministrativeAccess;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.Resources;
using OpenIdentityStack.Infrastructure.Persistence;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class CredentialCutoverResourceInventoryTests
{
    [Fact]
    public async Task RequiresPreparedManagementClientCurrentGrantAndMappedBusinessScopes()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        DbContextOptions<OpenIdentityStackDbContext> options = new DbContextOptionsBuilder<OpenIdentityStackDbContext>().UseSqlite(connection).UseOpenIddict().Options;
        await using var db = new OpenIdentityStackDbContext(options);
        await db.Database.EnsureCreatedAsync();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenIddict:Clients:ManagementWeb:RedirectUris:0"] = "https://console.example/auth/callback",
            ["OpenIddict:Clients:ManagementWeb:PostLogoutRedirectUris:0"] = "https://console.example/"
        }).Build();
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Production");
        var inventory = new CredentialCutoverResourceInventory(db, configuration, environment);
        (await inventory.ReadAsync()).Blockers.ShouldContain(x => x.Code == "Administrative.ManagementWebUnprepared");
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        DomainApplication management = DomainApplication.Create(ManagementWebPreparation.ClientId, "Management", null,
            ApplicationProfile.SinglePage, OAuthClientType.Public, ["authorization_code", "refresh_token"], ["openid", "profile", "email", "ois.admin"],
            ["https://console.example/auth/callback"], ["https://console.example/"], true, false, clock).Value;
        db.Add(ProtectedResource.CreateAdministrative());
        db.Add(management);
        await db.SaveChangesAsync();
        (await inventory.ReadAsync()).Blockers.ShouldContain(x => x.Code == "Administrative.ManagementWebUnprepared");
        ClientResourceGrant grant = ClientResourceGrant.Create(management.Id, ProtectedResource.AdministrativeResourceId, ["*"], []).Value;
        db.Add(grant);
        await db.SaveChangesAsync();
        (await inventory.ReadAsync()).Blockers.ShouldBeEmpty();
        await using var other = new OpenIdentityStackDbContext(options);
        ClientResourceGrant current = await other.ClientResourceGrants.SingleAsync();
        current.Configure([], []);
        await other.SaveChangesAsync();
        (await inventory.ReadAsync()).Blockers.ShouldContain(x => x.Code == "Administrative.ManagementWebUnprepared");
        current.Configure(["*"], []);
        await other.SaveChangesAsync();
        DomainApplication legacy = DomainApplication.Create("legacy", "Legacy", null, ApplicationProfile.MachineToMachine,
            OAuthClientType.Confidential, ["client_credentials"], ["legacy-api"], [], [], false, false, clock).Value;
        db.Add(legacy);
        await db.SaveChangesAsync();
        (await inventory.ReadAsync()).Blockers.ShouldContain(x => x.Code == "Resource.UnmappedScope");
        ProtectedResource business = ProtectedResource.Create("urn:business", "legacy-api", "Business", ["business"]).Value;
        business.Configure("Business", ["business"], false);
        db.Add(business);
        await db.SaveChangesAsync();
        OpenIdentityStack.Application.Abstractions.CutoverResourceInventory mapped = await inventory.ReadAsync();
        mapped.Blockers.ShouldBeEmpty();
        mapped.BusinessResources.Single().Id.ShouldBe(business.Id);
    }

    [Theory]
    [InlineData("openid")]
    [InlineData("profile")]
    [InlineData("email")]
    [InlineData("ois.admin")]
    public async Task MissingCanonicalManagementScopeBlocksCutover(string missingScope)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        DbContextOptions<OpenIdentityStackDbContext> options = new DbContextOptionsBuilder<OpenIdentityStackDbContext>().UseSqlite(connection).UseOpenIddict().Options;
        await using var db = new OpenIdentityStackDbContext(options);
        await db.Database.EnsureCreatedAsync();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenIddict:Clients:ManagementWeb:RedirectUris:0"] = "https://console.example/auth/callback",
            ["OpenIddict:Clients:ManagementWeb:PostLogoutRedirectUris:0"] = "https://console.example/"
        }).Build();
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Production");
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        string[] scopes = ["openid", "profile", "email", "ois.admin"];
        DomainApplication management = DomainApplication.Create(ManagementWebPreparation.ClientId, "Management", null,
            ApplicationProfile.SinglePage, OAuthClientType.Public, ["authorization_code", "refresh_token"], scopes.Where(scope => scope != missingScope).ToArray(),
            ["https://console.example/auth/callback"], ["https://console.example/"], true, false, clock).Value;
        db.Add(ProtectedResource.CreateAdministrative());
        db.Add(management);
        db.Add(ClientResourceGrant.Create(management.Id, ProtectedResource.AdministrativeResourceId, ["*"], []).Value);
        await db.SaveChangesAsync();

        var inventory = new CredentialCutoverResourceInventory(db, configuration, environment);
        (await inventory.ReadAsync()).Blockers.ShouldContain(blocker => blocker.Code == "Administrative.ManagementWebUnprepared");
    }

    [Fact]
    public async Task ManagementClientRequiringConsentBlocksCutover()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        DbContextOptions<OpenIdentityStackDbContext> options = new DbContextOptionsBuilder<OpenIdentityStackDbContext>().UseSqlite(connection).UseOpenIddict().Options;
        await using var db = new OpenIdentityStackDbContext(options);
        await db.Database.EnsureCreatedAsync();
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenIddict:Clients:ManagementWeb:RedirectUris:0"] = "https://console.example/auth/callback",
            ["OpenIddict:Clients:ManagementWeb:PostLogoutRedirectUris:0"] = "https://console.example/"
        }).Build();
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Production");
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        DomainApplication management = DomainApplication.Create(ManagementWebPreparation.ClientId, "Management", null,
            ApplicationProfile.SinglePage, OAuthClientType.Public, ["authorization_code", "refresh_token"], ["openid", "profile", "email", "ois.admin"],
            ["https://console.example/auth/callback"], ["https://console.example/"], true, true, clock).Value;
        db.Add(ProtectedResource.CreateAdministrative());
        db.Add(management);
        db.Add(ClientResourceGrant.Create(management.Id, ProtectedResource.AdministrativeResourceId, ["*"], []).Value);
        await db.SaveChangesAsync();

        var inventory = new CredentialCutoverResourceInventory(db, configuration, environment);

        (await inventory.ReadAsync()).Blockers.ShouldContain(blocker => blocker.Code == "Administrative.ManagementWebUnprepared");
    }
}
