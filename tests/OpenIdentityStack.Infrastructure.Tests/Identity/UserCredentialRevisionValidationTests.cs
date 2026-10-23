using System.Data.Common;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Identity;
using OpenIdentityStack.Infrastructure.Persistence;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Identity;

public sealed class UserCredentialRevisionValidationTests
{
    [Fact]
    public async Task CredentialRevisionLookupIsACompiledScalarQuery()
    {
        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "OpenIdentityStack.Infrastructure",
            "Identity",
            "UserCredentialRevisionValidation.cs"));
        source.ShouldContain("EF.CompileAsyncQuery");
        source.ShouldNotContain("SingleOrDefaultAsync");

        FieldInfo compiled = typeof(UserCredentialRevisionValidation)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic)
            .ShouldHaveSingleItem();
        compiled.FieldType.ShouldBe(typeof(ConditionalWeakTable<IModel, Func<OpenIdentityStackDbContext, UserId, CancellationToken, Task<Guid?>>>));

        await using SqliteConnection connection = await CreateDatabaseAsync();
        await using OpenIdentityStackDbContext first = CreateContext(connection, interceptor: null);
        await using OpenIdentityStackDbContext second = new(
            new DbContextOptionsBuilder<OpenIdentityStackDbContext>()
                .UseSqlite(connection)
                .UseOpenIddict()
                .EnableSensitiveDataLogging()
                .Options);
        ReferenceEquals(first.Model, second.Model).ShouldBeFalse();

        OpenIddictServerEvents.ValidateTokenContext missing = Token(UserId.Create(), revision: null);
        await new UserCredentialRevisionValidation(first).HandleAsync(missing);
        await new UserCredentialRevisionValidation(second).HandleAsync(missing);
        missing.IsRejected.ShouldBeFalse();
        CompiledQuery(compiled, first.Model).Method.Name.ShouldBe("ExecuteAsync");
        CompiledQuery(compiled, second.Model).Method.DeclaringType!.Namespace
            .ShouldBe("Microsoft.EntityFrameworkCore.Query.Internal");
    }

    [Fact]
    public async Task ValidationReadsTheCommittedRevisionEvenWhenTheUserIsTracked()
    {
        await using SqliteConnection connection = await CreateDatabaseAsync();
        var commands = new CommandRecorder();
        await using OpenIdentityStackDbContext reader = CreateContext(connection, commands);
        IDateTimeProvider clock = Clock();
        User seeded = User.CreateBootstrap("tracked@example.test", "Tracked", "hash", clock).Value;
        reader.Users.Add(seeded);
        await reader.SaveChangesAsync();
        UserId userId = seeded.Id;
        User tracked = await reader.Users.SingleAsync(user => user.Id == userId);
        tracked.CredentialRevision.ShouldBe(Guid.Empty);

        Guid revised;
        await using (OpenIdentityStackDbContext writer = CreateContext(connection, interceptor: null))
        {
            User current = await writer.Users.SingleAsync(user => user.Id == userId);
            current.SetPassword("next-hash", clock).IsSuccess.ShouldBeTrue();
            await writer.SaveChangesAsync();
            revised = current.CredentialRevision;
        }

        revised.ShouldNotBe(Guid.Empty);
        commands.Commands.Clear();
        var validator = new UserCredentialRevisionValidation(reader);
        OpenIddictServerEvents.ValidateTokenContext stale = Token(userId, Guid.Empty.ToString());
        OpenIddictServerEvents.ValidateTokenContext fresh = Token(userId, revised.ToString());
        await validator.HandleAsync(stale);
        await validator.HandleAsync(fresh);

        stale.IsRejected.ShouldBeTrue();
        fresh.IsRejected.ShouldBeFalse();
        commands.Commands.Count(IsCredentialRevisionRead).ShouldBe(2);
    }

    [Fact]
    public async Task ProcessSignInReadsTheRevisionAfterTheBoundaryLock()
    {
        await using SqliteConnection connection = await CreateDatabaseAsync();
        var commands = new CommandRecorder();
        await using OpenIdentityStackDbContext db = CreateContext(connection, commands);
        IDateTimeProvider clock = Clock();
        User user = User.CreateBootstrap("signin@example.test", "Sign in", "hash", clock).Value;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var validator = new UserCredentialRevisionValidation(db);
        await using var issuance = new TokenIssuanceTransaction(db);
        await issuance.BeginAsync(CancellationToken.None);
        commands.Commands.Clear();
        OpenIddictServerEvents.ProcessSignInContext first = SignIn(user.Id, Guid.Empty.ToString());
        OpenIddictServerEvents.ProcessSignInContext second = SignIn(user.Id, Guid.Empty.ToString());
        await validator.HandleAsync(first);
        await validator.HandleAsync(second);

        first.IsRejected.ShouldBeFalse();
        second.IsRejected.ShouldBeFalse();
        commands.Commands.Count(IsCredentialRevisionRead).ShouldBe(2);
        commands.Commands.Count(command => command.Contains("UPDATE", StringComparison.OrdinalIgnoreCase)
            && command.Contains("CredentialRevision", StringComparison.Ordinal)).ShouldBe(2);
    }

    [Fact]
    public async Task CanceledValidationDoesNotReadTheRevision()
    {
        await using SqliteConnection connection = await CreateDatabaseAsync();
        await using OpenIdentityStackDbContext db = CreateContext(connection, interceptor: null);
        IDateTimeProvider clock = Clock();
        User user = User.CreateBootstrap("canceled@example.test", "Canceled", "hash", clock).Value;
        db.Users.Add(user);
        await db.SaveChangesAsync();

        OpenIddictServerEvents.ValidateTokenContext context = Token(user.Id, Guid.Empty.ToString());
        context.CancellationToken = new CancellationToken(canceled: true);

        await Should.ThrowAsync<OperationCanceledException>(
            () => new UserCredentialRevisionValidation(db).HandleAsync(context).AsTask());
        context.IsRejected.ShouldBeFalse();
    }

    private static Delegate CompiledQuery(FieldInfo compiled, IModel model)
    {
        object table = compiled.GetValue(null)!;
        MethodInfo tryGetValue = compiled.FieldType.GetMethod("TryGetValue")!;
        object?[] args = [model, null];
        ((bool)tryGetValue.Invoke(table, args)!).ShouldBeTrue();
        return args[1].ShouldBeAssignableTo<Delegate>()!;
    }

    private static OpenIddictServerEvents.ValidateTokenContext Token(UserId userId, string? revision)
    {
        var identity = new ClaimsIdentity([new Claim(OpenIddictConstants.Claims.Subject, userId.Value.ToString())]);
        if (revision is not null)
        {
            identity.AddClaim(new Claim("ois_credential_revision", revision));
        }

        return new OpenIddictServerEvents.ValidateTokenContext(new OpenIddictServerTransaction())
        {
            Principal = new ClaimsPrincipal(identity),
        };
    }

    private static OpenIddictServerEvents.ProcessSignInContext SignIn(UserId userId, string revision) =>
        new(new OpenIddictServerTransaction())
        {
            Principal = Token(userId, revision).Principal,
        };

    private static bool IsCredentialRevisionRead(string command) =>
        command.Contains("SELECT", StringComparison.OrdinalIgnoreCase)
        && command.Contains("CredentialRevision", StringComparison.Ordinal);

    private static IDateTimeProvider Clock()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        return clock;
    }

    private static async Task<SqliteConnection> CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using OpenIdentityStackDbContext db = CreateContext(connection, interceptor: null);
        await db.Database.EnsureCreatedAsync();
        return connection;
    }

    private static OpenIdentityStackDbContext CreateContext(SqliteConnection connection, CommandRecorder? interceptor)
    {
        DbContextOptionsBuilder<OpenIdentityStackDbContext> builder = new DbContextOptionsBuilder<OpenIdentityStackDbContext>()
            .UseSqlite(connection)
            .UseOpenIddict();
        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        return new OpenIdentityStackDbContext(builder.Options);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenIdentityStack.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }

    private sealed class CommandRecorder : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            this.Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            this.Commands.Add(command.CommandText);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
