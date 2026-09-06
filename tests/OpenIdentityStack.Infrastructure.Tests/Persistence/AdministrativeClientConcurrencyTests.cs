using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Applications;
using OpenIdentityStack.Application.Applications.Commands;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.Resources;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;
using DomainApplicationId = OpenIdentityStack.Domain.Applications.ApplicationId;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class AdministrativeClientConcurrencyTests(AdministrativeAuthorityTestFixture fixture) : IClassFixture<AdministrativeAuthorityTestFixture>
{
    [Fact]
    public async Task ActualAdminWorkflowReturnsConflictWhenAuthorityChangesDuringCreatePersistence()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        await using OpenIdentityStackDbContext writer = fixture.CreateDbContext();
        DomainApplication administrativeClient = DomainApplication.Create($"admin-create-race-{Guid.NewGuid():N}", "Admin", null,
            ApplicationProfile.Web, OAuthClientType.Confidential, ["authorization_code"], ["openid", "ois.admin"],
            ["https://example.com/callback"], [], true, false, clock).Value;
        writer.Add(administrativeClient);
        if (!await writer.ProtectedResources.AnyAsync(resource => resource.Id == ProtectedResource.AdministrativeResourceId))
        {
            writer.Add(ProtectedResource.CreateAdministrative());
        }
        await writer.SaveChangesAsync();

        await using OpenIdentityStackDbContext stale = fixture.CreateDbContext();
        var snapshot = new AdministrativeAuthoritySnapshot(stale);
        IAdministrativeClientGuard guard = Substitute.For<IAdministrativeClientGuard>();
        guard.CaptureAuthorityAsync(Arg.Any<CancellationToken>()).Returns(_ => snapshot.CaptureAsync());
        IApplicationRepository repository = Substitute.For<IApplicationRepository>();
        repository.ExistsByClientIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            writer.ClientResourceGrants.Add(ClientResourceGrant.Create(
                administrativeClient.Id,
                ProtectedResource.AdministrativeResourceId,
                ["users:read"],
                []).Value);
            await writer.SaveChangesAsync();
            return false;
        });
        repository.AddAsync(Arg.Any<DomainApplication>(), Arg.Any<CancellationToken>()).Returns(async call =>
            await stale.Applications.AddAsync(call.Arg<DomainApplication>()));
        repository.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(async _ => await stale.SaveChangesAsync());
        IApplicationProtocolProjection projection = Substitute.For<IApplicationProtocolProjection>();
        projection.UpsertAsync(Arg.Any<DomainApplication>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        projection.DeleteAsync(Arg.Any<DomainApplicationId>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        var lifecycle = new ApplicationLifecycleUseCases(repository, projection, Substitute.For<IPasswordHasher>(), clock,
            Substitute.For<IAuditLog>(), guard, Substitute.For<IApplicationProtocolProjectionTransaction>());

        Result<ApplicationCreateCommandResult> result = await lifecycle.ExecuteCreateAsync(new CreateApplicationCommand(
            $"new-client-{Guid.NewGuid():N}", "New client", null, ApplicationProfile.Web, OAuthClientType.Confidential,
            ["authorization_code"], ["openid"], ["https://new.example.com/callback"], [], true, false), null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Conflict.Application.CreateConflict");
        await projection.Received(1).DeleteAsync(Arg.Any<DomainApplicationId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActualAdminWorkflowRejectsEnableWhenClientGainsAdministrativeGrantAfterRead()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        await using OpenIdentityStackDbContext writer = fixture.CreateDbContext();
        DomainApplication client = DomainApplication.Create($"enable-race-{Guid.NewGuid():N}", "Client", null,
            ApplicationProfile.Web, OAuthClientType.Confidential, ["authorization_code"], ["openid", "ois.admin"],
            ["https://example.com/callback"], [], true, false, clock).Value;
        client.Disable(clock);
        writer.Add(client);
        if (!await writer.ProtectedResources.AnyAsync(resource => resource.Id == ProtectedResource.AdministrativeResourceId))
        {
            writer.Add(ProtectedResource.CreateAdministrative());
        }
        await writer.SaveChangesAsync();
        await using OpenIdentityStackDbContext stale = fixture.CreateDbContext();
        IAdministrativeClientGuard guard = Substitute.For<IAdministrativeClientGuard>();
        guard.CaptureAuthorityAsync(Arg.Any<CancellationToken>()).Returns(_ => new AdministrativeAuthoritySnapshot(stale).CaptureAsync());
        guard.RequireAsync(client.Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            // The old read found no Admin entitlement and required no privileged approval.
            writer.ClientResourceGrants.Add(ClientResourceGrant.Create(client.Id, ProtectedResource.AdministrativeResourceId, ["users:read"], []).Value);
            await writer.SaveChangesAsync();
            return Result.Success();
        });
        IApplicationRepository repository = Substitute.For<IApplicationRepository>();
        repository.GetByIdAsync(client.Id, Arg.Any<CancellationToken>()).Returns(async _ => (DomainApplication?)await stale.Applications.SingleAsync(value => value.Id == client.Id));
        repository.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(async _ => await stale.SaveChangesAsync());
        IApplicationProtocolProjection projection = Substitute.For<IApplicationProtocolProjection>();
        projection.UpsertAsync(Arg.Any<DomainApplication>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        IPasswordHasher hasher = Substitute.For<IPasswordHasher>();
        IAuditLog audit = Substitute.For<IAuditLog>();
        var workflow = new ApplicationsAdminWorkflow(new ApplicationLifecycleUseCases(repository, projection, hasher, clock, audit, guard,
                Substitute.For<IApplicationProtocolProjectionTransaction>()),
            new ApplicationCredentialUseCases(repository, projection, hasher, clock, audit, guard));

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => workflow.EnableAsync(new(client.Id)));

        await using OpenIdentityStackDbContext verification = fixture.CreateDbContext();
        (await verification.Applications.SingleAsync(value => value.Id == client.Id)).Status.ShouldBe(ApplicationStatus.Disabled);
        await guard.DidNotReceive().RecordOutcomeAsync(Arg.Any<CancellationToken>());
    }
}
