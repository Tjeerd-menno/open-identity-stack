using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.AdministrativeAccess;
using OpenIdentityStack.Application.Resources;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Applications;
using OpenIdentityStack.Domain.Resources;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Applications;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Groups;
using OpenIdentityStack.Infrastructure.Persistence.Roles;
using OpenIdentityStack.Infrastructure.Persistence.Users;
using OpenIdentityStack.Infrastructure.Resources;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;
using DomainApplication = OpenIdentityStack.Domain.Applications.Application;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class AdministrativeAccessAuthorityRaceTests(AdministrativeAuthorityTestFixture fixture) : IClassFixture<AdministrativeAuthorityTestFixture>
{
    [Fact]
    public async Task ResourceWindowReviewConsumesCapturedAuthorityRevision()
    {
        await using OpenIdentityStackDbContext requestDb = fixture.CreateDbContext();
        await requestDb.CaptureAuthoritySnapshotAsync(CancellationToken.None);
        await using (OpenIdentityStackDbContext writer = fixture.CreateDbContext())
        {
            Role role = Role.Create($"authority-change-{Guid.NewGuid():N}", "Authority change").Value;
            writer.Roles.Add(role);
            await writer.SaveChangesAsync();
        }
        requestDb.ResourceTokenWindowReviews.Add(new ResourceWindowReviewRecord
        {
            Id = Guid.NewGuid(), ResourceId = Guid.NewGuid(), Epoch = Guid.NewGuid(), ResourceRevision = 1,
            Mechanism = "OnlineIntrospection", EvidenceReference = "race-test", ReviewedAt = DateTimeOffset.UtcNow
        });

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => requestDb.SaveChangesAsync());
    }

    [Theory]
    [InlineData("role")]
    [InlineData("client")]
    [InlineData("delegated")]
    [InlineData("machine")]
    [InlineData("unchanged")]
    public async Task AuthorizationReadsAndMutationShareTheSameAuthorityRevision(string change)
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        await using OpenIdentityStackDbContext writer = fixture.CreateDbContext();
        ProtectedResource? admin = await writer.ProtectedResources.SingleOrDefaultAsync(resource => resource.Scope == "ois.admin");
        if (admin is null) { admin = ProtectedResource.CreateAdministrative(); writer.Add(admin); }
        DomainApplication client = DomainApplication.Create($"client-{Guid.NewGuid():N}", "Client", null,
            ApplicationProfile.MachineToMachine, OAuthClientType.Confidential, ["client_credentials"], ["ois.admin"], [], [], false, false, clock).Value;
        User user = User.CreateBootstrap($"user-{Guid.NewGuid():N}@example.test", "User", "hash", clock).Value;
        Role role = Role.Create($"role-{Guid.NewGuid():N}", "Role").Value;
        role.AddPermission("roles:write");
        ClientResourceGrant grant = ClientResourceGrant.Create(client.Id, admin.Id, ["roles:write"], ["roles:write"]).Value;
        writer.AddRange(client, user, role, grant, RoleAssignment.Create(user.Id, role.Id, clock.UtcNow).Value);
        await writer.SaveChangesAsync();

        await using OpenIdentityStackDbContext requestDb = fixture.CreateDbContext();
        var snapshot = new AdministrativeAuthoritySnapshot(requestDb);
        var projection = new ResourcePermissionService(new ResourceAccessRepository(requestDb, Substitute.For<IOpenIddictScopeManager>(), clock),
            new ApplicationRepository(requestDb), Substitute.For<IApplicationPermissionRegistryRepository>(), new UserRepository(requestDb),
            new GetUserEffectiveRolesQueryHandler(new RoleRepository(requestDb), new GroupRepository(requestDb)));
        var evaluator = new AdministrativeAccessEvaluator(projection, snapshot);
        Result<IReadOnlyList<string>> permissions = await evaluator.EvaluateAsync(new(client.ClientId, change == "machine" ? null : user.Id, ["roles:write"]));
        permissions.IsSuccess.ShouldBeTrue();
        permissions.Value.ShouldContain("roles:write");

        switch (change)
        {
            case "role": role.RemovePermission("roles:write"); break;
            case "client": client.Disable(clock); break;
            case "delegated": grant.Configure([], ["roles:write"]); break;
            case "machine": grant.Configure(["roles:write"], []); break;
        }
        await writer.SaveChangesAsync();

        IAdministrativeApproval approval = Substitute.For<IAdministrativeApproval>();
        approval.CaptureAuthorityAsync(Arg.Any<CancellationToken>()).Returns(call => snapshot.CaptureAsync(call.Arg<CancellationToken>()));
        var useCase = new CreateRoleUseCase(new RoleRepository(requestDb), Substitute.For<IPermissionAssignmentValidator>(), approval);
        string name = $"mutation-{Guid.NewGuid():N}";
        var command = new CreateRoleCommand(name, "Role", null, []);
        if (change == "unchanged") { (await useCase.ExecuteAsync(command)).IsSuccess.ShouldBeTrue(); }
        else { await Should.ThrowAsync<DbUpdateConcurrencyException>(() => useCase.ExecuteAsync(command)); }

        await using OpenIdentityStackDbContext read = fixture.CreateDbContext();
        (await read.Roles.AnyAsync(value => value.Name == name)).ShouldBe(change == "unchanged");
    }
}
