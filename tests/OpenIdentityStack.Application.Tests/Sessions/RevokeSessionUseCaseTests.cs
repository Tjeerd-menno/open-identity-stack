using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Sessions.Commands;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;
using SharedKernel;

namespace OpenIdentityStack.Application.Tests.Sessions;

public sealed class RevokeSessionUseCaseTests
{
    private readonly ISessionRepository sessions = Substitute.For<ISessionRepository>();
    private readonly ICredentialTerminationService termination = Substitute.For<ICredentialTerminationService>();
    private readonly RevokeSessionUseCase useCase;

    public RevokeSessionUseCaseTests()
    {
        this.termination.TerminateSessionAsync(Arg.Any<SessionId>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(Result.Success());
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        this.useCase = new RevokeSessionUseCase(this.sessions, this.termination, clock);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingSession_DelegatesAuditedTermination()
    {
        UserSession session = CreateSession();
        this.sessions.GetByIdAsync(session.Id, Arg.Any<CancellationToken>()).Returns(session);

        Result<RevokeSessionResult> result = await this.useCase.ExecuteAsync(new RevokeSessionCommand(session.Id, "admin"));

        result.IsSuccess.ShouldBeTrue();
        await this.termination.Received(1).TerminateSessionAsync(session.Id, "admin", "administrative-revocation", false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_MissingSession_ReturnsNotFoundWithoutTermination()
    {
        var id = SessionId.Create();
        this.sessions.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((UserSession?)null);

        Result<RevokeSessionResult> result = await this.useCase.ExecuteAsync(new RevokeSessionCommand(id, "admin"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SessionErrors.NotFound);
    }

    private static UserSession CreateSession()
    {
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        return UserSession.Create(UserId.Create(), "127.0.0.1", "agent", clock).Value;
    }
}
