using System.Security.Claims;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIddict.Server;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Infrastructure.Identity;
using OpenIdentityStack.Infrastructure.Audit;
using OpenIdentityStack.Infrastructure.Persistence;

namespace OpenIdentityStack.Infrastructure.Tests.Identity;

public sealed class TokenIssuanceTransactionTests
{
    [Fact]
    public async Task CommitMakesAllIssuanceWritesDurableTogether()
    {
        await using SqliteConnection connection = await CreateDatabaseAsync();
        await using OpenIdentityStackDbContext db = CreateContext(connection);
        await using var transaction = new TokenIssuanceTransaction(db);

        await transaction.BeginAsync(default);
        db.AuditLogEntries.Add(new AuditLogEntry
        {
            UserId = "actor", Action = "Issuance.Test", EntityType = "Token", EntityId = "committed"
        });
        await db.SaveChangesAsync();
        await transaction.CommitAsync(default);

        await using OpenIdentityStackDbContext read = CreateContext(connection);
        (await read.AuditLogEntries.AnyAsync(entry => entry.EntityId == "committed")).ShouldBeTrue();
    }

    [Fact]
    public async Task DisposalRollsBackAnUncompletedIssuance()
    {
        await using SqliteConnection connection = await CreateDatabaseAsync();
        await using (OpenIdentityStackDbContext db = CreateContext(connection))
        {
            await using var transaction = new TokenIssuanceTransaction(db);
            await transaction.BeginAsync(default);
            db.AuditLogEntries.Add(new AuditLogEntry
            {
                UserId = "actor", Action = "Issuance.Test", EntityType = "Token", EntityId = "rolled-back"
            });
            await db.SaveChangesAsync();
        }

        await using OpenIdentityStackDbContext read = CreateContext(connection);
        (await read.AuditLogEntries.AnyAsync(entry => entry.EntityId == "rolled-back")).ShouldBeFalse();
    }

    [Fact]
    public async Task ApplicationMetadataCannotBePersistedOutsideTheIssuanceTransaction()
    {
        await using SqliteConnection connection = await CreateDatabaseAsync();
        await using OpenIdentityStackDbContext db = CreateContext(connection);
        const string clientId = "machine-client";
        var token = new OpenIddictEntityFrameworkCoreToken
        {
            Id = Guid.NewGuid().ToString(), Subject = clientId,
            Type = OpenIddictConstants.TokenTypeIdentifiers.AccessToken,
            Status = OpenIddictConstants.Statuses.Valid
        };
        db.Add(token);
        await db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(OpenIddictConstants.Claims.Subject, clientId),
            new Claim(TokenSubjectClaims.Kind, TokenSubjectClaims.Application)]));
        principal.SetTokenId(token.Id);
        var serverTransaction = new OpenIddictServerTransaction
        {
            Request = new OpenIddictRequest
            {
                GrantType = OpenIddictConstants.GrantTypes.ClientCredentials,
                ClientId = clientId
            }
        };
        var context = new OpenIddictServerEvents.GenerateTokenContext(serverTransaction)
        {
            CreateTokenEntry = true,
            Principal = principal,
            TokenType = OpenIddictConstants.TokenTypeIdentifiers.AccessToken
        };

        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => new ApplicationTokenSubjectMetadata(db).HandleAsync(context).AsTask());

        exception.Message.ShouldContain("token issuance transaction");
        (await db.Set<OpenIddictEntityFrameworkCoreToken>().SingleAsync()).Properties.ShouldBeNull();
    }

    private static async Task<SqliteConnection> CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using OpenIdentityStackDbContext db = CreateContext(connection);
        await db.Database.EnsureCreatedAsync();
        return connection;
    }

    private static OpenIdentityStackDbContext CreateContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<OpenIdentityStackDbContext>().UseSqlite(connection).UseOpenIddict().Options);
}
