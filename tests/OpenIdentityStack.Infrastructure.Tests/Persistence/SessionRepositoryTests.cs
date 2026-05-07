using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Sessions;
using OpenIdentityStack.Infrastructure.Tests.Common;

using SharedKernel;
#pragma warning disable IDE0007 // Use var instead of explicit type
#pragma warning disable IDE0008 // Use explicit type instead of var

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class SessionRepositoryTests : IClassFixture<SqliteTestFixture>, IAsyncLifetime
{
    private readonly SqliteTestFixture _fixture;
    private OpenIdentityStackDbContext _dbContext = null!;
    private SessionRepository _repository = null!;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly DateTimeOffset _now = new(2026, 1, 22, 12, 0, 0, TimeSpan.Zero);

    public SessionRepositoryTests(SqliteTestFixture fixture)
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
        this._repository = new SessionRepository(this._dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        await this._dbContext.DisposeAsync();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsSession_WhenExists()
    {
        UserSession session = await this.SeedAsync(UserId.Create());

        UserSession? result = await this._repository.GetByIdAsync(session.Id);

        result.ShouldNotBeNull();
        result!.Id.ShouldBe(session.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotExists()
    {
        UserSession? result = await this._repository.GetByIdAsync(SessionId.Create());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsSessions()
    {
        var userId = UserId.Create();
        await this.SeedAsync(userId);
        await this.SeedAsync(userId);
        await this.SeedAsync(UserId.Create()); // Different user

        var result = await this._repository.GetByUserIdAsync(userId);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetActiveByUserIdAsync_ReturnsOnlyActiveSessions()
    {
        var userId = UserId.Create();
        await this.SeedAsync(userId);
        var revoked = await this.SeedAsync(userId);
        revoked.Revoke(this._dateTimeProvider);
        await this._repository.UpdateAsync(revoked);

        var result = await this._repository.GetActiveByUserIdAsync(userId);

        result.Count.ShouldBe(1);
        result[0].Status.ShouldBe(SessionStatus.Active);
    }

    [Fact]
    public async Task AddAsync_AddsSession()
    {
        var userId = UserId.Create();
        var session = UserSession.Create(userId, "127.0.0.1", "Test Browser", this._dateTimeProvider).Value;

        await this._repository.AddAsync(session);

        UserSession? saved = await this._dbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        saved.ShouldNotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesSession()
    {
        var session = await this.SeedAsync(UserId.Create());
        session.Revoke(this._dateTimeProvider);

        await this._repository.UpdateAsync(session);

        UserSession? updated = await this._dbContext.UserSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
        updated!.Status.ShouldBe(SessionStatus.Revoked);
    }

    [Fact]
    public async Task ListAsync_ReturnsPagedSessions()
    {
        var userId = UserId.Create();
        await this.SeedAsync(userId);
        await this.SeedAsync(userId);
        await this.SeedAsync(userId);

        (IReadOnlyList<UserSession> sessions, int total) = await this._repository.ListAsync(1, 2);

        total.ShouldBe(3);
        sessions.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListAsync_FiltersBy_UserId()
    {
        var userId1 = UserId.Create();
        var userId2 = UserId.Create();
        await this.SeedAsync(userId1);
        await this.SeedAsync(userId2);

        (IReadOnlyList<UserSession> sessions, int total) = await this._repository.ListAsync(1, 10, userId1);

        total.ShouldBe(1);
        sessions[0].UserId.ShouldBe(userId1);
    }

    [Fact]
    public async Task ListAsync_FiltersBy_Status()
    {
        var active = await this.SeedAsync(UserId.Create());
        var revoked = await this.SeedAsync(UserId.Create());
        revoked.Revoke(this._dateTimeProvider);
        await this._repository.UpdateAsync(revoked);

        (IReadOnlyList<UserSession> sessions, int total) = await this._repository.ListAsync(1, 10, statusFilter: SessionStatus.Active);

        total.ShouldBe(1);
        sessions[0].Status.ShouldBe(SessionStatus.Active);
    }

    private async Task<UserSession> SeedAsync(UserId userId)
    {
        UserSession session = UserSession.Create(userId, "127.0.0.1", "Test Browser", this._dateTimeProvider).Value;
        await this._repository.AddAsync(session);
        return session;
    }
}
