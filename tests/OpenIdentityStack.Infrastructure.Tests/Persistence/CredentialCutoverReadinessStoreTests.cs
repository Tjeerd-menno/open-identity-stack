using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Persistence;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class CredentialCutoverReadinessStoreTests
{
    [Theory]
    [InlineData(30, 15, true)]
    [InlineData(300, 299, true)]
    [InlineData(0, 0, true)]
    [InlineData(301, 20, false)]
    [InlineData(-1, 0, false)]
    [InlineData(30, 31, false)]
    [InlineData(30, -1, false)]
    public async Task EmergencyProofUsesOneFreshnessWindowForAuthenticationAndSessionCreation(int authenticationAgeSeconds, int sessionDelaySeconds, bool accepted)
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        DateTimeOffset now = database.Clock.UtcNow;
        DateTimeOffset authenticatedAt = now.AddSeconds(-authenticationAgeSeconds);
        database.Clock.UtcNow.Returns(authenticatedAt.AddSeconds(sessionDelaySeconds));
        AdministrativeActor actor = await database.SeedEmergencyAsync();
        database.Clock.UtcNow.Returns(now);

        Result<EmergencyAccessEvidence> result = await database.Store.RecordEmergencyAccessAsync(actor with { AuthenticatedAt = authenticatedAt });

        result.IsSuccess.ShouldBe(accepted);
        (await database.Db.EmergencyAccessEvidence.CountAsync()).ShouldBe(accepted ? 1 : 0);
        (await database.Store.EvaluateAsync()).Ready.ShouldBe(accepted);
    }

    [Fact]
    public async Task FreshSessionMustBelongToTheAuthenticatedEmergencyUser()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        AdministrativeActor actor = await database.SeedEmergencyAsync();
        User other = User.CreateLocal("other@example.com", "Other", "hash", database.Clock).Value;
        database.Db.Add(other);
        UserSession session = UserSession.Create(other.Id, "127.0.0.1", "test", database.Clock).Value;
        database.Db.Add(session);
        await database.Db.SaveChangesAsync();

        (await database.Store.RecordEmergencyAccessAsync(actor with { LocalPasswordSessionId = session.Id.Value })).IsFailure.ShouldBeTrue();
        (await database.Db.EmergencyAccessEvidence.CountAsync()).ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QuarantinedAssociationsBlockEvenWithConfiguredPasswordAndTestedEmergencyAccess(bool passwordCandidate)
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        AdministrativeActor actor = await database.SeedEmergencyAsync();
        (await database.Store.RecordEmergencyAccessAsync(actor)).IsSuccess.ShouldBeTrue();
        UpstreamProvider provider = UpstreamProvider.Create("legacy", "Legacy", "https://issuer.example", "client").Value;
        User legacy = passwordCandidate
            ? User.CreateLocal("legacy@example.com", "Legacy", "hash", database.Clock).Value
            : User.CreateFederated("legacy@example.com", "Legacy", database.Clock).Value;
        legacy.LinkUpstreamIdentity(provider.Id, provider.Name, "historical-subject", legacy.Email, "https://issuer.example");
        database.Db.Add(provider);
        database.Db.Add(legacy);
        await database.Db.SaveChangesAsync();

        CredentialCutoverPreflight first = await database.Store.EvaluateAsync();
        CredentialCutoverPreflight second = await database.Store.EvaluateAsync();

        first.Ready.ShouldBeFalse();
        first.Identities.QuarantinedLinks.ShouldBe(1);
        first.Identities.PasswordCandidates.ShouldBe(passwordCandidate ? 1 : 0);
        first.Identities.FederationOnlyUsers.ShouldBe(passwordCandidate ? 0 : 1);
        first.EmergencyAccess!.CurrentlyUsable.ShouldBeTrue();
        first.Blockers.ShouldContain(x => x.Code == "Identity.Quarantined");
        second.Identities.ShouldBe(first.Identities);
        (await database.Db.Users.SingleAsync(u => u.Id == legacy.Id)).UpstreamIdentities.Single().IsQuarantined.ShouldBeTrue();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task RoleNameOrDisabledPasswordUserCannotEstablishEmergencyAccess(bool wildcard, bool disabled)
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        AdministrativeActor actor = await database.SeedEmergencyAsync(wildcard, disabled);

        (await database.Store.RecordEmergencyAccessAsync(actor)).IsFailure.ShouldBeTrue();

        (await database.Db.EmergencyAccessEvidence.CountAsync()).ShouldBe(0);
        (await database.Store.EvaluateAsync()).Ready.ShouldBeFalse();
        (await database.Db.Users.SingleAsync(u => u.Id == actor.UserId)).Status.ShouldBe(disabled ? UserStatus.Disabled : UserStatus.Active);
    }

    [Fact]
    public async Task ProofExpiresAndCurrentAuthorityWithdrawalIsObservedDespiteTrackedRole()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        AdministrativeActor actor = await database.SeedEmergencyAsync();
        (await database.Store.RecordEmergencyAccessAsync(actor)).IsSuccess.ShouldBeTrue();
        (await database.Store.EvaluateAsync()).Ready.ShouldBeTrue();
        await using OpenIdentityStackDbContext other = database.CreateContext();
        Role role = await other.Roles.SingleAsync();
        role.SetPermissions([]);
        await other.SaveChangesAsync();

        (await database.Store.EvaluateAsync()).EmergencyAccess!.CurrentlyUsable.ShouldBeFalse();
        (await database.Store.EvaluateAsync()).Blockers.ShouldContain(x => x.Code == "Emergency.IndependentAccessRequired");
        role.SetPermissions(["*"]);
        await other.SaveChangesAsync();
        database.Clock.UtcNow.Returns(actor.AuthenticatedAt!.Value.AddMinutes(6));
        (await database.Store.EvaluateAsync()).Ready.ShouldBeFalse();
    }

    [Fact]
    public async Task MissingOrRevokedSessionCannotBeUsedAsIndependentProof()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        AdministrativeActor actor = await database.SeedEmergencyAsync();
        (await database.Store.RecordEmergencyAccessAsync(actor with { LocalPasswordSessionId = Guid.NewGuid() })).IsFailure.ShouldBeTrue();
        UserSession session = await database.Db.UserSessions.SingleAsync();
        session.Revoke(database.Clock);
        await database.Db.SaveChangesAsync();
        (await database.Store.RecordEmergencyAccessAsync(actor)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task ExternalWindowReviewIsExplicitBoundToResourceRevisionAndCannotHideUnknownExpiry()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        AdministrativeActor actor = await database.SeedEmergencyAsync();
        (await database.Store.RecordEmergencyAccessAsync(actor)).IsSuccess.ShouldBeTrue();
        var resource = new CutoverProtectedResource(Guid.NewGuid(), "Business", "urn:business", "business", 1);
        database.Resources.ReadAsync(Arg.Any<CancellationToken>()).Returns(new CutoverResourceInventory([], [resource], []));
        (await database.Store.EvaluateAsync()).Blockers.ShouldContain(x => x.Code == "Resource.TokenWindowUnresolved");
        await database.Store.ReviewResourceWindowAsync(new(resource.Id, "OnlineIntrospection", 30, "https://change.example/rehearsal/1"), actor.UserId.Value.ToString());
        CredentialCutoverPreflight reviewed = await database.Store.EvaluateAsync();
        reviewed.Ready.ShouldBeTrue();
        reviewed.BusinessResources.Single().ResidualSeconds.ShouldBe(30);
        database.Resources.ReadAsync(Arg.Any<CancellationToken>()).Returns(new CutoverResourceInventory([], [resource with { Revision = 2 }], []));
        (await database.Store.EvaluateAsync()).Ready.ShouldBeFalse();
        await database.Store.ReviewResourceWindowAsync(new(resource.Id, "OfflineExpiry", 3600, "https://change.example/rehearsal/2"), actor.UserId.Value.ToString());
        database.Db.Add(new OpenIddictEntityFrameworkCoreToken { Id = Guid.NewGuid().ToString(), Type = "access_token", Status = "revoked", ExpirationDate = null });
        await database.Db.SaveChangesAsync();
        (await database.Store.EvaluateAsync()).Ready.ShouldBeFalse();
    }

    [Theory]
    [InlineData(OpenIddict.Abstractions.OpenIddictConstants.TokenTypeIdentifiers.AccessToken, false)]
    [InlineData(OpenIddict.Abstractions.OpenIddictConstants.TokenTypeIdentifiers.AccessToken, true)]
    [InlineData(OpenIddict.Abstractions.OpenIddictConstants.TokenTypeHints.AccessToken, false)]
    [InlineData(OpenIddict.Abstractions.OpenIddictConstants.TokenTypeHints.AccessToken, true)]
    public async Task OfflineWindowIncludesCurrentAndLegacyRevokedAccessTokenMetadata(string tokenType, bool unknownExpiry)
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        AdministrativeActor actor = await database.SeedEmergencyAsync();
        (await database.Store.RecordEmergencyAccessAsync(actor)).IsSuccess.ShouldBeTrue();
        var resource = new CutoverProtectedResource(Guid.NewGuid(), "Business", "urn:business", "business", 1);
        database.Resources.ReadAsync(Arg.Any<CancellationToken>()).Returns(new CutoverResourceInventory([], [resource], []));
        database.Db.Add(new OpenIddictEntityFrameworkCoreToken
        {
            Id = Guid.NewGuid().ToString(), Type = tokenType, Status = "revoked",
            ExpirationDate = unknownExpiry ? null : database.Clock.UtcNow.AddHours(1).UtcDateTime
        });
        await database.Db.SaveChangesAsync();
        await database.Store.ReviewResourceWindowAsync(new(resource.Id, "OfflineExpiry", 0, "fixture:zero-window"), actor.UserId.Value.ToString());
        CredentialCutoverPreflight rejected = await database.Store.EvaluateAsync();
        rejected.OutstandingAccessTokens.ShouldBe(1);
        rejected.BusinessResources.Single().Reviewed.ShouldBeFalse();
        rejected.Ready.ShouldBeFalse();
        rejected.Blockers.ShouldContain(x => x.Code == "Resource.TokenWindowUnresolved");
        database.Clock.UtcNow.Returns(database.Clock.UtcNow.AddSeconds(1));
        await database.Store.ReviewResourceWindowAsync(new(resource.Id, "OfflineExpiry", 7200, "fixture:measured-window"), actor.UserId.Value.ToString());
        CredentialCutoverPreflight bounded = await database.Store.EvaluateAsync();
        bounded.BusinessResources.Single().Reviewed.ShouldBe(!unknownExpiry);
        bounded.Ready.ShouldBe(!unknownExpiry);
    }

    [Fact]
    public async Task ReviewsAtTheSameInstantRequireAnUnambiguousLaterReview()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        AdministrativeActor actor = await database.SeedEmergencyAsync();
        (await database.Store.RecordEmergencyAccessAsync(actor)).IsSuccess.ShouldBeTrue();
        var resource = new CutoverProtectedResource(Guid.NewGuid(), "Business", "urn:business", "business", 1);
        database.Resources.ReadAsync(Arg.Any<CancellationToken>()).Returns(new CutoverResourceInventory([], [resource], []));
        await database.Store.ReviewResourceWindowAsync(new(resource.Id, "OnlineIntrospection", 60, "fixture:first"), actor.UserId.Value.ToString());
        await database.Store.ReviewResourceWindowAsync(new(resource.Id, "OnlineIntrospection", 30, "fixture:second"), actor.UserId.Value.ToString());

        CredentialCutoverPreflight ambiguous = await database.Store.EvaluateAsync();
        ambiguous.Ready.ShouldBeFalse();
        ambiguous.BusinessResources.Single().Reviewed.ShouldBeFalse();
        ambiguous.Blockers.ShouldContain(x => x.Code == "Resource.TokenWindowUnresolved");

        database.Clock.UtcNow.Returns(database.Clock.UtcNow.AddSeconds(1));
        await database.Store.ReviewResourceWindowAsync(new(resource.Id, "OnlineIntrospection", 30, "fixture:resolved"), actor.UserId.Value.ToString());
        CredentialCutoverPreflight resolved = await database.Store.EvaluateAsync();
        resolved.Ready.ShouldBeTrue();
        resolved.BusinessResources.Single().EvidenceReference.ShouldBe("fixture:resolved");
    }
    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("DataSource=:memory:");
        private DbContextOptions<OpenIdentityStackDbContext> options = null!;
        public IDateTimeProvider Clock { get; } = Substitute.For<IDateTimeProvider>();
        public ICredentialCutoverResourceInventory Resources { get; } = Substitute.For<ICredentialCutoverResourceInventory>();
        public OpenIdentityStackDbContext Db { get; private set; } = null!;
        public CredentialCutoverReadinessStore Store => new(this.Db, this.Resources, this.Clock);

        public static async Task<TestDatabase> CreateAsync()
        {
            var database = new TestDatabase();
            await database.connection.OpenAsync();
            database.options = new DbContextOptionsBuilder<OpenIdentityStackDbContext>().UseSqlite(database.connection).UseOpenIddict().Options;
            database.Db = database.CreateContext();
            await database.Db.Database.EnsureCreatedAsync();
            database.Clock.UtcNow.Returns(DateTimeOffset.UtcNow);
            database.Resources.ReadAsync(Arg.Any<CancellationToken>()).Returns(new CutoverResourceInventory([], [], []));
            return database;
        }

        public OpenIdentityStackDbContext CreateContext() => new(this.options);

        public async Task<AdministrativeActor> SeedEmergencyAsync(bool wildcard = true, bool disabled = false)
        {
            User user = User.CreateLocal("emergency@example.com", "Emergency", "hash", this.Clock).Value;
            user.VerifyEmail(this.Clock);
            if (disabled) { user.Disable("Local administrative decision", this.Clock); }
            Role role = Role.Create("admin", "Admin", null).Value;
            role.SetPermissions(wildcard ? ["*"] : []);
            this.Db.Add(user);
            this.Db.Add(role);
            this.Db.Add(RoleAssignment.Create(user.Id, role.Id, this.Clock.UtcNow).Value);
            UserSession session = UserSession.Create(user.Id, "127.0.0.1", "test", this.Clock).Value;
            this.Db.Add(session);
            await this.Db.SaveChangesAsync();
            return new(user.Id, this.Clock.UtcNow, true, true, session.Id.Value, Guid.Empty);
        }

        public async ValueTask DisposeAsync()
        {
            await this.Db.DisposeAsync();
            await this.connection.DisposeAsync();
        }
    }
}
