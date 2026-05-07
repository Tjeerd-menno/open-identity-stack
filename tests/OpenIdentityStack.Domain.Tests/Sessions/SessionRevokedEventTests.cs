using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Sessions;

public sealed class SessionRevokedEventTests
{
    [Fact]
    public void SessionRevokedEvent_SetsProperties()
    {
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var sessionId = SessionId.Create();
        var userId = UserId.Create();

        var evt = new SessionRevokedEvent(sessionId, userId, occurredAt);

        evt.SessionId.ShouldBe(sessionId);
        evt.UserId.ShouldBe(userId);
        evt.OccurredAt.ShouldBe(occurredAt);
        evt.EventId.ShouldNotBe(Guid.Empty);
    }
}
