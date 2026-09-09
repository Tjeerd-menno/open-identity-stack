using System.Net;
using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Api.Tests.Fixtures;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Sessions;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Admin;

public sealed class SessionRevocationTests
{
    [Fact]
    public async Task PersistedSessionRevocationClearsMonitoringCookieOnCheckSessionRequest()
    {
        await using var fixture = new AppHostFixture($"monitoring-{Guid.NewGuid():N}");
        await fixture.InitializeAsync();
        string email = $"monitoring-{Guid.NewGuid():N}@example.test";
        const string password = "Password123!@#";
        Guid userId = await fixture.CreateTestUserAsync(email, "Session monitor", password);
        await fixture.ExecuteDbContextAsync(async db =>
        {
            Role role = Role.Create("session-monitor", null).Value;
            role.SetPermissions(["users:read"]);
            db.Roles.Add(role);
            db.RoleAssignments.Add(RoleAssignment.Create(new UserId(userId), role.Id, DateTimeOffset.UtcNow).Value);
            await db.SaveChangesAsync();
        });
        HumanAdministrativeSession authenticated = await HumanAdministrativeSession.SignInAsync(
            fixture, email, password, ["users:read"]);
        using HttpClient browser = authenticated.Client;
        (await browser.GetAsync("/connect/check_session")).StatusCode.ShouldBe(HttpStatusCode.OK);
        using HttpClient thirdPartyIframe = fixture.CreateClient(allowAutoRedirect: false);
        thirdPartyIframe.DefaultRequestHeaders.Add("Cookie", $"op_session={authenticated.MonitoringCookie}");
        HttpResponseMessage initiallyCurrent = await thirdPartyIframe.GetAsync("/connect/check_session");
        initiallyCurrent.StatusCode.ShouldBe(HttpStatusCode.OK);
        initiallyCurrent.Headers.TryGetValues("Set-Cookie", out _).ShouldBeFalse();
        await fixture.ExecuteDbContextAsync(async db =>
        {
            IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
            clock.UtcNow.Returns(DateTimeOffset.UtcNow);
            foreach (UserSession session in await db.UserSessions.Where(value => value.UserId == new UserId(userId)).ToListAsync())
            {
                session.Revoke(clock);
            }
            await db.SaveChangesAsync();
        });

        AssertMonitoringCookieCleared(await thirdPartyIframe.GetAsync("/connect/check_session"));
    }

    [Fact]
    public async Task RemovedSessionRejectsItsAlreadyIssuedRefreshToken()
    {
        await using var fixture = new AppHostFixture($"removed-session-{Guid.NewGuid():N}");
        await fixture.InitializeAsync();
        string email = $"session-{Guid.NewGuid():N}@example.test";
        const string password = "Password123!@#";
        Guid userId = await fixture.CreateTestUserAsync(email, "Session test", password);
        await fixture.ExecuteDbContextAsync(async db =>
        {
            Role role = Role.Create("session-reader", null).Value;
            role.SetPermissions(["users:read"]);
            db.Roles.Add(role);
            db.RoleAssignments.Add(RoleAssignment.Create(new UserId(userId), role.Id, DateTimeOffset.UtcNow).Value);
            await db.SaveChangesAsync();
        });
        HumanAdministrativeSession session = await HumanAdministrativeSession.SignInAsync(fixture, email, password, ["users:read"]);
        using HttpClient client = session.Client;
        await fixture.ExecuteDbContextAsync(async db =>
        {
            db.UserSessions.RemoveRange(await db.UserSessions.Where(candidate => candidate.UserId == new UserId(userId)).ToListAsync());
            await db.SaveChangesAsync();
        });
        HttpResponseMessage response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["refresh_token"] = session.RefreshToken,
            ["client_id"] = session.ClientId, ["client_secret"] = session.ClientSecret
        }));
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static void AssertMonitoringCookieCleared(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("op_session=", StringComparison.Ordinal));
        cookie.ShouldContain("op_session=;");
        cookie.ShouldContain("expires=Thu, 01 Jan 1970");
        cookie.ShouldContain("path=/");
        cookie.ShouldContain("secure");
        cookie.ShouldContain("samesite=none");
    }
}
