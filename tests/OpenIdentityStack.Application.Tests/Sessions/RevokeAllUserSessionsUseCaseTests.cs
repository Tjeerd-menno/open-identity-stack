using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;
using SharedKernel;

namespace OpenIdentityStack.Application.Tests.Sessions;

public sealed class RevokeAllUserSessionsUseCaseTests
{
    private readonly ICredentialTerminationService termination = Substitute.For<ICredentialTerminationService>();
    private readonly IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
    private readonly RevokeAllUserSessionsUseCase useCase;

    public RevokeAllUserSessionsUseCaseTests()
    {
        this.clock.UtcNow.Returns(new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero));
        this.useCase = new RevokeAllUserSessionsUseCase(this.clock, this.termination);
    }

    [Fact]
    public async Task ExecuteAsync_DelegatesOneAtomicTerminationIncludingExcludedSession()
    {
        var userId = UserId.Create();
        var excluded = SessionId.Create();
        this.termination.TerminateAllSessionsAsync(userId, "admin", "administrative-revoke-all", excluded, Arg.Any<CancellationToken>()).Returns(2);

        Result<RevokeAllUserSessionsResult> result = await this.useCase.ExecuteAsync(new RevokeAllUserSessionsCommand(userId, excluded, "admin"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.RevokedCount.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesAtomicTerminationFailure()
    {
        var userId = UserId.Create();
        this.termination.TerminateAllSessionsAsync(userId, "admin", "administrative-revoke-all", null, Arg.Any<CancellationToken>()).Returns(SessionErrors.NotFound);

        Result<RevokeAllUserSessionsResult> result = await this.useCase.ExecuteAsync(new RevokeAllUserSessionsCommand(userId, null, "admin"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SessionErrors.NotFound);
    }
}
