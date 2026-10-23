using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIddict.Abstractions;
using OpenIdentityStack.Domain.Resources;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Resources;
using SharedKernel;
using ClientApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Infrastructure.Tests.Resources;

public sealed class ResourceAccessBatchLookupTests : IAsyncLifetime
{
    private SqliteConnection connection = null!;
    private DbContextOptions<OpenIdentityStackDbContext> options = null!;
    private ProtectedResource orders = null!;
    private ProtectedResource payroll = null!;
    private ProtectedResource billing = null!;
    private ClientApplication client = null!;

    public async ValueTask InitializeAsync()
    {
        this.connection = new SqliteConnection("DataSource=:memory:");
        await this.connection.OpenAsync();
        this.options = new DbContextOptionsBuilder<OpenIdentityStackDbContext>().UseSqlite(this.connection).Options;
        await using OpenIdentityStackDbContext db = new(this.options);
        await db.Database.EnsureCreatedAsync();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        var repository = new ResourceAccessRepository(db, Substitute.For<IOpenIddictScopeManager>(), clock);
        this.orders = ProtectedResource.Create("https://orders-batch.example.com", "orders-batch", "Orders", ["orders"]).Value;
        this.payroll = ProtectedResource.Create("https://payroll-batch.example.com", "payroll-batch", "Payroll", ["payroll"]).Value;
        this.payroll.Configure("Payroll", ["payroll"], false).IsSuccess.ShouldBeTrue();
        this.billing = ProtectedResource.Create("https://billing-batch.example.com", "billing-batch", "Billing", ["billing"]).Value;
        this.client = ClientApplication.CreateMachineToMachine("batch-client", "Batch", null, ["orders-batch", "payroll-batch"], clock).Value;
        ClientApplication other = ClientApplication.CreateMachineToMachine("other-client", "Other", null, ["billing-batch"], clock).Value;
        db.Applications.AddRange(this.client, other);
        repository.AddResource(this.orders);
        repository.AddResource(this.payroll);
        repository.AddResource(this.billing);
        repository.AddGrant(ClientResourceGrant.Create(this.client.Id, this.orders.Id, ["orders:invoice:read"], ["orders:invoice:write"]).Value);
        repository.AddGrant(ClientResourceGrant.Create(this.client.Id, this.payroll.Id, [], ["payroll:salary:read"]).Value);
        repository.AddGrant(ClientResourceGrant.Create(other.Id, this.billing.Id, ["billing:invoice:read"], []).Value);
        await repository.SaveChangesAsync("operator", "Seed", "batch");
    }

    public async ValueTask DisposeAsync()
    {
        await this.connection.DisposeAsync();
    }

    [Fact]
    public async Task FindByScopesAsync_ReturnsEveryMatchInOneUntrackedQuery()
    {
        (ResourceAccessRepository repository, OpenIdentityStackDbContext db, SelectObserver observer) = this.OpenReader();
        await using (db)
        {
            IReadOnlyList<ProtectedResource> loaded = await repository.FindByScopesAsync(["missing-batch", this.orders.Scope, this.payroll.Scope]);

            loaded.Select(resource => resource.Scope).ShouldBe([this.orders.Scope, this.payroll.Scope], ignoreOrder: true);
            loaded.Single(resource => resource.Scope == this.orders.Scope).PermissionNamespaces.ShouldBe(["orders"]);
            ProtectedResource disabled = loaded.Single(resource => resource.Scope == this.payroll.Scope);
            disabled.Enabled.ShouldBeFalse();
            disabled.PermissionNamespaces.ShouldBe(["payroll"]);
            db.ChangeTracker.Entries<ProtectedResource>().ShouldBeEmpty();
            ResourceQueries(observer).Count.ShouldBe(1);
            QueryEvidence(observer).ShouldContain(this.orders.Scope);
            QueryEvidence(observer).ShouldContain(this.payroll.Scope);
        }
    }

