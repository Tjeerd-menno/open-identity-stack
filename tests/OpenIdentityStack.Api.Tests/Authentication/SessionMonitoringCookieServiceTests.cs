using Microsoft.AspNetCore.DataProtection;
using OpenIdentityStack.Api.Authentication;
using OpenIdentityStack.Application.Sessions.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Users;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Authentication;

public sealed class SessionMonitoringCookieServiceTests
{
    private readonly IGetUserQueryHandler users = Substitute.For<IGetUserQueryHandler>();
    private readonly IGetSessionQueryHandler sessions = Substitute.For<IGetSessionQueryHandler>();
    private readonly IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
    private readonly SessionMonitoringCookieService service;
    private readonly User user;
    private readonly UserSession session;

    public SessionMonitoringCookieServiceTests()
    {
        DateTimeOffset now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
        this.clock.UtcNow.Returns(now);
        this.user = User.CreateLocal("monitor@example.test", "Monitor", "fixture-hash", this.clock).Value;
        this.user.VerifyEmail(this.clock).IsSuccess.ShouldBeTrue();
        this.session = UserSession.Create(this.user.Id, "127.0.0.1", "tests", this.clock).Value;
        this.users.HandleAsync(Arg.Any<GetUserQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => PersistedUser.From(this.user));
        this.sessions.HandleAsync(Arg.Any<GetSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns(call => PersistedUser.From(this.SessionFor(call.Arg<GetSessionQuery>().SessionId)));
        this.service = new SessionMonitoringCookieService(
            new EphemeralDataProtectionProvider(), this.users, this.sessions, this.clock);
    }

    [Fact]
    public async Task ProtectedCookieValidatesWithoutUpdatingSessionActivity()
    {
        string value = this.service.Create(
            this.user.Id, this.session.Id, this.clock.UtcNow.AddHours(1));

        (await this.service.IsCurrentAsync(value)).ShouldBeTrue();
        await this.sessions.Received(1).HandleAsync(Arg.Any<GetSessionQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ProtectedCookiesUseIndependentRateLimitPartitions()
    {
        string first = this.service.Create(
            this.user.Id, this.session.Id, this.clock.UtcNow.AddHours(1));
        UserSession otherSession = UserSession.Create(this.user.Id, "127.0.0.1", "tests", this.clock).Value;
        string second = this.service.Create(
            this.user.Id, otherSession.Id, this.clock.UtcNow.AddHours(1));

        this.service.GetRateLimitPartitionKey(first).ShouldBe(this.session.Id.Value.ToString("N"));
        this.service.GetRateLimitPartitionKey(second).ShouldBe(otherSession.Id.Value.ToString("N"));
    }

    [Fact]
    public void UnprotectedCookieCannotChooseRateLimitPartition()
    {
        this.service.GetRateLimitPartitionKey("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA").ShouldBeNull();
    }

    [Fact]
    public void ExpiredProtectedCookieCannotChooseRateLimitPartition()
    {
        string value = this.service.Create(
            this.user.Id, this.session.Id, this.clock.UtcNow.AddMinutes(-1));

        this.service.GetRateLimitPartitionKey(value).ShouldBeNull();
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
    public async Task UnprotectedBase64UrlCookieFailsClosed()
    {
        const string legacyValue = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

        (await this.service.IsCurrentAsync(legacyValue)).ShouldBeFalse();
    }

    [Fact]
    public async Task TamperedCookieFailsClosed()
    {
        string value = this.service.Create(
            this.user.Id, this.session.Id, this.clock.UtcNow.AddHours(1));
        char replacement = value[^1] == 'A' ? 'B' : 'A';

        (await this.service.IsCurrentAsync(value[..^1] + replacement)).ShouldBeFalse();
    }

    [Fact]
    public async Task ExpiredCookieFailsClosed()
    {
        string value = this.service.Create(
            this.user.Id, this.session.Id, this.clock.UtcNow.AddMinutes(-1));

        (await this.service.IsCurrentAsync(value)).ShouldBeFalse();
    }

    [Fact]
    public async Task CookieCannotBeReboundToAnotherUsersSession()
    {
        string value = this.service.Create(
            this.user.Id, this.session.Id, this.clock.UtcNow.AddHours(1));
        User other = User.CreateLocal("other@example.test", "Other", "fixture-hash", this.clock).Value;
        UserSession otherSession = UserSession.Create(other.Id, "127.0.0.1", "tests", this.clock).Value;
        this.sessions.HandleAsync(Arg.Any<GetSessionQuery>(), Arg.Any<CancellationToken>())
            .Returns(PersistedUser.From(otherSession));

        (await this.service.IsCurrentAsync(value)).ShouldBeFalse();
    }

    [Fact]
    public async Task RevokedSessionMakesProtectedCookieStale()
    {
        string value = this.service.Create(
            this.user.Id, this.session.Id, this.clock.UtcNow.AddHours(1));
        this.session.Revoke(this.clock).IsSuccess.ShouldBeTrue();

        (await this.service.IsCurrentAsync(value)).ShouldBeFalse();
    }

    private UserSession SessionFor(SessionId sessionId) =>
        sessionId == this.session.Id
            ? this.session
            : throw new InvalidOperationException($"Unexpected session {sessionId}.");
}
