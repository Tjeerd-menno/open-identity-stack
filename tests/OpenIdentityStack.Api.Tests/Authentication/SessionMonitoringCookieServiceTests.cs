using Microsoft.AspNetCore.DataProtection;
using OpenIdentityStack.Api.Authentication;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Users;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Authentication;

public sealed class SessionMonitoringCookieServiceTests
{
    private readonly IUserRepository users = Substitute.For<IUserRepository>();
    private readonly ISessionRepository sessions = Substitute.For<ISessionRepository>();
    private readonly ICredentialBoundaryStore boundary = Substitute.For<ICredentialBoundaryStore>();
    private readonly IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
    private readonly SessionMonitoringCookieService service;
    private readonly User user;
    private readonly UserSession session;
    private readonly Guid epoch = Guid.NewGuid();

    public SessionMonitoringCookieServiceTests()
    {
        DateTimeOffset now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        this.clock.UtcNow.Returns(now);
        this.user = User.CreateLocal("monitor@example.test", "Monitor", "fixture-hash", this.clock).Value;
        this.user.VerifyEmail(this.clock).IsSuccess.ShouldBeTrue();
        this.session = UserSession.Create(this.user.Id, "127.0.0.1", "tests", this.clock).Value;
        this.users.GetByIdAsync(this.user.Id, Arg.Any<CancellationToken>()).Returns(this.user);
        this.sessions.GetByIdAsync(this.session.Id, Arg.Any<CancellationToken>()).Returns(this.session);
        this.boundary.IsCurrentAsync(this.epoch.ToString(), Arg.Any<CancellationToken>()).Returns(true);
        this.service = new SessionMonitoringCookieService(
            new EphemeralDataProtectionProvider(), this.users, this.sessions, this.boundary, this.clock);
    }

    [Fact]
    public async Task ProtectedCookieValidatesWithoutUpdatingSessionActivity()
    {
        string value = this.service.Create(
            this.user.Id, this.session.Id, this.epoch, this.clock.UtcNow.AddHours(1));

        (await this.service.IsCurrentAsync(value)).ShouldBeTrue();
        await this.sessions.DidNotReceive().UpdateAsync(
            Arg.Any<UserSession>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("legacy-random-cookie")]
    [InlineData("")]
    [InlineData(null)]
    public async Task UnprotectedOrMissingCookieFailsClosed(string? value)
    {
        (await this.service.IsCurrentAsync(value)).ShouldBeFalse();
    }

    [Fact]
    public async Task LegacyBase64UrlCookieRemainsCurrentBeforeCredentialCutover()
    {
        const string legacyValue = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        this.boundary.IsCurrentAsync(null, Arg.Any<CancellationToken>()).Returns(true);

        (await this.service.IsCurrentAsync(legacyValue)).ShouldBeTrue();
    }

    [Fact]
    public async Task LegacyBase64UrlCookieFailsClosedAfterCredentialCutover()
    {
        const string legacyValue = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        this.boundary.IsCurrentAsync(null, Arg.Any<CancellationToken>()).Returns(false);

        (await this.service.IsCurrentAsync(legacyValue)).ShouldBeFalse();
    }

    [Fact]
    public async Task TamperedCookieFailsClosed()
    {
        string value = this.service.Create(
            this.user.Id, this.session.Id, this.epoch, this.clock.UtcNow.AddHours(1));
        char replacement = value[^1] == 'A' ? 'B' : 'A';

        (await this.service.IsCurrentAsync(value[..^1] + replacement)).ShouldBeFalse();
    }

    [Fact]
    public async Task ExpiredCookieFailsClosed()
    {
        string value = this.service.Create(
            this.user.Id, this.session.Id, this.epoch, this.clock.UtcNow.AddMinutes(-1));

        (await this.service.IsCurrentAsync(value)).ShouldBeFalse();
    }

    [Fact]
    public async Task CredentialCutoverMakesProtectedCookieStale()
    {
        string value = this.service.Create(
            this.user.Id, this.session.Id, this.epoch, this.clock.UtcNow.AddHours(1));
        this.boundary.IsCurrentAsync(this.epoch.ToString(), Arg.Any<CancellationToken>()).Returns(false);

        (await this.service.IsCurrentAsync(value)).ShouldBeFalse();
    }

    [Fact]
    public async Task CookieCannotBeReboundToAnotherUsersSession()
    {
        string value = this.service.Create(
            this.user.Id, this.session.Id, this.epoch, this.clock.UtcNow.AddHours(1));
        User other = User.CreateLocal("other@example.test", "Other", "fixture-hash", this.clock).Value;
        UserSession otherSession = UserSession.Create(other.Id, "127.0.0.1", "tests", this.clock).Value;
        this.sessions.GetByIdAsync(this.session.Id, Arg.Any<CancellationToken>()).Returns(otherSession);

        (await this.service.IsCurrentAsync(value)).ShouldBeFalse();
    }

    [Fact]
    public async Task RevokedSessionMakesProtectedCookieStale()
    {
        string value = this.service.Create(
            this.user.Id, this.session.Id, this.epoch, this.clock.UtcNow.AddHours(1));
        this.session.Revoke(this.clock).IsSuccess.ShouldBeTrue();

        (await this.service.IsCurrentAsync(value)).ShouldBeFalse();
    }
}
