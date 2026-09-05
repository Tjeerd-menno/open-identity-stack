using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class CredentialBoundaryTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    [Fact]
    public async Task ValidationRejectsStaleCapturedEpochEvenWhenTokenWasNotPresentDuringBulkRevocation()
    {
        await using OpenIdentityStackDbContext db = fixture.CreateDbContext();
        CredentialBoundaryStore store = CreateStore(db);
        Guid captured = await store.GetEpochAsync();
        await store.ExecuteAsync(Guid.NewGuid(), "operator");
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([
            new System.Security.Claims.Claim(OpenIdentityStack.Application.Abstractions.CredentialBoundaryClaims.Epoch, captured.ToString())]));
        var validator = new OpenIdentityStack.Infrastructure.Identity.CredentialBoundaryValidation(store);
        var server = new OpenIddict.Server.OpenIddictServerEvents.ValidateTokenContext(new OpenIddict.Server.OpenIddictServerTransaction()) { Principal = principal };
        var local = new OpenIddict.Validation.OpenIddictValidationEvents.ValidateTokenContext(new OpenIddict.Validation.OpenIddictValidationTransaction()) { Principal = principal };
        await validator.HandleAsync(server);
        await validator.HandleAsync(local);
        server.IsRejected.ShouldBeTrue();
        local.IsRejected.ShouldBeTrue();
    }

    [Fact]
    public async Task CutoverIsDurableAndIdempotentAndRejectsPreviouslyCapturedEpoch()
    {
        await using OpenIdentityStackDbContext db = fixture.CreateDbContext();
        CredentialBoundaryStore store = CreateStore(db);
        Guid before = await store.GetEpochAsync();
        var operation = Guid.NewGuid();
        OpenIdentityStack.Application.Abstractions.CredentialCutoverResult completed = await store.ExecuteAsync(operation, "operator");
        (await store.IsCurrentAsync(before.ToString())).ShouldBeFalse();
        (await store.IsCurrentAsync(null)).ShouldBeFalse();
        (await store.IsCurrentAsync(operation.ToString())).ShouldBeTrue();

        await using OpenIdentityStackDbContext restarted = fixture.CreateDbContext();
        CredentialBoundaryStore otherInstance = CreateStore(restarted);
        (await otherInstance.GetEpochAsync()).ShouldBe(operation);
        (await otherInstance.ExecuteAsync(operation, "operator")).ShouldBe(completed);
        var next = Guid.NewGuid();
        await otherInstance.ExecuteAsync(next, "operator");
        await otherInstance.ExecuteAsync(operation, "operator");
        (await store.GetEpochAsync()).ShouldBe(next);
        (await db.AuditLogEntries.CountAsync(e => e.Action == "CredentialBoundary.CutoverCompleted" && e.EntityId == operation.ToString())).ShouldBe(1);
    }

    [Fact]
    public async Task FailedRevocationCannotAdvanceBoundaryAndRetryCanComplete()
    {
        await using OpenIdentityStackDbContext db = fixture.CreateDbContext();
        IOpenIddictTokenManager tokens = Substitute.For<IOpenIddictTokenManager>();
        CredentialBoundaryStore store = CreateStore(db, tokens);
        Guid before = await store.GetEpochAsync();
        var operation = Guid.NewGuid();
#pragma warning disable CA2012 // NSubstitute configures this call; the production ValueTask is awaited by the store.
        tokens.RevokeAsync(null, null, null, null, Arg.Any<CancellationToken>()).Returns(_ => ValueTask.FromException<long>(new InvalidOperationException("Simulated revocation failure")));
#pragma warning restore CA2012
        await Should.ThrowAsync<InvalidOperationException>(() => store.ExecuteAsync(operation, "operator"));
        await using OpenIdentityStackDbContext fresh = fixture.CreateDbContext();
        CredentialBoundaryStore retry = CreateStore(fresh);
        (await retry.GetEpochAsync()).ShouldBe(before);
        await retry.ExecuteAsync(operation, "operator");
        (await retry.GetEpochAsync()).ShouldBe(operation);
    }

    private static CredentialBoundaryStore CreateStore(OpenIdentityStackDbContext db, IOpenIddictTokenManager? tokens = null)
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        return new CredentialBoundaryStore(db, tokens ?? Substitute.For<IOpenIddictTokenManager>(), Substitute.For<IOpenIddictAuthorizationManager>(), clock);
    }
}
