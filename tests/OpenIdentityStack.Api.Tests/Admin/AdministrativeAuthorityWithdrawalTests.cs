using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Api.Tests.Fixtures;
using OpenIdentityStack.Domain.Resources;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Common;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Admin;

public sealed class AdministrativeAuthorityWithdrawalTests(AppHostFixture fixture)
{
    [Fact]
    public async Task SigningInAgainWithoutAuthorityChangesDoesNotRecordAnAuthorityChange()
    {
        AuthoritySubject authority = await this.CreateHumanAsync();
        using HttpClient first = authority.Session.Client;
        int before = 0;
        string email = string.Empty;
        await fixture.ExecuteDbContextAsync(async db =>
        {
            before = await db.AuditLogEntries.CountAsync(entry => entry.Action == "AdministrativeAuthorityChanged" && entry.EntityId == authority.UserId.Value.ToString());
            email = (await db.Users.SingleAsync(user => user.Id == authority.UserId)).Email;
        });
        HumanAdministrativeSession repeated = await HumanAdministrativeSession.SignInAsync(fixture, email, "Password123!@#", ["*"]);
        using HttpClient second = repeated.Client;
        (await second.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await fixture.ExecuteDbContextAsync(async db =>
            (await db.AuditLogEntries.CountAsync(entry => entry.Action == "AdministrativeAuthorityChanged" && entry.EntityId == authority.UserId.Value.ToString())).ShouldBe(before));
    }

    [Fact]
    public async Task ConcurrentDisablementFailureDoesNotCommitASecondAuthorityAudit()
    {
        AuthoritySubject authority = await this.CreateHumanAsync();
        using HttpClient client = authority.Session.Client;
        int before = 0;
        await fixture.ExecuteDbContextAsync(async db => before = await db.AuditLogEntries.CountAsync(entry =>
            entry.Action == "AdministrativeAuthorityChanged" && entry.EntityId == authority.UserId.Value.ToString()));
        await fixture.ExecuteDbContextAsync(async stale =>
        {
            OpenIdentityStack.Domain.Users.User staleUser = await stale.Users.SingleAsync(value => value.Id == authority.UserId);
            await fixture.ExecuteDbContextAsync(async winner =>
            {
                (await winner.Users.SingleAsync(value => value.Id == authority.UserId)).Disable("withdrawal-test", CreateClock()).IsSuccess.ShouldBeTrue();
                await winner.SaveChangesAsync();
            });
            staleUser.Disable("stale-withdrawal", CreateClock()).IsSuccess.ShouldBeTrue();
            await Should.ThrowAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        });
        (await client.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await fixture.ExecuteDbContextAsync(async db =>
            (await db.AuditLogEntries.CountAsync(entry => entry.Action == "AdministrativeAuthorityChanged" && entry.EntityId == authority.UserId.Value.ToString())).ShouldBe(before + 1));
    }

    [Fact]
    public async Task AuthorityStoreFailureNeverFallsBackToPreviouslyAcceptedTokenPermissions()
    {
        using HttpClient client = await fixture.CreateAuthenticatedClientAsync($"authority-outage-{Guid.NewGuid():N}", "fixture-secret");
        (await client.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await fixture.ExecuteDbContextAsync(async db =>
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"ProtectedResources\" RENAME TO \"UnavailableResources\""));
        try
        {
            (await client.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            (await client.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }
        finally
        {
            await fixture.ExecuteDbContextAsync(async db =>
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"UnavailableResources\" RENAME TO \"ProtectedResources\""));
        }
        (await client.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommittedWithdrawalAppliesToTheSameBearerOnIndependentServingInstances(bool human)
    {
        AuthoritySubject? authority = human ? await this.CreateHumanAsync(inherited: true) : null;
        string clientId = authority?.Session.ClientId ?? $"cross-instance-{Guid.NewGuid():N}";
        using HttpClient original = authority?.Session.Client ?? await fixture.CreateAuthenticatedClientAsync(clientId, "fixture-secret");
        await using var secondInstance = new AppHostFixture();
        await secondInstance.InitializeAsync();
        using HttpClient other = secondInstance.CreateClient();
        other.DefaultRequestHeaders.Authorization = original.DefaultRequestHeaders.Authorization;
        (await original.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await other.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.OK);

        await fixture.ExecuteDbContextAsync(async db =>
        {
            if (authority is not null)
            {
                (await db.Roles.SingleAsync(value => value.Id == authority.RoleId)).RemovePermission("users:read").IsSuccess.ShouldBeTrue();
            }
            else
            {
                OpenIdentityStack.Domain.Applications.Application application = await db.Applications.SingleAsync(value => value.ClientId == clientId);
                ClientResourceGrant grant = await db.ClientResourceGrants.SingleAsync(value => value.ClientApplicationId == application.Id && value.ResourceId == ProtectedResource.AdministrativeResourceId);
                grant.Configure([], ["roles:read"]).IsSuccess.ShouldBeTrue();
            }
            await db.SaveChangesAsync();
        });
        (await original.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await other.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await other.GetAsync("/api/admin/roles")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task FailedWithdrawalPreservesAuthorityAndAuditThenRetryCommitsExactlyOnce()
    {
        AuthoritySubject authority = await this.CreateHumanAsync();
        using HttpClient client = authority.Session.Client;
        (await client.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await fixture.ExecuteDbContextAsync(async db =>
        {
            Role role = await db.Roles.SingleAsync(value => value.Id == authority.RoleId);
            role.RemovePermission("users:read").IsSuccess.ShouldBeTrue();
            Role duplicate = Role.Create(role.Name, "Duplicate fails unique constraint", null).Value;
            db.Roles.Add(duplicate);
            await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
        });
        (await client.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // A failed authority mutation is discarded. A new request reloads current state
        // and deliberately reapplies the withdrawal instead of replaying tracked objects.
        await fixture.ExecuteDbContextAsync(async db =>
        {
            Role role = await db.Roles.SingleAsync(value => value.Id == authority.RoleId);
            role.Permissions.ShouldContain("users:read");
            (await db.AuditLogEntries.AnyAsync(entry => entry.Action == "AdministrativeAuthorityChanged" && entry.EntityId == role.Id.Value.ToString())).ShouldBeFalse();
            (await db.Roles.CountAsync(value => value.Name == role.Name)).ShouldBe(1);
            role.RemovePermission("users:read").IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync();
        });
        (await client.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await fixture.ExecuteDbContextAsync(async db =>
            (await db.AuditLogEntries.CountAsync(entry => entry.Action == "AdministrativeAuthorityChanged" && entry.EntityId == authority.RoleId.Value.ToString())).ShouldBe(1));
    }

    [Fact]
    public async Task RolledBackWithdrawalDoesNotChangeAuthorityOrCommitAudit()
    {
        AuthoritySubject authority = await this.CreateHumanAsync();
        using HttpClient client = authority.Session.Client;
        await fixture.ExecuteDbContextAsync(async db =>
        {
            await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await db.Database.BeginTransactionAsync();
            (await db.Roles.SingleAsync(value => value.Id == authority.RoleId)).RemovePermission("users:read").IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        });
        (await client.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await fixture.ExecuteDbContextAsync(async db =>
            (await db.AuditLogEntries.AnyAsync(entry => entry.Action == "AdministrativeAuthorityChanged" && entry.EntityId == authority.RoleId.Value.ToString())).ShouldBeFalse());
    }

    [Fact]
    public async Task RefreshCannotRestoreWithdrawnInheritedPermission()
    {
        AuthoritySubject authority = await this.CreateHumanAsync(inherited: true);
        using HttpClient client = authority.Session.Client;
        await fixture.ExecuteDbContextAsync(async db =>
        {
            (await db.Roles.SingleAsync(value => value.Id == authority.RoleId)).RemovePermission("users:read").IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync();
        });
        (await client.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using HttpResponseMessage refresh = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["refresh_token"] = authority.Session.RefreshToken,
            ["client_id"] = authority.Session.ClientId, ["client_secret"] = authority.Session.ClientSecret,
        }));
        refresh.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode tokens = (await refresh.Content.ReadFromJsonAsync<JsonNode>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens["access_token"]!.GetValue<string>());
        (await client.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/admin/roles")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("direct-assignment")]
    [InlineData("direct-permission")]
    [InlineData("wildcard")]
    [InlineData("role-disabled")]
    [InlineData("group-permission")]
    [InlineData("group-membership")]
    [InlineData("group-mapping")]
    [InlineData("user-disabled")]
    [InlineData("client-disabled")]
    public async Task ExistingHumanBearerLosesWithdrawnAuthorityOnNextRequest(string change)
    {
        AuthoritySubject authority = await this.CreateHumanAsync(change.StartsWith("group", StringComparison.Ordinal), change == "wildcard");
        using HttpClient client = authority.Session.Client;
        string bearer = client.DefaultRequestHeaders.Authorization!.Parameter!;
        (await client.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.OK);
        string? auditId = null;
        await fixture.ExecuteDbContextAsync(async db =>
        {
            IDateTimeProvider clock = CreateClock();
            Role role = await db.Roles.SingleAsync(value => value.Id == authority.RoleId);
            auditId = role.Id.Value.ToString();
            switch (change)
            {
                case "direct-assignment":
                    db.RoleAssignments.Remove(await db.RoleAssignments.SingleAsync(value => value.UserId == authority.UserId && value.RoleId == authority.RoleId));
                    auditId = $"{authority.UserId.Value}:{authority.RoleId.Value}";
                    break;
                case "direct-permission":
                case "group-permission":
                    role.RemovePermission("users:read").IsSuccess.ShouldBeTrue();
                    break;
                case "wildcard":
                    role.RemovePermission("*").IsSuccess.ShouldBeTrue();
                    break;
                case "role-disabled":
                    role.Disable().IsSuccess.ShouldBeTrue();
                    break;
                case "group-membership":
                case "group-mapping":
                    Group group = await db.Groups.Include(value => value.Memberships).Include(value => value.Mappings).SingleAsync(value => value.Id == authority.GroupId);
                    if (change == "group-membership")
                    {
                        group.RemoveMember(authority.UserId, clock).IsSuccess.ShouldBeTrue();
                        auditId = $"{group.Id.Value}:{authority.UserId.Value}";
                    }
                    else
                    {
                        GroupMapping mapping = group.Mappings.Single();
                        auditId = db.Entry(mapping).Property<Guid>("Id").CurrentValue.ToString();
                        group.RemoveMapping(mapping, clock).IsSuccess.ShouldBeTrue();
                    }
                    break;
                case "user-disabled":
                    (await db.Users.SingleAsync(value => value.Id == authority.UserId)).Disable("withdrawal-test", clock).IsSuccess.ShouldBeTrue();
                    auditId = authority.UserId.Value.ToString();
                    break;
                case "client-disabled":
                    OpenIdentityStack.Domain.Applications.Application application = await db.Applications.SingleAsync(value => value.ClientId == authority.Session.ClientId);
                    application.Disable(clock).IsSuccess.ShouldBeTrue();
                    auditId = application.Id.Value.ToString();
                    break;
            }
            await db.SaveChangesAsync();
        });
        (await client.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        client.DefaultRequestHeaders.Authorization!.Parameter.ShouldBe(bearer);
        await fixture.ExecuteDbContextAsync(async db =>
            (await db.AuditLogEntries.AnyAsync(entry => entry.Action == "AdministrativeAuthorityChanged" && entry.EntityId == auditId &&
                (entry.Details!.Contains("Modified") || entry.Details.Contains("Deleted")))).ShouldBeTrue());
    }

    [Theory]
    [InlineData(64)]
    [InlineData(129)]
    [InlineData(255)]
    public async Task RolePermissionWithdrawalImmediatelyDeniesExistingBearerAndCommitsActorAudit(int actorLength)
    {
        string email = $"withdrawal-{Guid.NewGuid():N}@example.com";
        const string password = "Password123!@#";
        Guid userId = await fixture.CreateTestUserAsync(email, "Withdrawal subject", password);
        Role role = Role.Create($"withdrawal-role-{Guid.NewGuid():N}", "Withdrawal role", null).Value;
        role.SetPermissions(["users:read", "roles:read"]);
        await fixture.ExecuteDbContextAsync(async db =>
        {
            db.Roles.Add(role);
            db.RoleAssignments.Add(RoleAssignment.Create(new UserId(userId), role.Id, DateTimeOffset.UtcNow).Value);
            await db.SaveChangesAsync();
        });
        HumanAdministrativeSession session = await HumanAdministrativeSession.SignInAsync(fixture, email, password, ["*"]);
        using HttpClient subject = session.Client;
        string bearer = subject.DefaultRequestHeaders.Authorization!.Parameter!;
        (await subject.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.OK);

        string actorId = $"withdrawal-operator-{Guid.NewGuid():N}".PadRight(actorLength, 'x');
        using HttpClient actor = await fixture.CreateAuthenticatedClientAsync(actorId, "fixture-secret");
        using HttpResponseMessage withdrawal = await actor.DeleteAsync($"/api/admin/roles/{role.Id.Value}/permissions/users:read");
        withdrawal.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await subject.GetAsync("/api/admin/users")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await subject.GetAsync("/api/admin/roles")).StatusCode.ShouldBe(HttpStatusCode.OK);
        subject.DefaultRequestHeaders.Authorization!.Parameter.ShouldBe(bearer);
        await fixture.ExecuteDbContextAsync(async db =>
        {
            OpenIdentityStack.Infrastructure.Audit.AuditLogEntry audit = await db.AuditLogEntries.SingleAsync(entry =>
                entry.Action == "AdministrativeAuthorityChanged" && entry.EntityId == role.Id.Value.ToString());
            if (actorLength <= 128) { audit.UserId.ShouldBe(actorId); }
            else
            {
                audit.UserId.ShouldStartWith("sha256:");
                audit.UserId.Length.ShouldBe(71);
            }
            string details = audit.Details.ShouldNotBeNull();
            details.ShouldContain("Permissions");
            details.ShouldNotContain(email);
            details.ShouldNotContain(password);
            details.ShouldNotContain(bearer);
        });
    }

    [Fact]
    public async Task CurrentUserReturnsCurrentCeilingWhenTheSameBearerIsReused()
    {
        string clientId = $"current-authority-{Guid.NewGuid():N}";
        using HttpClient client = await fixture.CreateAuthenticatedClientAsync(clientId, "fixture-secret");
        string bearer = client.DefaultRequestHeaders.Authorization!.Parameter!;
        (await client.GetAsync("/api/admin/roles")).StatusCode.ShouldBe(HttpStatusCode.OK);

        await fixture.ExecuteDbContextAsync(async db =>
        {
            OpenIdentityStack.Domain.Applications.Application application = await db.Applications.SingleAsync(value => value.ClientId == clientId);
            ClientResourceGrant grant = await db.ClientResourceGrants.SingleAsync(value => value.ClientApplicationId == application.Id && value.ResourceId == ProtectedResource.AdministrativeResourceId);
            grant.Configure([], ["users:read"]).IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync();
        });

        using HttpResponseMessage response = await client.GetAsync("/api/me");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonObject currentUser = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        currentUser["permissions"]!.AsArray().Select(value => value!.GetValue<string>()).ShouldBe(["users:read"]);
        (await client.GetAsync("/api/admin/roles")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        client.DefaultRequestHeaders.Authorization!.Parameter.ShouldBe(bearer);
    }

    private async Task<AuthoritySubject> CreateHumanAsync(bool inherited = false, bool wildcard = false)
    {
        string email = $"withdrawal-{Guid.NewGuid():N}@example.com";
        const string password = "Password123!@#";
        var userId = new UserId(await fixture.CreateTestUserAsync(email, "Withdrawal subject", password));
        Role role = Role.Create($"withdrawal-role-{Guid.NewGuid():N}", "Withdrawal role", null).Value;
        role.SetPermissions(wildcard ? ["*"] : ["users:read", "roles:read"]);
        GroupId? groupId = null;
        await fixture.ExecuteDbContextAsync(async db =>
        {
            db.Roles.Add(role);
            if (inherited)
            {
                IDateTimeProvider clock = CreateClock();
                Group group = Group.Create($"withdrawal-group-{Guid.NewGuid():N}", null, clock).Value;
                group.AddMember(userId, userId, clock).IsSuccess.ShouldBeTrue();
                group.AddMapping(MappingType.Role, role.Id.Value.ToString(), null, TokenTarget.AccessToken, clock).IsSuccess.ShouldBeTrue();
                groupId = group.Id;
                db.Groups.Add(group);
            }
            else
            {
                db.RoleAssignments.Add(RoleAssignment.Create(userId, role.Id, DateTimeOffset.UtcNow).Value);
            }
            await db.SaveChangesAsync();
        });
        HumanAdministrativeSession session = await HumanAdministrativeSession.SignInAsync(fixture, email, password, ["*"]);
        return new(session, userId, role.Id, groupId);
    }

    private static IDateTimeProvider CreateClock()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        return clock;
    }

    private sealed record AuthoritySubject(HumanAdministrativeSession Session, UserId UserId, RoleId RoleId, GroupId? GroupId);
}
