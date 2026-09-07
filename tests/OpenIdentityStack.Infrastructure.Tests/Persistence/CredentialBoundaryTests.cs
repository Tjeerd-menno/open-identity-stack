using OpenIdentityStack.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Infrastructure.Persistence;
using OpenIdentityStack.Infrastructure.Tests.Common;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Tests.Persistence;

public sealed class CredentialBoundaryTests(SqliteTestFixture fixture) : IClassFixture<SqliteTestFixture>
{
    [Fact]
    public async Task CutoverRevokesActiveSessionsSetBasedWithoutTrackingAggregates()
    {
        await using OpenIdentityStackDbContext db = fixture.CreateDbContext();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        clock.UtcNow.Returns(now);
        UserSession[] sessions = Enumerable.Range(0, 5)
            .Select(_ => UserSession.Create(UserId.Create(), "127.0.0.1", "Test Browser", clock).Value)
            .ToArray();
        db.UserSessions.AddRange(sessions);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        OpenIdentityStack.Application.Abstractions.CredentialCutoverResult result =
            (await CreateStore(db, clock: clock).ExecuteAsync(Guid.NewGuid(), "operator")).Value;

        result.Sessions.ShouldBe(sessions.Length);
        db.ChangeTracker.Entries<UserSession>().ShouldBeEmpty();
        UserSession[] persisted = await db.UserSessions.AsNoTracking()
            .Where(session => sessions.Select(seed => seed.Id).Contains(session.Id))
            .ToArrayAsync();
        persisted.ShouldAllBe(session => session.Status == SessionStatus.Revoked && session.RevokedAt == now);
    }

    [Fact]
    public async Task BlockedPreflightLeavesBoundaryAndCredentialsUntouchedAndRecordsTheFailure()
    {
        await using OpenIdentityStackDbContext db = fixture.CreateDbContext();
        ICredentialCutoverGate gate = Substitute.For<ICredentialCutoverGate>();
        var emergencyUser = Guid.NewGuid();
        var preflight = new CredentialCutoverPreflight(Guid.Empty, DateTimeOffset.UtcNow,
            [new CutoverBlocker("Identity.Quarantined", "Recovery is not specified.")],
            new(Guid.NewGuid(), emergencyUser, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true), new(1, 1, 1, 0, 0, 0, 0, 0), [], [], 0, null);
        gate.EvaluateAsync(Arg.Any<CancellationToken>()).Returns(preflight);
        IOpenIddictTokenManager tokens = Substitute.For<IOpenIddictTokenManager>();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        var store = new CredentialBoundaryStore(db, tokens, Substitute.For<IOpenIddictAuthorizationManager>(), clock, gate);
        Guid before = await store.GetEpochAsync();
        var operation = Guid.NewGuid();

        Result<CredentialCutoverResult> result = await store.ExecuteAsync(operation, "operator");

        result.IsFailure.ShouldBeTrue();
        (await store.GetEpochAsync()).ShouldBe(before);
        (await db.Set<CredentialCutoverRecord>().AnyAsync(x => x.Id == operation)).ShouldBeFalse();
        string? auditSummary = await db.AuditLogEntries.Where(x => x.Action == "CredentialCutover.PreflightBlocked" && x.EntityId == operation.ToString())
            .Select(x => x.AfterState).SingleAsync();
        auditSummary.ShouldNotBeNull();
        auditSummary.ShouldContain("Identity.Quarantined");
        auditSummary.ShouldNotContain(emergencyUser.ToString());
#pragma warning disable CA2012
        await tokens.DidNotReceive().RevokeAsync(null, null, null, null, Arg.Any<CancellationToken>());
#pragma warning restore CA2012
    }

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
        OpenIdentityStack.Application.Abstractions.CredentialCutoverResult completed = (await store.ExecuteAsync(operation, "operator")).Value;
        (await store.IsCurrentAsync(before.ToString())).ShouldBeFalse();
        (await store.IsCurrentAsync(null)).ShouldBeFalse();
        (await store.IsCurrentAsync(operation.ToString())).ShouldBeTrue();

        await using OpenIdentityStackDbContext restarted = fixture.CreateDbContext();
        CredentialBoundaryStore otherInstance = CreateStore(restarted);
        (await otherInstance.GetEpochAsync()).ShouldBe(operation);
        (await otherInstance.ExecuteAsync(operation, "operator")).Value.ShouldBe(completed);
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

    private static CredentialBoundaryStore CreateStore(
        OpenIdentityStackDbContext db,
        IOpenIddictTokenManager? tokens = null,
        IDateTimeProvider? clock = null,
        ICredentialCutoverGate? gate = null)
    {
        if (clock is null)
        {
            clock = Substitute.For<IDateTimeProvider>();
            clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        }
        gate ??= Substitute.For<ICredentialCutoverGate>();
        gate.EvaluateAsync(Arg.Any<CancellationToken>()).Returns(new CredentialCutoverPreflight(Guid.Empty, DateTimeOffset.UtcNow, [], null, new(0, 0, 0, 0, 0, 0, 0, 0), [], [], 0, null));
        return new CredentialBoundaryStore(db, tokens ?? Substitute.For<IOpenIddictTokenManager>(), Substitute.For<IOpenIddictAuthorizationManager>(), clock, gate);
    }
}
