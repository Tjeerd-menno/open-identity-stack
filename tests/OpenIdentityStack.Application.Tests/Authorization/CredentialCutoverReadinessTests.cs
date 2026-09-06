using OpenIdentityStack.Application.Abstractions;
using SharedKernel;

namespace OpenIdentityStack.Application.Tests.Authorization;

public sealed class CredentialCutoverReadinessTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task EmergencyProofRequiresLocalAuthenticationNotJustHumanFreshness(bool human, bool localPassword)
    {
        ICredentialCutoverReadinessStore store = Substitute.For<ICredentialCutoverReadinessStore>();
        IAdministrativeApproval approval = Substitute.For<IAdministrativeApproval>();
        approval.RequireAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        IAdministrativeActorContext actor = Substitute.For<IAdministrativeActorContext>();
        actor.Current.Returns(new AdministrativeActor(UserId.Create(), DateTimeOffset.UtcNow, human, true,
            localPassword ? Guid.NewGuid() : null, Guid.Empty));
        var workflow = new CredentialCutoverReadiness(store, approval, actor, Substitute.For<IAuditLog>());

        Result<EmergencyAccessEvidence> result = await workflow.RecordEmergencyAccessAsync();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldContain("IndependentLoginRequired");
        await approval.Received(1).RecordOutcomeAsync(false, Arg.Any<CancellationToken>());
        await store.DidNotReceive().RecordEmergencyAccessAsync(Arg.Any<AdministrativeActor>(), Arg.Any<CancellationToken>());
    }
    [Theory]
    [InlineData("AssumeSafe", 0, "reference")]
    [InlineData("OfflineExpiry", -1, "reference")]
    [InlineData("OnlineIntrospection", 0, " ")]
    public async Task ResourceReviewRejectsUnboundedOrUnspecifiedEvidence(string mechanism, int seconds, string reference)
    {
        ICredentialCutoverReadinessStore store = Substitute.For<ICredentialCutoverReadinessStore>();
        IAdministrativeApproval approval = Substitute.For<IAdministrativeApproval>();
        approval.RequireAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        var workflow = new CredentialCutoverReadiness(store, approval, Substitute.For<IAdministrativeActorContext>(), Substitute.For<IAuditLog>());
        (await workflow.ReviewResourceWindowAsync(new(Guid.NewGuid(), mechanism, seconds, reference))).IsFailure.ShouldBeTrue();
        await approval.Received(1).RecordOutcomeAsync(false, Arg.Any<CancellationToken>());
        await store.DidNotReceive().ReviewResourceWindowAsync(Arg.Any<ResourceWindowReview>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

    [Fact]
    public async Task PreflightAuditContainsReadinessSummaryWithoutEmergencyIdentifiers()
    {
        ICredentialCutoverReadinessStore store = Substitute.For<ICredentialCutoverReadinessStore>();
        IAuditLog audit = Substitute.For<IAuditLog>();
        var emergencyUser = Guid.NewGuid();
        var emergencySession = Guid.NewGuid();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var preflight = new CredentialCutoverPreflight(Guid.NewGuid(), now,
            [new("Resource.WindowUnresolved", "A resource requires review")],
            new(Guid.NewGuid(), emergencyUser, emergencySession, now, now, true),
            new(0, 0, 0, 0, 0, 0, 0, 0), [], [], 12, now.AddMinutes(5));
        store.EvaluateAsync(Arg.Any<CancellationToken>()).Returns(preflight);
        var workflow = new CredentialCutoverReadiness(store, Substitute.For<IAdministrativeApproval>(),
            Substitute.For<IAdministrativeActorContext>(), audit);

        (await workflow.EvaluateAsync()).ShouldBe(preflight);

        await audit.Received(1).LogChangeAsync(Arg.Any<string>(), "CredentialCutover.PreflightEvaluated", "CredentialBoundary",
            preflight.Epoch.ToString(), Arg.Is<string?>(value => value == null),
            Arg.Is<string>(json => json.Contains("Resource.WindowUnresolved", StringComparison.Ordinal)
                && json.Contains("OutstandingAccessTokens", StringComparison.Ordinal)
                && !json.Contains(emergencyUser.ToString(), StringComparison.Ordinal)
                && !json.Contains(emergencySession.ToString(), StringComparison.Ordinal)), Arg.Any<CancellationToken>());
    }
}
