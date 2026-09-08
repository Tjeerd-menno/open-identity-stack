using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Identity;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Sessions;
using OpenIdentityStack.Infrastructure.Tests.Common;

using SharedKernel;
namespace OpenIdentityStack.Infrastructure.Tests.Identity;

public sealed class CredentialTerminationServiceTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture fixture = fixture;
    private readonly IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();

    public ValueTask InitializeAsync()
    {
        this.clock.UtcNow.Returns(new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero));
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task TerminateSessionAsync_IsIdempotentAndWritesOneAudit()
    {
        await this.fixture.ClearAllDataAsync();
        UserSession session = await this.SeedSessionAsync();
        await using (OpenIdentityStackDbContext context = this.fixture.CreateDbContext())
        {
            var service = new CredentialTerminationService(context, this.clock);
            (await service.TerminateSessionAsync(session.Id, "admin", "security event")).IsSuccess.ShouldBeTrue();
            (await service.TerminateSessionAsync(session.Id, "admin", "security event")).IsSuccess.ShouldBeTrue();
        }
        await using OpenIdentityStackDbContext verify = this.fixture.CreateDbContext();
        (await verify.UserSessions.SingleAsync(x => x.Id == session.Id)).Status.ShouldBe(SessionStatus.Revoked);
        (await verify.AuditLogEntries.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task TerminateAllSessionsAsync_PreservesExcludedSessionAtNewEpoch()
    {
        await this.fixture.ClearAllDataAsync();
        User user = await this.SeedUserAsync();
        UserSession retained = await this.SeedSessionAsync(user.Id);
        UserSession revoked = await this.SeedSessionAsync(user.Id);
        await using (OpenIdentityStackDbContext context = this.fixture.CreateDbContext())
        {
            Result<int> result = await new CredentialTerminationService(context, this.clock)
                .TerminateAllSessionsAsync(user.Id, "admin", "security event", retained.Id);
            result.Value.ShouldBe(1);
        }
        await using OpenIdentityStackDbContext verify = this.fixture.CreateDbContext();
        User persistedUser = await verify.Users.SingleAsync(x => x.Id == user.Id);
        UserSession persistedRetained = await verify.UserSessions.SingleAsync(x => x.Id == retained.Id);
        (await verify.UserSessions.SingleAsync(x => x.Id == revoked.Id)).Status.ShouldBe(SessionStatus.Revoked);
        persistedRetained.Status.ShouldBe(SessionStatus.Active);
        persistedRetained.UserSecurityVersion.ShouldBe(persistedUser.SecurityVersion);
    }

    [Fact]
    public async Task TerminateSessionAsync_WhenAuditPersistenceFails_RollsBackTerminalState()
    {
        await this.fixture.ClearAllDataAsync();
        UserSession session = await this.SeedSessionAsync();
        await using (var failing = new FailingSaveDbContext(this.fixture.Options))
        {
            var service = new CredentialTerminationService(failing, this.clock);
            await Should.ThrowAsync<InvalidOperationException>(() => service.TerminateSessionAsync(session.Id, "admin", "audit failure"));
        }

        await using OpenIdentityStackDbContext verify = this.fixture.CreateDbContext();
        (await verify.UserSessions.SingleAsync(x => x.Id == session.Id)).Status.ShouldBe(SessionStatus.Active);
        (await verify.AuditLogEntries.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task StaleContextUpdate_AfterTermination_ThrowsConcurrencyException()
    {
        await this.fixture.ClearAllDataAsync();
        UserSession session = await this.SeedSessionAsync();
        await using OpenIdentityStackDbContext stale = this.fixture.CreateDbContext();
        UserSession staleSession = await stale.UserSessions.SingleAsync(x => x.Id == session.Id);
        await using (OpenIdentityStackDbContext terminating = this.fixture.CreateDbContext())
        {
            (await new CredentialTerminationService(terminating, this.clock).TerminateSessionAsync(session.Id, "admin", "terminate")).IsSuccess.ShouldBeTrue();
        }

        IDateTimeProvider laterClock = Substitute.For<IDateTimeProvider>();
        laterClock.UtcNow.Returns(this.clock.UtcNow.AddMinutes(1));
        staleSession.UpdateLastActivity(laterClock);
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());

        await using OpenIdentityStackDbContext verify = this.fixture.CreateDbContext();
        (await verify.UserSessions.SingleAsync(x => x.Id == session.Id)).Status.ShouldBe(SessionStatus.Revoked);
    }

    [Fact]
    public async Task CredentialLifecycleTransactionRunner_WhenAuditSaveFails_RollsBackUserEpochAndSession()
    {
        await this.fixture.ClearAllDataAsync();
        User user = await this.SeedUserAsync();
        UserSession session = await this.SeedSessionAsync(user.Id);
        await using (OpenIdentityStackDbContext context = this.fixture.CreateDbContext())
        {
            User trackedUser = await context.Users.SingleAsync(x => x.Id == user.Id);
            UserSession trackedSession = await context.UserSessions.SingleAsync(x => x.Id == session.Id);
            var runner = new CredentialLifecycleTransactionRunner(context);

            await Should.ThrowAsync<InvalidOperationException>(() => runner.ExecuteAsync<bool>(async cancellationToken =>
            {
                trackedUser.AdvanceSecurityVersion(this.clock);
                trackedSession.Revoke(this.clock);
                await context.SaveChangesAsync(cancellationToken);
                throw new InvalidOperationException("Audit persistence failed after session and user changes were saved.");
            }));
        }

        await using OpenIdentityStackDbContext verify = this.fixture.CreateDbContext();
        (await verify.Users.SingleAsync(x => x.Id == user.Id)).SecurityVersion.ShouldBe(0);
        (await verify.UserSessions.SingleAsync(x => x.Id == session.Id)).Status.ShouldBe(SessionStatus.Active);
    }

    [Fact]
    public async Task PendingLogoutQuery_ReturnsOnlyDuePendingOrFailedDeliveries()
    {
        await this.fixture.ClearAllDataAsync();
        UserSession due = UserSession.Create(UserId.Create(), "127.0.0.1", "test", this.clock).Value;
        UserSession future = UserSession.Create(UserId.Create(), "127.0.0.1", "test", this.clock).Value;
        UserSession completed = UserSession.Create(UserId.Create(), "127.0.0.1", "test", this.clock).Value;
        UserSession frontOnly = UserSession.Create(UserId.Create(), "127.0.0.1", "test", this.clock).Value;
        due.AddClientSession("due", this.clock);
        future.AddClientSession("future", this.clock);
        completed.AddClientSession("completed", this.clock);
        frontOnly.AddClientSession("front-only", this.clock);
        due.ClientSessions.Single().SetLogoutUris(null, "https://due.example.test/back");
        future.ClientSessions.Single().SetLogoutUris(null, "https://future.example.test/back");
        completed.ClientSessions.Single().SetLogoutUris(null, "https://completed.example.test/back");
        frontOnly.ClientSessions.Single().SetLogoutUris("https://front.example.test/logout", null);
        due.Revoke(this.clock);
        future.Revoke(this.clock);
        completed.Revoke(this.clock);
        frontOnly.Revoke(this.clock);
        future.ClientSessions.Single().MarkLogoutAttemptFailed(this.clock);
        completed.ClientSessions.Single().MarkLogoutCompleted(this.clock);
        await using (OpenIdentityStackDbContext save = this.fixture.CreateDbContext())
        {
            save.AddRange(due, future, completed, frontOnly);
            await save.SaveChangesAsync();
        }

        await using OpenIdentityStackDbContext queryContext = this.fixture.CreateDbContext();
        IReadOnlyList<UserSession> results = await new SessionRepository(queryContext)
            .GetTerminalSessionsWithPendingLogoutNotificationsAsync(this.clock.UtcNow, 10);

        results.Select(x => x.Id).ShouldBe([due.Id]);
    }

    private async Task<User> SeedUserAsync()
    {
        User user = User.CreateLocal($"{Guid.NewGuid()}@example.test", "User", "hash", this.clock).Value;
        user.VerifyEmail(this.clock);
        await using OpenIdentityStackDbContext context = this.fixture.CreateDbContext();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task<UserSession> SeedSessionAsync(UserId? userId = null)
    {
        UserSession session = UserSession.Create(userId ?? UserId.Create(), "127.0.0.1", "agent", this.clock).Value;
        await using OpenIdentityStackDbContext context = this.fixture.CreateDbContext();
        context.UserSessions.Add(session);
        await context.SaveChangesAsync();
        return session;
    }

    private sealed class FailingSaveDbContext(DbContextOptions<OpenIdentityStackDbContext> options) : OpenIdentityStackDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Audit persistence failed.");
    }
}
