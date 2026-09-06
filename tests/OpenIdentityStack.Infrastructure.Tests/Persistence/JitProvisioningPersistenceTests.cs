using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Federation.Commands;
using OpenIdentityStack.Domain.Federation;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Audit;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Persistence.Federation;
using OpenIdentityStack.Infrastructure.Persistence.Users;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class JitProvisioningPersistenceTests
{
    [Fact]
    public async Task DisabledProviderDenialPersistsWithoutSubjectOrEmailData()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync(bindIssuer: false);
        await using OpenIdentityStackDbContext attempt = database.CreateContext();
        UpstreamProvider provider = await attempt.UpstreamProviders.SingleAsync();
        provider.Disable();
        await attempt.SaveChangesAsync();

        Result<JitProvisionUserResult> result = await CreateUseCase(attempt).ExecuteAsync(database.Command("private-subject", "private-email@example.com"));

        result.Error.ShouldBe(UpstreamProviderErrors.ProviderDisabled);
        await using OpenIdentityStackDbContext read = database.CreateContext();
        AuditLogEntry entry = await read.AuditLogEntries.SingleAsync();
        entry.Action.ShouldBe("Federation.AccountAssociationDenied");
        entry.UserId.ShouldBe("federation");
        entry.EntityId.ShouldBe(provider.Id.Value.ToString());
        entry.Details.ShouldBe("Upstream provider is disabled.");
        (await read.Users.CountAsync()).ShouldBe(0);
        (await read.UpstreamProviders.SingleAsync()).BoundIssuer.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangedProviderPolicyDeniesStaleCreationWithoutPersistingAccount(bool disableProvider)
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using OpenIdentityStackDbContext attempt = database.CreateContext();
        await using OpenIdentityStackDbContext administrator = database.CreateContext();
        UpstreamProvider stale = await attempt.UpstreamProviders.SingleAsync();
        User user = User.CreateFederated("person@example.com", "Person", new TestClock()).Value;
        user.LinkUpstreamIdentity(stale.Id, stale.Name, "subject", user.Email);
        attempt.Users.Add(user);
        UpstreamProvider current = await administrator.UpstreamProviders.SingleAsync();
        if (disableProvider)
        {
            current.Disable();
        }
        else
        {
            current.SetJitProvisioningEnabled(false);
        }
        await administrator.SaveChangesAsync();

        var persistence = new JitProvisioningPersistence(attempt,
            new AuditLogService(NullLogger<AuditLogService>.Instance, attempt, new TestClock()));
        Result result = await persistence.CommitAsync(user.Id, stale.Id, true);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DomainError.Forbidden("Federation.AuthenticationFailed", "Unable to complete sign-in."));
        await using OpenIdentityStackDbContext read = database.CreateContext();
        (await read.Users.CountAsync()).ShouldBe(0);
        (await read.AuditLogEntries.CountAsync(entry => entry.Action == "Federation.AccountAssociationDenied")).ShouldBe(1);
    }

    [Fact]
    public async Task ExistingUserAuthenticationIsDeniedWhenProviderIsDisabledAfterInitialValidation()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using (OpenIdentityStackDbContext setup = database.CreateContext())
        {
            UpstreamProvider provider = await setup.UpstreamProviders.SingleAsync();
            User user = User.CreateFederated(
                "person@example.com",
                "Person",
                provider.Id,
                provider.Name,
                "subject",
                issuer: "https://issuer.example").Value;
            setup.Users.Add(user);
            await setup.SaveChangesAsync();
        }

        await using OpenIdentityStackDbContext attempt = database.CreateContext();
        await using OpenIdentityStackDbContext administrator = database.CreateContext();
        var realUsers = new UserRepository(attempt);
        IUserRepository users = Substitute.For<IUserRepository>();
        users.FindByUpstreamIdentityAsync(Arg.Any<UpstreamProviderId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                User? existing = await realUsers.FindByUpstreamIdentityAsync(
                    call.ArgAt<UpstreamProviderId>(0),
                    call.ArgAt<string>(1),
                    call.ArgAt<CancellationToken>(2));
                UpstreamProvider current = await administrator.UpstreamProviders.SingleAsync();
                current.Disable();
                await administrator.SaveChangesAsync();
                return existing;
            });

        Result<JitProvisionUserResult> result = await CreateUseCase(attempt, users).ExecuteAsync(
            database.Command("subject", "person@example.com"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(DomainError.Forbidden("Federation.AuthenticationFailed", "Unable to complete sign-in."));
        await using OpenIdentityStackDbContext read = database.CreateContext();
        (await read.UpstreamProviders.SingleAsync()).IsActive.ShouldBeFalse();
        (await read.AuditLogEntries.CountAsync(entry => entry.Action == "Federation.AccountAssociationDenied")).ShouldBe(1);
    }

    [Fact]
    public async Task InitialProviderAuditFailureCannotCommitTheProvider()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using OpenIdentityStackDbContext attempt = database.CreateContext();
        await attempt.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reject_provider_audit BEFORE INSERT ON "AuditLogEntries"
            WHEN NEW."Action" = 'Federation.ProviderCreated'
            BEGIN SELECT RAISE(ABORT, 'injected audit insertion failure'); END;
            """);
        var useCase = new CreateProviderUseCase(new UpstreamProviderRepository(attempt), Substitute.For<IEnvironmentProvider>(),
            Substitute.For<ISecretProtector>(), new AuditLogService(NullLogger<AuditLogService>.Instance, attempt, new TestClock()));
        await Should.ThrowAsync<DbUpdateException>(() => useCase.ExecuteAsync(
            new CreateProviderCommand("audited-new-provider", "Provider", "https://new.example", "client", null, null, ActorId: "operator")));
        await using OpenIdentityStackDbContext read = database.CreateContext();
        (await read.UpstreamProviders.AnyAsync(provider => provider.Name == "audited-new-provider")).ShouldBeFalse();
        (await read.AuditLogEntries.CountAsync(entry => entry.Action == "Federation.ProviderCreated")).ShouldBe(0);
    }

    [Fact]
    public async Task PolicyAuditFailureCannotCommitThePolicyChange()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync();
        await using OpenIdentityStackDbContext attempt = database.CreateContext();
        await attempt.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reject_policy_audit BEFORE INSERT ON "AuditLogEntries"
            WHEN NEW."Action" = 'Federation.JitProvisioningPolicyChanged'
            BEGIN SELECT RAISE(ABORT, 'injected audit insertion failure'); END;
            """);
        UpstreamProvider provider = await attempt.UpstreamProviders.SingleAsync();
        var useCase = new UpdateProviderUseCase(new UpstreamProviderRepository(attempt),
            audit: new AuditLogService(NullLogger<AuditLogService>.Instance, attempt, new TestClock()));
        await Should.ThrowAsync<DbUpdateException>(() => useCase.ExecuteAsync(
            new UpdateProviderCommand(provider.Id.Value, JitProvisioningEnabled: false, ActorId: "operator")));
        await using OpenIdentityStackDbContext read = database.CreateContext();
        (await read.UpstreamProviders.SingleAsync()).JitProvisioningEnabled.ShouldBeTrue();
        (await read.AuditLogEntries.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task CreationAuditFailureRollsBackUserAssociationAndIssuerBinding()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync(bindIssuer: false);
        await using OpenIdentityStackDbContext attempt = database.CreateContext();
        await attempt.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reject_creation_audit BEFORE INSERT ON "AuditLogEntries"
            WHEN NEW."Action" = 'Federation.NewAccountAssociationRecorded'
            BEGIN SELECT RAISE(ABORT, 'injected audit insertion failure'); END;
            """);
        await Should.ThrowAsync<DbUpdateException>(() => CreateUseCase(attempt).ExecuteAsync(database.Command("subject", "person@example.com")));

        await using OpenIdentityStackDbContext read = database.CreateContext();
        (await read.Users.CountAsync()).ShouldBe(0);
        (await read.AuditLogEntries.CountAsync()).ShouldBe(0);
        (await read.UpstreamProviders.SingleAsync()).BoundIssuer.ShouldBeNull();
        await attempt.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_creation_audit;");
        (await CreateUseCase(attempt).ExecuteAsync(database.Command("subject", "person@example.com"))).IsSuccess.ShouldBeTrue();
        read.ChangeTracker.Clear();
        User retry = await read.Users.SingleAsync();
        retry.UpstreamIdentities.Single().SubjectId.ShouldBe("subject");
        (await read.AuditLogEntries.CountAsync(entry => entry.Action == "Federation.NewAccountAssociationRecorded")).ShouldBe(1);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("email")]
    [InlineData("provider")]
    public async Task CompetingIdentityEmailOrProviderCommitReturnsGenericAuditedDenial(string conflict)
    {
        await using TestDatabase database = await TestDatabase.CreateAsync(bindIssuer: conflict != "provider");
        await using OpenIdentityStackDbContext losingContext = database.CreateContext();
        await using OpenIdentityStackDbContext winningContext = database.CreateContext();
        JitProvisionUserCommand losingCommand = database.Command("loser", "loser@example.com");
        JitProvisionUserCommand winningCommand = database.Command(conflict == "identity" ? "loser" : "winner", conflict == "email" ? "loser@example.com" : "winner@example.com");
        var realUsers = new UserRepository(losingContext);
        IUserRepository losingUsers = Substitute.For<IUserRepository>();
        losingUsers.FindByUpstreamIdentityAsync(Arg.Any<UpstreamProviderId>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => realUsers.FindByUpstreamIdentityAsync(call.ArgAt<UpstreamProviderId>(0), call.ArgAt<string>(1), call.ArgAt<CancellationToken>(2)));
        losingUsers.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(async call =>
        {
            User? beforeWinner = await realUsers.GetByEmailAsync(call.ArgAt<string>(0), call.ArgAt<CancellationToken>(1));
            beforeWinner.ShouldBeNull();
            // Both operations have observed no collision; commit the competing request before this stale request saves.
            (await CreateUseCase(winningContext).ExecuteAsync(winningCommand)).IsSuccess.ShouldBeTrue();
            return beforeWinner;
        });
        losingUsers.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(call => realUsers.AddAsync(call.ArgAt<User>(0), call.ArgAt<CancellationToken>(1)));
        losingUsers.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(call => realUsers.SaveChangesAsync(call.ArgAt<CancellationToken>(0)));

        Result<JitProvisionUserResult> denied = await CreateUseCase(losingContext, losingUsers).ExecuteAsync(losingCommand);
        denied.IsFailure.ShouldBeTrue();
        denied.Error.ShouldBe(DomainError.Forbidden("Federation.AuthenticationFailed", "Unable to complete sign-in."));
        await using OpenIdentityStackDbContext read = database.CreateContext();
        User winner = await read.Users.SingleAsync();
        winner.Email.ShouldBe(winningCommand.Email);
        winner.UpstreamIdentities.Count.ShouldBe(1);
        winner.UpstreamIdentities[0].SubjectId.ShouldBe(winningCommand.SubjectId);
        (await read.AuditLogEntries.CountAsync(entry => entry.Action == "Federation.AccountAssociationDenied")).ShouldBe(1);
        (await read.AuditLogEntries.CountAsync(entry => entry.Action == "Federation.NewAccountAssociationRecorded")).ShouldBe(1);
    }

    [Fact]
    public async Task CancellationAfterCreationSaveRollsBackAndPropagates()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync(bindIssuer: false);
        await using OpenIdentityStackDbContext attempt = database.CreateContext();
        using var cancellation = new CancellationTokenSource();
        IAuditLog audit = Substitute.For<IAuditLog>();
        audit.LogAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                cancellation.Cancel();
                return Task.FromCanceled(cancellation.Token);
            });
        await Should.ThrowAsync<OperationCanceledException>(() => CreateUseCase(attempt, audit: audit)
            .ExecuteAsync(database.Command("subject", "person@example.com"), cancellation.Token));
        attempt.ChangeTracker.Entries().ShouldBeEmpty();
        await using OpenIdentityStackDbContext read = database.CreateContext();
        (await read.Users.CountAsync()).ShouldBe(0);
        (await read.AuditLogEntries.CountAsync()).ShouldBe(0);
        (await read.UpstreamProviders.SingleAsync()).BoundIssuer.ShouldBeNull();
    }

    [Fact]
    public async Task UnrelatedDatabaseFailureIsNotReportedAsAnIdentityCollision()
    {
        await using TestDatabase database = await TestDatabase.CreateAsync(bindIssuer: false);
        await using OpenIdentityStackDbContext attempt = database.CreateContext();
        await attempt.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reject_user_insert BEFORE INSERT ON "Users"
            BEGIN SELECT RAISE(ABORT, 'injected unrelated database failure'); END;
            """);
        await Should.ThrowAsync<DbUpdateException>(() => CreateUseCase(attempt).ExecuteAsync(database.Command("subject", "person@example.com")));
        attempt.ChangeTracker.Entries().ShouldBeEmpty();
        await using OpenIdentityStackDbContext read = database.CreateContext();
        (await read.Users.CountAsync()).ShouldBe(0);
        (await read.AuditLogEntries.CountAsync()).ShouldBe(0);
        (await read.UpstreamProviders.SingleAsync()).BoundIssuer.ShouldBeNull();
    }
    private static JitProvisionUserUseCase CreateUseCase(OpenIdentityStackDbContext db, IUserRepository? users = null, IAuditLog? audit = null) =>
        new(users ?? new UserRepository(db), new UpstreamProviderRepository(db),
            new AuditLogService(NullLogger<AuditLogService>.Instance, db, new TestClock()),
            new JitProvisioningPersistence(db, audit ?? new AuditLogService(NullLogger<AuditLogService>.Instance, db, new TestClock())));

    private sealed class TestClock : IDateTimeProvider
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
        public DateTimeOffset Now => DateTimeOffset.Now;
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("DataSource=:memory:");
        private DbContextOptions<OpenIdentityStackDbContext> options = null!;
        private UpstreamProviderId providerId;
        public OpenIdentityStackDbContext CreateContext() => new(this.options);
        public JitProvisionUserCommand Command(string subject, string email) =>
            new(this.providerId, subject, email, "Person", "https://issuer.example", "https://issuer.example");

        public static async Task<TestDatabase> CreateAsync(bool bindIssuer = true)
        {
            var database = new TestDatabase();
            await database.connection.OpenAsync();
            database.options = new DbContextOptionsBuilder<OpenIdentityStackDbContext>().UseSqlite(database.connection).Options;
            await using OpenIdentityStackDbContext db = database.CreateContext();
            await db.Database.EnsureCreatedAsync();
            UpstreamProvider provider = UpstreamProvider.Create("provider", "Provider", "https://issuer.example", "client").Value;
            if (bindIssuer) { provider.BindIssuer("https://issuer.example", "https://issuer.example").IsSuccess.ShouldBeTrue(); }
            db.UpstreamProviders.Add(provider);
            await db.SaveChangesAsync();
            database.providerId = provider.Id;
            return database;
        }

        public ValueTask DisposeAsync() => this.connection.DisposeAsync();
    }
}
