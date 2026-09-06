using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Groups.Commands;
using OpenIdentityStack.Application.Roles.Commands;
using OpenIdentityStack.Application.Users.Commands;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class AdministrativeAuthorityConcurrencyTests(AdministrativeAuthorityTestFixture fixture) : IClassFixture<AdministrativeAuthorityTestFixture>
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedFencedSavePreservesCallerTransactionAndPriorWrites(bool asynchronous)
    {
        await using OpenIdentityStackDbContext db = fixture.CreateDbContext();
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await db.Database.BeginTransactionAsync();
        Role prior = Role.Create($"prior-{Guid.NewGuid():N}", null).Value;
        db.Roles.Add(prior);
        await db.SaveChangesAsync();
        long revision = await db.Set<AdministrativeAuthorityRevision>().Select(value => value.Revision).SingleAsync();
        db.Roles.Add(Role.Create(prior.Name, null).Value);

        if (asynchronous) { await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync()); }
        else { Should.Throw<DbUpdateException>(() => db.SaveChanges()); }

        db.Database.CurrentTransaction.ShouldBeSameAs(transaction);
        (await db.Roles.AnyAsync(value => value.Id == prior.Id)).ShouldBeTrue();
        (await db.Set<AdministrativeAuthorityRevision>().Select(value => value.Revision).SingleAsync()).ShouldBe(revision);
        await transaction.CommitAsync();
        await using OpenIdentityStackDbContext verify = fixture.CreateDbContext();
        (await verify.Roles.CountAsync(value => value.Name == prior.Name)).ShouldBe(1);
    }

    [Fact]
    public async Task SnapshotDropsCachedStateWhenChangeCommittedBeforeCapture()
    {
        await using OpenIdentityStackDbContext writer = fixture.CreateDbContext();
        Role role = Role.Create($"before-{Guid.NewGuid():N}", null).Value;
        role.AddPermission("users:read");
        writer.Roles.Add(role);
        await writer.SaveChangesAsync();
        await using OpenIdentityStackDbContext stale = fixture.CreateDbContext();
        (await stale.Roles.SingleAsync(value => value.Id == role.Id)).Permissions.ShouldNotContain("*");
        role.AddPermission("*");
        await writer.SaveChangesAsync();

        await new AdministrativeAuthoritySnapshot(stale).CaptureAsync();

        (await stale.Roles.SingleAsync(value => value.Id == role.Id)).Permissions.ShouldContain("*");
    }

    [Fact]
    public async Task FailedMutationRollsBackFenceAlongsideData()
    {
        IDateTimeProvider clock = CreateClock();
        await using OpenIdentityStackDbContext writer = fixture.CreateDbContext();
        User user = User.CreateBootstrap($"rollback-{Guid.NewGuid():N}@example.com", "Rollback", "fixture-hash", clock).Value;
        Role role = Role.Create($"rollback-{Guid.NewGuid():N}", null).Value;
        writer.AddRange(user, role);
        await writer.SaveChangesAsync();
        await using OpenIdentityStackDbContext guarded = fixture.CreateDbContext();
        await new AdministrativeAuthoritySnapshot(guarded).CaptureAsync();
        writer.Roles.Add(Role.Create(role.Name, null).Value);

        await Should.ThrowAsync<DbUpdateException>(() => writer.SaveChangesAsync());

        guarded.RoleAssignments.Add(RoleAssignment.Create(user.Id, role.Id, clock.UtcNow).Value);
        await guarded.SaveChangesAsync();
        (await guarded.RoleAssignments.AnyAsync(value => value.UserId == user.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task OrdinaryLoginTimestampDoesNotInvalidateSnapshot()
    {
        IDateTimeProvider clock = CreateClock();
        await using OpenIdentityStackDbContext writer = fixture.CreateDbContext();
        User user = User.CreateBootstrap($"login-{Guid.NewGuid():N}@example.com", "Login", "fixture-hash", clock).Value;
        Role role = Role.Create($"login-{Guid.NewGuid():N}", null).Value;
        writer.AddRange(user, role);
        await writer.SaveChangesAsync();
        await using OpenIdentityStackDbContext guarded = fixture.CreateDbContext();
        await new AdministrativeAuthoritySnapshot(guarded).CaptureAsync();
        user.RecordLogin(clock);
        writer.SaveChanges();
        guarded.RoleAssignments.Add(RoleAssignment.Create(user.Id, role.Id, clock.UtcNow).Value);
        await guarded.SaveChangesAsync();
        (await guarded.RoleAssignments.AnyAsync(value => value.UserId == user.Id)).ShouldBeTrue();
    }

    [Fact]
    public async Task RoleExpansionRejectsStaleGroupMapping()
    {
        IDateTimeProvider clock = CreateClock();
        await using OpenIdentityStackDbContext writer = fixture.CreateDbContext();
        Role role = Role.Create($"mapping-{Guid.NewGuid():N}", null).Value;
        role.AddPermission("users:read");
        Group group = Group.Create($"mapping-{Guid.NewGuid():N}", null, clock).Value;
        writer.AddRange(role, group);
        await writer.SaveChangesAsync();
        await using OpenIdentityStackDbContext stale = fixture.CreateDbContext();
        IAdministrativeApproval approval = Substitute.For<IAdministrativeApproval>();
        approval.CaptureAuthorityAsync(Arg.Any<CancellationToken>()).Returns(_ => new AdministrativeAuthoritySnapshot(stale).CaptureAsync());
        IGroupRepository groups = Substitute.For<IGroupRepository>();
        groups.GetByIdAsync(group.Id, Arg.Any<CancellationToken>()).Returns(async _ => (Group?)await stale.Groups.SingleAsync(value => value.Id == group.Id));
        groups.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(async _ => await stale.SaveChangesAsync());
        IRoleRepository roles = Substitute.For<IRoleRepository>();
        roles.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            Role? prior = await stale.Roles.SingleAsync(value => value.Id == role.Id);
            role.AddPermission("*");
            writer.SaveChanges();
            return (Role?)prior;
        });
        var useCase = new AddGroupMappingUseCase(groups, clock, approval, new UnrestrictedGrantPolicy(roles));

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => useCase.ExecuteAsync(new(group.Id, MappingType.Role, role.Id.Value.ToString(), null, TokenTarget.AccessToken)));

        await using OpenIdentityStackDbContext verification = fixture.CreateDbContext();
        (await verification.Groups.SingleAsync(value => value.Id == group.Id)).Mappings.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TargetAuthorityGainRejectsStaleAccessRestoration(bool passwordReset)
    {
        IDateTimeProvider clock = CreateClock();
        await using OpenIdentityStackDbContext writer = fixture.CreateDbContext();
        User user = User.CreateBootstrap($"restore-{Guid.NewGuid():N}@example.com", "Restore", "original-hash", clock).Value;
        if (!passwordReset) { user.Disable("fixture", clock); }
        Role role = Role.Create($"restore-{Guid.NewGuid():N}", null).Value;
        role.AddPermission("*");
        writer.AddRange(user, role);
        await writer.SaveChangesAsync();
        await using OpenIdentityStackDbContext stale = fixture.CreateDbContext();
        IAdministrativeApproval approval = Substitute.For<IAdministrativeApproval>();
        approval.CaptureAuthorityAsync(Arg.Any<CancellationToken>()).Returns(_ => new AdministrativeAuthoritySnapshot(stale).CaptureAsync());
        approval.RequireForUserAccessAsync(user.Id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            writer.RoleAssignments.Add(RoleAssignment.Create(user.Id, role.Id, clock.UtcNow).Value);
            await writer.SaveChangesAsync();
            return Result.Success();
        });
        IUserRepository users = Substitute.For<IUserRepository>();
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(async _ => (User?)await stale.Users.SingleAsync(value => value.Id == user.Id));
        users.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(async _ => await stale.SaveChangesAsync());
        IPasswordHasher hasher = Substitute.For<IPasswordHasher>();
        hasher.HashPassword(Arg.Any<string>()).Returns("replacement-hash");
        IPasswordPolicyValidator policy = Substitute.For<IPasswordPolicyValidator>();
        policy.ValidatePassword(Arg.Any<string>()).Returns(Result.Success());
        Func<Task> attempt = passwordReset
            ? async () => await new ResetPasswordUseCase(users, hasher, policy, clock, Substitute.For<IAuditLog>(), approval).ExecuteAsync(new(user.Id, "Password123!", "operator"))
            : async () => await new EnableUserUseCase(users, clock, Substitute.For<IAuditLog>(), approval).ExecuteAsync(new(user.Id, "operator"));

        await Should.ThrowAsync<DbUpdateConcurrencyException>(attempt);

        await using OpenIdentityStackDbContext verification = fixture.CreateDbContext();
        User persisted = await verification.Users.SingleAsync(value => value.Id == user.Id);
        persisted.PasswordHash.ShouldBe("original-hash");
        persisted.Status.ShouldBe(user.Status);
    }

    private static IDateTimeProvider CreateClock()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        return clock;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentUnrestrictedChangeRejectsStaleAssignmentWithoutPartialMutation(bool groupMembership)
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        await using OpenIdentityStackDbContext writer = fixture.CreateDbContext();
        User user = User.CreateBootstrap($"race-{Guid.NewGuid():N}@example.com", "Race user", "fixture-hash", clock).Value;
        Role role = Role.Create($"race-{Guid.NewGuid():N}", null).Value;
        role.AddPermission(groupMembership ? "*" : "users:read");
        Group group = Group.Create($"race-{Guid.NewGuid():N}", null, clock).Value;
        writer.AddRange(user, role, group);
        await writer.SaveChangesAsync();
        await using OpenIdentityStackDbContext stale = fixture.CreateDbContext();
        IAdministrativeApproval approval = Substitute.For<IAdministrativeApproval>();
        approval.CaptureAuthorityAsync(Arg.Any<CancellationToken>()).Returns(_ => new AdministrativeAuthoritySnapshot(stale).CaptureAsync());
        approval.RequireAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure(DomainError.Forbidden("HumanRequired", "No human approval.")));
        IRoleRepository roles = Substitute.For<IRoleRepository>();
        roles.GetByIdAsync(role.Id, Arg.Any<CancellationToken>()).Returns(async _ => (Role?)await stale.Roles.SingleAsync(value => value.Id == role.Id));
        roles.IsRoleAssignedAsync(user.Id, role.Id, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            role.AddPermission("*");
            await writer.SaveChangesAsync();
            return false;
        });
        roles.AssignRoleAsync(Arg.Any<RoleAssignment>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            stale.RoleAssignments.Add(call.Arg<RoleAssignment>());
            return Task.CompletedTask;
        });
        roles.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(async _ => await stale.SaveChangesAsync());
        IUserRepository users = Substitute.For<IUserRepository>();
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            if (groupMembership)
            {
                group.AddMapping(MappingType.Role, role.Id.Value.ToString(), null, TokenTarget.AccessToken, clock);
                await writer.SaveChangesAsync();
            }
            return (User?)await stale.Users.SingleAsync(value => value.Id == user.Id);
        });
        IGroupRepository groups = Substitute.For<IGroupRepository>();
        groups.GetByIdAsync(group.Id, Arg.Any<CancellationToken>()).Returns(async _ => (Group?)await stale.Groups.SingleAsync(value => value.Id == group.Id));
        groups.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(async _ => await stale.SaveChangesAsync());
        Func<Task> attempt = groupMembership
            ? async () => await new AddUserToGroupUseCase(groups, users, clock, approval, new UnrestrictedGrantPolicy(roles))
                .ExecuteAsync(new(group.Id, user.Id, user.Id))
            : async () => await new AssignRoleUseCase(users, roles, clock, Substitute.For<IAuditLog>(), Substitute.For<ILogger<AssignRoleUseCase>>(), approval)
                .ExecuteAsync(new(user.Id, role.Id, "ordinary-operator"));

        await Should.ThrowAsync<DbUpdateConcurrencyException>(attempt);
        await using OpenIdentityStackDbContext verification = fixture.CreateDbContext();
        (await verification.RoleAssignments.AnyAsync(value => value.UserId == user.Id)).ShouldBeFalse();
        (await verification.Set<GroupMembership>().AnyAsync(value => value.UserId == user.Id)).ShouldBeFalse();
        await approval.DidNotReceive().RecordOutcomeAsync(true, Arg.Any<CancellationToken>());
    }
}
