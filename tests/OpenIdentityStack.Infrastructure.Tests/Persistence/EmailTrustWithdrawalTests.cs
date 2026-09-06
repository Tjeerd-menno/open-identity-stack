using System.Security.Claims;
using OpenIddict.Server;
using OpenIddict.Validation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Audit;
using OpenIdentityStack.Infrastructure.Identity;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Federation;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class EmailTrustWithdrawalTests
{
    [Theory]
    [InlineData("none", true)]
    [InlineData("local", false)]
    [InlineData("provider", false)]
    [InlineData("old-address", true)]
    public async Task WithdrawalRevokesOnlyCredentialsLosingSufficientEvidence(string alternative, bool revoked)
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        (UpstreamProviderId providerId, UserId userId) = await database.SeedAsync(alternative);
        await using (AsyncServiceScope scope = database.Services.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<ProviderEmailTrustStore>()
                .SetAsync(providerId, false, "operator", default)).IsSuccess.ShouldBeTrue();
        }

        await database.AssertStateAsync(providerId, userId, withdrawn: true, revoked);
        await using (AsyncServiceScope scope = database.Services.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<ProviderEmailTrustStore>()
                .SetAsync(providerId, false, "operator", default)).IsSuccess.ShouldBeTrue();
        }
        await database.AssertStateAsync(providerId, userId, withdrawn: true, revoked);
    }

    [Fact]
    public async Task FailureAfterRevocationAndAuditRollsEverythingBackAndFreshRetryCommits()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        (UpstreamProviderId providerId, UserId userId) = await database.SeedAsync("none");
        database.FailAfterAudit = true;
        await using (AsyncServiceScope scope = database.Services.CreateAsyncScope())
        {
            await Should.ThrowAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<ProviderEmailTrustStore>()
                .SetAsync(providerId, false, "operator", default));
        }
        await database.AssertStateAsync(providerId, userId, withdrawn: false, revoked: false);
        await using (AsyncServiceScope scope = database.Services.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<OpenIdentityStackDbContext>().AuditLogEntries.CountAsync()).ShouldBe(0);
        }

        database.FailAfterAudit = false;
        await using (AsyncServiceScope scope = database.Services.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<ProviderEmailTrustStore>()
                .SetAsync(providerId, false, "operator", default)).IsSuccess.ShouldBeTrue();
        }
        await database.AssertStateAsync(providerId, userId, withdrawn: true, revoked: true);
        await using (AsyncServiceScope scope = database.Services.CreateAsyncScope())
        {
            (await scope.ServiceProvider.GetRequiredService<OpenIdentityStackDbContext>().AuditLogEntries.CountAsync()).ShouldBeGreaterThan(0);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("malformed")]
    public async Task StaleCredentialsAreRejectedOnBothValidationPathsEvenWithPreviouslyTrackedUser(string? captured)
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        (UpstreamProviderId providerId, UserId userId) = await database.SeedAsync("none");
        await using AsyncServiceScope staleScope = database.Services.CreateAsyncScope();
        OpenIdentityStackDbContext stale = staleScope.ServiceProvider.GetRequiredService<OpenIdentityStackDbContext>();
        User oldProjection = await stale.Users.SingleAsync(u => u.Id == userId);
        oldProjection.CredentialRevision.ShouldBe(Guid.Empty);
        await using (AsyncServiceScope scope = database.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ProviderEmailTrustStore>().SetAsync(providerId, false, "operator", default);
        }

        // Represents a credential minted from the old projection after revocation committed.
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId.Value.ToString())]));
        principal.SetClaim("ois_credential_revision", captured);
        var server = new OpenIddictServerEvents.ValidateTokenContext(new OpenIddictServerTransaction()) { Principal = principal };
        var local = new OpenIddictValidationEvents.ValidateTokenContext(new OpenIddictValidationTransaction()) { Principal = principal };
        var validator = new UserCredentialRevisionValidation(stale);
        await validator.HandleAsync(server);
        await validator.HandleAsync(local);
        server.IsRejected.ShouldBeTrue();
        local.IsRejected.ShouldBeTrue();

        // Reauthentication reads the committed state and can obtain corrected credentials.
        Guid current = await stale.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.CredentialRevision).SingleAsync();
        principal.SetClaim("ois_credential_revision", current.ToString());
        var fresh = new OpenIddictServerEvents.ValidateTokenContext(new OpenIddictServerTransaction()) { Principal = principal };
        await validator.HandleAsync(fresh);
        fresh.IsRejected.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true, true, false, false, true)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, true, false, false, false)]
    [InlineData(true, true, false, true, false)]
    public async Task ApplicationExemptionRequiresProtectedMachineEvidenceAndNeverOverridesUserRevision(
        bool applicationMarker, bool matchingClient, bool userRevision, bool duplicateMarker, bool accepted)
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        (UpstreamProviderId providerId, UserId userId) = await database.SeedAsync("none");
        await using AsyncServiceScope scope = database.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ProviderEmailTrustStore>().SetAsync(providerId, false, "operator", default);
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", userId.Value.ToString()),
            new Claim("client_id", matchingClient ? userId.Value.ToString() : "another-client")]));
        if (applicationMarker) { principal.SetClaim("ois.subject_kind", "application"); }
        if (duplicateMarker) { ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim("ois.subject_kind", "application")); }
        if (userRevision) { principal.SetClaim("ois_credential_revision", Guid.Empty.ToString()); }
        var validator = new UserCredentialRevisionValidation(scope.ServiceProvider.GetRequiredService<OpenIdentityStackDbContext>());
        var server = new OpenIddictServerEvents.ValidateTokenContext(new OpenIddictServerTransaction()) { Principal = principal };
        var local = new OpenIddictValidationEvents.ValidateTokenContext(new OpenIddictValidationTransaction()) { Principal = principal };

        await validator.HandleAsync(server);
        await validator.HandleAsync(local);

        server.IsRejected.ShouldBe(!accepted);
        local.IsRejected.ShouldBe(!accepted);
    }

    [Fact]
    public async Task ConcurrentWithdrawalsCannotBothRelyOnTheOtherProvidersEvidence()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        (UpstreamProviderId providerId, UserId userId) = await database.SeedAsync("provider");
        await using AsyncServiceScope firstScope = database.Services.CreateAsyncScope();
        await using AsyncServiceScope secondScope = database.Services.CreateAsyncScope();
        OpenIdentityStackDbContext first = firstScope.ServiceProvider.GetRequiredService<OpenIdentityStackDbContext>();
        OpenIdentityStackDbContext second = secondScope.ServiceProvider.GetRequiredService<OpenIdentityStackDbContext>();
        User firstView = await first.Users.SingleAsync(u => u.Id == userId);
        User secondView = await second.Users.SingleAsync(u => u.Id == userId);
        Guid otherProvider = secondView.EmailVerificationEvidence.Single(e => e.ProviderId != providerId.Value).ProviderId!.Value;
        firstView.WithdrawProviderEmailVerification(providerId.Value, DateTimeOffset.UtcNow).ShouldBeFalse();
        secondView.WithdrawProviderEmailVerification(otherProvider, DateTimeOffset.UtcNow).ShouldBeFalse();
        await first.SaveChangesAsync();
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        await using AsyncServiceScope retryScope = database.Services.CreateAsyncScope();
        OpenIdentityStackDbContext retry = retryScope.ServiceProvider.GetRequiredService<OpenIdentityStackDbContext>();
        User current = await retry.Users.SingleAsync(u => u.Id == userId);
        current.WithdrawProviderEmailVerification(otherProvider, DateTimeOffset.UtcNow).ShouldBeTrue();
        await retry.SaveChangesAsync();
        current.EmailVerified.ShouldBeFalse();
        current.CredentialRevision.ShouldNotBe(Guid.Empty);
    }
    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("DataSource=:memory:");
        private readonly IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        public ServiceProvider Services { get; private set; } = null!;
        public bool FailAfterAudit { get; set; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var database = new TestDatabase();
            database.clock.UtcNow.Returns(DateTimeOffset.UtcNow);
            await database.connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(database.clock);
            services.AddDbContext<OpenIdentityStackDbContext>(options => options.UseSqlite(database.connection).UseOpenIddict());
            services.AddOpenIddict().AddCore(options => options.UseEntityFrameworkCore().UseDbContext<OpenIdentityStackDbContext>());
            services.AddScoped<AuditLogService>();
            services.AddScoped<IAuditLog>(sp =>
            {
                IAuditLog audit = Substitute.For<IAuditLog>();
                audit.LogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
                    .Returns(async call =>
                    {
                        await sp.GetRequiredService<AuditLogService>().LogAsync(call.ArgAt<string>(0), call.ArgAt<string>(1),
                            call.ArgAt<string>(2), call.ArgAt<string>(3), call.ArgAt<string?>(4), call.ArgAt<CancellationToken>(5));
                        if (database.FailAfterAudit)
                        {
                            throw new InvalidOperationException("Injected failure after durable work.");
                        }
                    });
                return audit;
            });
            services.AddScoped<ProviderEmailTrustStore>();
            services.AddScoped<IEmailTrustCredentialInvalidator, EmailTrustCredentialInvalidator>();
            database.Services = services.BuildServiceProvider();
            await using AsyncServiceScope scope = database.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<OpenIdentityStackDbContext>().Database.EnsureCreatedAsync();
            return database;
        }

        public async Task<(UpstreamProviderId, UserId)> SeedAsync(string alternative)
        {
            await using AsyncServiceScope scope = this.Services.CreateAsyncScope();
            OpenIdentityStackDbContext db = scope.ServiceProvider.GetRequiredService<OpenIdentityStackDbContext>();
            UpstreamProvider provider = UpstreamProvider.Create("provider", "Provider", "https://issuer.example", "client").Value;
            provider.SetEmailVerificationTrust(true);
            User user = alternative is "local" or "old-address"
                ? User.CreateLocal("person@example.com", "Person", "hash", this.clock).Value
                : User.CreateFederated("person@example.com", "Person", this.clock).Value;
            user.RecordProviderEmailVerification(provider, "https://issuer.example", user.Email, true, this.clock.UtcNow);
            if (alternative == "local")
            {
                user.VerifyEmail(this.clock).IsSuccess.ShouldBeTrue();
            }
            else if (alternative == "provider")
            {
                UpstreamProvider other = UpstreamProvider.Create("other", "Other", "https://other.example", "client").Value;
                other.SetEmailVerificationTrust(true);
                user.RecordProviderEmailVerification(other, "https://other.example", user.Email, true, this.clock.UtcNow);
                db.Add(other);
            }
            else if (alternative == "old-address")
            {
                db.Entry(user).Property(u => u.Email).CurrentValue = "new@example.com";
                db.Entry(user).Property(u => u.NormalizedEmail).CurrentValue = "NEW@EXAMPLE.COM";
                user.VerifyEmail(this.clock).IsSuccess.ShouldBeTrue();
            }
            db.Add(provider);
            db.Add(user);
            db.Add(UserSession.Create(user.Id, "127.0.0.1", "test", this.clock).Value);
            await db.SaveChangesAsync();
            IOpenIddictAuthorizationManager authorizations = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();
            object authorization = await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
            {
                Subject = user.Id.Value.ToString(), Status = OpenIddictConstants.Statuses.Valid,
                Type = OpenIddictConstants.AuthorizationTypes.Permanent
            });
            IOpenIddictTokenManager tokens = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
            foreach (string type in new[] { OpenIddictConstants.TokenTypeHints.AccessToken, OpenIddictConstants.TokenTypeHints.RefreshToken, "authorization_code" })
            {
                await tokens.CreateAsync(new OpenIddictTokenDescriptor
                {
                    Subject = user.Id.Value.ToString(), Status = OpenIddictConstants.Statuses.Valid,
                    Type = type, AuthorizationId = await authorizations.GetIdAsync(authorization)
                });
            }
            return (provider.Id, user.Id);
        }

        public async Task AssertStateAsync(UpstreamProviderId providerId, UserId userId, bool withdrawn, bool revoked)
        {
            await using AsyncServiceScope scope = this.Services.CreateAsyncScope();
            OpenIdentityStackDbContext db = scope.ServiceProvider.GetRequiredService<OpenIdentityStackDbContext>();
            (await db.UpstreamProviders.SingleAsync(p => p.Id == providerId)).TrustEmailVerification.ShouldBe(!withdrawn);
            User user = await db.Users.SingleAsync(u => u.Id == userId);
            (user.CredentialRevision != Guid.Empty).ShouldBe(revoked);
            (user.EmailVerificationEvidence.Single(e => e.ProviderId == providerId.Value).WithdrawnAt is not null).ShouldBe(withdrawn);
            (await db.UserSessions.SingleAsync(s => s.UserId == userId)).Status.ShouldBe(revoked ? SessionStatus.Revoked : SessionStatus.Active);
            string status = revoked ? OpenIddictConstants.Statuses.Revoked : OpenIddictConstants.Statuses.Valid;
            (await db.Set<OpenIddictEntityFrameworkCoreAuthorization>().SingleAsync()).Status.ShouldBe(status);
            List<OpenIddictEntityFrameworkCoreToken> tokens = await db.Set<OpenIddictEntityFrameworkCoreToken>().ToListAsync();
            tokens.Count.ShouldBe(3);
            tokens.ShouldAllBe(t => t.Status == status);
        }

        public async ValueTask DisposeAsync()
        {
            await this.Services.DisposeAsync();
            await this.connection.DisposeAsync();
        }
    }
}