    [Fact]
    public async Task FindByAudiencesAsync_ReturnsEveryMatchInOneUntrackedQuery()
    {
        (ResourceAccessRepository repository, OpenIdentityStackDbContext db, SelectObserver observer) = this.OpenReader();
        await using (db)
        {
            IReadOnlyList<ProtectedResource> loaded = await repository.FindByAudiencesAsync(
                ["https://missing-batch.example.com", this.orders.Audience, this.payroll.Audience]);

            loaded.Select(resource => resource.Audience).ShouldBe([this.orders.Audience, this.payroll.Audience], ignoreOrder: true);
            loaded.ShouldNotContain(resource => resource.Audience == this.billing.Audience);
            loaded.Single(resource => resource.Audience == this.orders.Audience).PermissionNamespaces.ShouldBe(["orders"]);
            loaded.Single(resource => resource.Audience == this.payroll.Audience).Enabled.ShouldBeFalse();
            db.ChangeTracker.Entries<ProtectedResource>().ShouldBeEmpty();
            ResourceQueries(observer).Count.ShouldBe(1);
            QueryEvidence(observer).ShouldContain(this.orders.Audience);
            QueryEvidence(observer).ShouldContain(this.payroll.Audience);
        }
    }

    [Fact]
    public async Task FindByScopesAndAudiences_EmptyInputDoesNotQuery()
    {
        (ResourceAccessRepository repository, OpenIdentityStackDbContext db, SelectObserver observer) = this.OpenReader();
        await using (db)
        {
            (await repository.FindByScopesAsync([])).ShouldBeEmpty();
            (await repository.FindByAudiencesAsync([])).ShouldBeEmpty();
            observer.Commands.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task ListGrantsAsync_ReturnsEveryClientGrantInOneQuery()
    {
        (ResourceAccessRepository repository, OpenIdentityStackDbContext db, SelectObserver observer) = this.OpenReader();
        await using (db)
        {
            IReadOnlyList<ClientResourceGrant> loaded = await repository.ListGrantsAsync(this.client.Id);

            loaded.Select(grant => grant.ResourceId).ShouldBe([this.orders.Id, this.payroll.Id], ignoreOrder: true);
            loaded.Single(grant => grant.ResourceId == this.orders.Id).DelegatedPermissions.ShouldBe(["orders:invoice:read"]);
            loaded.Single(grant => grant.ResourceId == this.payroll.Id).ApplicationPermissions.ShouldBe(["payroll:salary:read"]);
            observer.Commands.Count(command => command.Contains("ClientResourceGrants", StringComparison.Ordinal)).ShouldBe(1);
        }
    }

    private (ResourceAccessRepository Repository, OpenIdentityStackDbContext Db, SelectObserver Observer) OpenReader()
    {
        var observer = new SelectObserver();
        DbContextOptions<OpenIdentityStackDbContext> readOptions = new DbContextOptionsBuilder<OpenIdentityStackDbContext>().UseSqlite(this.connection).AddInterceptors(observer).Options;
        OpenIdentityStackDbContext db = new(readOptions);
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        return (new ResourceAccessRepository(db, Substitute.For<IOpenIddictScopeManager>(), clock), db, observer);
    }

    private static List<string> ResourceQueries(SelectObserver observer) =>
        observer.Commands.Where(command => command.Contains("ProtectedResources", StringComparison.Ordinal)).ToList();

    private static string QueryEvidence(SelectObserver observer) =>
        string.Join('\n', observer.Commands.Concat(observer.Parameters.SelectMany(static values => values)));

    private sealed class SelectObserver : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public List<string[]> Parameters { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            this.Commands.Add(command.CommandText);
            this.Parameters.Add(command.Parameters.Cast<DbParameter>().Select(parameter => parameter.Value?.ToString() ?? string.Empty).ToArray());
            return ValueTask.FromResult(result);
        }
    }
}
