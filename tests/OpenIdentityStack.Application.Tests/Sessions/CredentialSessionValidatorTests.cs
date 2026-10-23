using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Sessions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Application.Tests.Sessions;

public sealed class CredentialSessionValidatorTests
{
    [Fact]
    public async Task ValidateAsync_AfterPasswordChange_RejectsSessionCreatedBeforeTheChange()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero));
        User user = ActiveUser(clock);
        UserSession session = UserSession.Create(user.Id, "203.0.113.1", "agent", clock, userSecurityVersion: user.SecurityVersion).Value;
        user.SetPassword("new", clock);
        (ISessionRepository sessions, IUserRepository users) = StubRepositories(session, user);
        var validator = new CredentialSessionValidator(sessions, users, clock);

        User? validated = await validator.ValidateAsync(user.Id, session.Id);

        validated.ShouldBeNull();
        await sessions.DidNotReceive().GetByIdAsync(Arg.Any<SessionId>(), Arg.Any<CancellationToken>());
        await users.DidNotReceive().GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAsync_UsableSession_ReturnsTheReadOnlyUserWithoutLoadingTheSessionGraph()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero));
        User user = ActiveUser(clock);
        UserSession session = UserSession.Create(user.Id, "203.0.113.1", "agent", clock, userSecurityVersion: user.SecurityVersion).Value;
        (ISessionRepository sessions, IUserRepository users) = StubRepositories(session, user);
        var validator = new CredentialSessionValidator(sessions, users, clock);

        User? validated = await validator.ValidateAsync(user.Id, session.Id);

        validated.ShouldBeSameAs(user);
        await sessions.Received(1).GetValidationSnapshotAsync(session.Id, Arg.Any<CancellationToken>());
        await users.Received(1).GetReadOnlyByIdAsync(user.Id, Arg.Any<CancellationToken>());
        await sessions.DidNotReceive().GetByIdAsync(Arg.Any<SessionId>(), Arg.Any<CancellationToken>());
        await users.DidNotReceive().GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAsync_UnusableSession_DoesNotLoadTheUser()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero));
        User user = ActiveUser(clock);
        UserSession session = UserSession.Create(user.Id, "203.0.113.1", "agent", clock, userSecurityVersion: user.SecurityVersion).Value;
        session.Revoke(clock).IsSuccess.ShouldBeTrue();
        (ISessionRepository sessions, IUserRepository users) = StubRepositories(session, user);
        var validator = new CredentialSessionValidator(sessions, users, clock);

        User? validated = await validator.ValidateAsync(user.Id, session.Id);

        validated.ShouldBeNull();
        await users.DidNotReceive().GetReadOnlyByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ValidateAsync_SessionExpiringAtTheCurrentInstant_RemainsUsableUntilTheNextTick()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        DateTimeOffset createdAt = new(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(createdAt);
        User user = ActiveUser(clock);
        UserSession session = UserSession.Create(user.Id, "203.0.113.1", "agent", clock, userSecurityVersion: user.SecurityVersion).Value;
        (ISessionRepository sessions, IUserRepository users) = StubRepositories(session, user);
        var validator = new CredentialSessionValidator(sessions, users, clock);

        clock.UtcNow.Returns(session.ExpiresAt);
        (await validator.ValidateAsync(user.Id, session.Id)).ShouldBeSameAs(user);

        clock.UtcNow.Returns(session.ExpiresAt.AddTicks(1));
        (await validator.ValidateAsync(user.Id, session.Id)).ShouldBeNull();
    }

    private static User ActiveUser(IDateTimeProvider clock)
    {
        User user = User.CreateLocal("validator@example.test", "Validator", "old", clock).Value;
        user.VerifyEmail(clock).IsSuccess.ShouldBeTrue();
        return user;
    }

    private static (ISessionRepository Sessions, IUserRepository Users) StubRepositories(UserSession session, User user)
    {
        ISessionRepository sessions = Substitute.For<ISessionRepository>();
        IUserRepository users = Substitute.For<IUserRepository>();
        sessions.GetValidationSnapshotAsync(session.Id, Arg.Any<CancellationToken>())
            .Returns(new SessionValidationSnapshot(session.UserId, session.Status, session.ExpiresAt, session.UserSecurityVersion));
        users.GetReadOnlyByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return (sessions, users);
    }
}
