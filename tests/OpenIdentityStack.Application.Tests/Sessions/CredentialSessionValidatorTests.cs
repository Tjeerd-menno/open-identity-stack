using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Sessions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Application.Tests.Sessions;

public sealed class CredentialSessionValidatorTests
{
    [Fact]
    public async Task IsValidAsync_AfterPasswordChange_RejectsSessionCreatedBeforeTheChange()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero));
        User user = User.CreateLocal("validator@example.test", "Validator", "old", clock).Value;
        user.VerifyEmail(clock);
        UserSession session = UserSession.Create(user.Id, "203.0.113.1", "agent", clock, userSecurityVersion: user.SecurityVersion).Value;
        ISessionRepository sessions = Substitute.For<ISessionRepository>();
        IUserRepository users = Substitute.For<IUserRepository>();
        sessions.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);
        users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        var validator = new CredentialSessionValidator(sessions, users, clock);

        user.SetPassword("new", clock);

        bool isValid = await validator.IsValidAsync(user.Id, session.Id);

        isValid.ShouldBeFalse();
    }
}
