using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIdentityStack.Domain.Resources;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Resources;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;
using ClientApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Infrastructure.Tests.Resources;

public sealed class ResourceAccessRepositoryTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    [Fact]
    public async Task SaveAsync_PersistsExplicitMappingsCeilingsRevisionAndAudit()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        IOpenIddictScopeManager scopes = Substitute.For<IOpenIddictScopeManager>();
        await using OpenIdentityStackDbContext db = fixture.CreateDbContext();
        var repository = new ResourceAccessRepository(db, scopes, clock);
        ProtectedResource resource = ProtectedResource.Create("https://orders.example.com", "orders", "Orders", ["orders"]).Value;
        ClientApplication client = ClientApplication.CreateMachineToMachine("reports-client", "Reports", null, ["orders"], clock).Value;
        db.Applications.Add(client);
        repository.AddResource(resource);
        ClientResourceGrant grant = ClientResourceGrant.Create(client.Id, resource.Id, ["orders:invoice:read"], []).Value;
        repository.AddGrant(grant);
        await repository.SaveChangesAsync("operator", "ClientResourceGrantChanged", grant.Id.ToString(), resource);
        db.ChangeTracker.Clear();

        (await repository.FindByAudienceAsync(resource.Audience))!.PermissionNamespaces.ShouldBe(["orders"]);
        ClientResourceGrant loaded = (await repository.GetGrantAsync(client.Id, resource.Id))!;
        loaded.DelegatedPermissions.ShouldBe(["orders:invoice:read"]);
        loaded.ApplicationPermissions.ShouldBeEmpty();
        (await db.AuditLogEntries.SingleAsync(entry => entry.EntityId == grant.Id.ToString())).UserId.ShouldBe("operator");
        await scopes.Received().CreateAsync(Arg.Is<OpenIddictScopeDescriptor>(descriptor => descriptor.Resources.SetEquals(new[] { resource.Audience })), Arg.Any<CancellationToken>());

        await using OpenIdentityStackDbContext stale = fixture.CreateDbContext();
        ClientResourceGrant staleGrant = await stale.ClientResourceGrants.SingleAsync(entry => entry.Id == grant.Id);
        loaded.Configure([], []);
        await repository.SaveChangesAsync("operator", "ClientResourceGrantChanged", grant.Id.ToString());
        staleGrant.Configure(["orders:invoice:read"], []);
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveAsync_ProjectionFailureRollsBackGrantAndAudit()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        IOpenIddictScopeManager scopes = Substitute.For<IOpenIddictScopeManager>();
#pragma warning disable CA2012 // NSubstitute configures the ValueTask-returning method rather than consuming it.
        scopes.FindByNameAsync("failed", Arg.Any<CancellationToken>()).Returns(_ => throw new InvalidOperationException("projection unavailable"));
#pragma warning restore CA2012
        await using OpenIdentityStackDbContext db = fixture.CreateDbContext();
        var repository = new ResourceAccessRepository(db, scopes, clock);
        ProtectedResource resource = ProtectedResource.Create("https://failed.example.com", "failed", "Failed", ["orders"]).Value;
        repository.AddResource(resource);
        await Should.ThrowAsync<InvalidOperationException>(() => repository.SaveChangesAsync("operator", "ResourceMappingChanged", resource.Id.ToString(), resource));
        await using OpenIdentityStackDbContext fresh = fixture.CreateDbContext();
        (await fresh.ProtectedResources.AnyAsync(entry => entry.Id == resource.Id)).ShouldBeFalse();
        (await fresh.AuditLogEntries.AnyAsync(entry => entry.EntityId == resource.Id.ToString())).ShouldBeFalse();
    }
}
