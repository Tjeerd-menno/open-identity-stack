using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Application.Security.Commands;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Settings;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Identity;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Groups;
using OpenIdentityStack.Infrastructure.Persistence.Roles;
using OpenIdentityStack.Infrastructure.Persistence.Users;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class CredentialCutoverAuthorityConcurrencyTests(AdministrativeAuthorityTestFixture fixture) : IClassFixture<AdministrativeAuthorityTestFixture>
{
    [Fact]
    public async Task CutoverBoundaryFenceBlocksIssuanceUntilTheNewEpochCommits()
    {
        await using (OpenIdentityStackDbContext probe = fixture.CreateDbContext())
        {
            Assert.SkipWhen(!probe.Database.IsNpgsql(), "Overlapping credential-boundary lock verification requires PostgreSQL.");
        }

        var gateEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ICredentialCutoverGate gate = Substitute.For<ICredentialCutoverGate>();
        gate.EvaluateAsync(Arg.Any<CancellationToken>()).Returns(async _ =>
        {
            gateEntered.TrySetResult();
            await releaseGate.Task;
            return new CredentialCutoverPreflight(Guid.Empty, DateTimeOffset.UtcNow, [], null,
                new(0, 0, 0, 0, 0, 0, 0, 0), [], [], 0, null);
        });
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        var operationId = Guid.NewGuid();
        await using OpenIdentityStackDbContext cutoverDb = fixture.CreateDbContext();
        var store = new CredentialBoundaryStore(cutoverDb, Substitute.For<IOpenIddictTokenManager>(),
            Substitute.For<IOpenIddictAuthorizationManager>(), clock, gate);
        Task<Result<CredentialCutoverResult>> cutover = store.ExecuteAsync(operationId, "operator");
        await gateEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await using OpenIdentityStackDbContext issuanceDb = fixture.CreateDbContext();
        await using var issuance = new TokenIssuanceTransaction(issuanceDb);
        Task beginIssuance = issuance.BeginAsync(default);
        bool observedWaiting = false;
        try
        {
            await using OpenIdentityStackDbContext observer = fixture.CreateDbContext();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!beginIssuance.IsCompleted)
            {
                observedWaiting = await observer.Database.SqlQueryRaw<bool>("""
                    SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE datname = current_database()
                        AND wait_event_type = 'Lock' AND query LIKE '%CredentialBoundary%') AS "Value"
                    """).SingleAsync(deadline.Token);
                if (observedWaiting) { break; }
                await Task.Delay(20, deadline.Token);
            }
        }
        finally
        {
            releaseGate.TrySetResult();
        }

        (await cutover).IsSuccess.ShouldBeTrue();
        await beginIssuance;
        observedWaiting.ShouldBeTrue();
        (await issuanceDb.Set<CredentialBoundaryState>().AsNoTracking().Select(value => value.Epoch).SingleAsync())
            .ShouldBe(operationId);
        await issuance.CommitAsync(default);
    }

    [Theory]
    [InlineData("permission")]
    [InlineData("user")]
    [InlineData("local-fallback")]
    public async Task AuthorityChangeCannotAdvanceBoundaryAfterApprovalReads(string change)
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        await using OpenIdentityStackDbContext writer = fixture.CreateDbContext();
        User user = User.CreateBootstrap($"cutover-race-{Guid.NewGuid():N}@example.com", "Operator", "fixture-hash", clock).Value;
        Role role = Role.Create($"cutover-race-{Guid.NewGuid():N}", null).Value;
        role.AddPermission("*");
        UserSession session = UserSession.Create(user.Id, "127.0.0.1", "Fixture", clock).Value;
        writer.AddRange(user, role, session, RoleAssignment.Create(user.Id, role.Id, clock.UtcNow).Value);
        await writer.SaveChangesAsync();
        Guid originalEpoch = await writer.Set<CredentialBoundaryState>().Select(value => value.Epoch).SingleAsync();
        var operationId = Guid.NewGuid();
        await using OpenIdentityStackDbContext stale = fixture.CreateDbContext();
        IAdministrativeActorContext actor = Substitute.For<IAdministrativeActorContext>();
        var currentActor = new AdministrativeActor(user.Id, clock.UtcNow, true, true);
        actor.Current.Returns(currentActor);
        IAdministrativeApprovalAudit audit = Substitute.For<IAdministrativeApprovalAudit>();
        audit.LogAsync(Arg.Any<string>(), "AdministrativeApproval.IntentApproved", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                // The real approval has read the active user and unrestricted role. Another
                // request now commits their revocation before the boundary store starts.
                if (change == "permission") { role.RemovePermission("*"); }
                else if (change == "user") { user.Disable("Other administrator", clock); }
                else
                {
                    AuthenticationSettings settings = await writer.AuthenticationSettings.SingleOrDefaultAsync()
                        ?? AuthenticationSettings.CreateDefault(clock);
                    if (writer.Entry(settings).State == EntityState.Detached) { writer.AuthenticationSettings.Add(settings); }
                    settings.DisableLocalFallback(clock);
                }
                await writer.SaveChangesAsync();
            });
        var approval = new AdministrativeApproval(actor, new UserRepository(stale),
            new GetUserEffectiveRolesQueryHandler(new RoleRepository(stale), new GroupRepository(stale)),
            clock, audit, new AdministrativeAuthoritySnapshot(stale),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AdministrativeApproval>.Instance);
        IOpenIddictTokenManager tokens = Substitute.For<IOpenIddictTokenManager>();
        IOpenIddictAuthorizationManager grants = Substitute.For<IOpenIddictAuthorizationManager>();
        ICredentialCutoverGate gate = Substitute.For<ICredentialCutoverGate>();
        gate.EvaluateAsync(Arg.Any<CancellationToken>()).Returns(new CredentialCutoverPreflight(originalEpoch, DateTimeOffset.UtcNow, [], null, new(0, 0, 0, 0, 0, 0, 0, 0), [], [], 0, null));
        var store = new CredentialBoundaryStore(stale, tokens, grants, clock, gate);
        var useCase = new ExecuteCredentialCutoverUseCase(store, approval, actor);

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => useCase.ExecuteAsync(operationId));

        await using OpenIdentityStackDbContext verify = fixture.CreateDbContext();
        (await verify.Set<CredentialBoundaryState>().Select(value => value.Epoch).SingleAsync()).ShouldBe(originalEpoch);
        (await verify.Set<CredentialCutoverRecord>().AnyAsync(value => value.Id == operationId)).ShouldBeFalse();
        (await verify.UserSessions.SingleAsync(value => value.Id == session.Id)).Status.ShouldBe(SessionStatus.Active);
        (await verify.AuditLogEntries.AnyAsync(value => value.Action == "CredentialBoundary.CutoverCompleted" && value.EntityId == operationId.ToString())).ShouldBeFalse();
        (await verify.AuditLogEntries.AnyAsync(value => value.Action == "CredentialCutover.PreflightPassed" && value.EntityId == operationId.ToString())).ShouldBeFalse();
        await tokens.DidNotReceive().RevokeAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await grants.DidNotReceive().RevokeAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await audit.DidNotReceive().LogAsync(Arg.Any<string>(), "AdministrativeApproval.MutationSucceeded", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
