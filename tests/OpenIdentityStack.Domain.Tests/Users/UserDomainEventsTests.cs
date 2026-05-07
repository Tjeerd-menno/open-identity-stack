using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Domain.Tests.Users;

public sealed class UserDomainEventsTests
{
    [Fact]
    public void UserCreated_SetsProperties()
    {
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var userId = UserId.Create();

        var evt = new UserDomainEvents.UserCreated(userId, "user@example.com", "User", occurredAt);

        evt.UserId.ShouldBe(userId);
        evt.Email.ShouldBe("user@example.com");
        evt.DisplayName.ShouldBe("User");
        evt.OccurredAt.ShouldBe(occurredAt);
        evt.EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void UserEmailVerified_SetsProperties()
    {
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var userId = UserId.Create();

        var evt = new UserDomainEvents.UserEmailVerified(userId, occurredAt);

        evt.UserId.ShouldBe(userId);
        evt.OccurredAt.ShouldBe(occurredAt);
        evt.EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void UserDisabled_SetsProperties()
    {
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var userId = UserId.Create();

        var evt = new UserDomainEvents.UserDisabled(userId, "maintenance", occurredAt);

        evt.UserId.ShouldBe(userId);
        evt.Reason.ShouldBe("maintenance");
        evt.OccurredAt.ShouldBe(occurredAt);
        evt.EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void UserEnabled_SetsProperties()
    {
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var userId = UserId.Create();

        var evt = new UserDomainEvents.UserEnabled(userId, occurredAt);

        evt.UserId.ShouldBe(userId);
        evt.OccurredAt.ShouldBe(occurredAt);
        evt.EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void UserLoggedIn_SetsProperties()
    {
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var userId = UserId.Create();

        var evt = new UserDomainEvents.UserLoggedIn(userId, occurredAt);

        evt.UserId.ShouldBe(userId);
        evt.OccurredAt.ShouldBe(occurredAt);
        evt.EventId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void UserPasswordChanged_SetsProperties()
    {
        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var userId = UserId.Create();

        var evt = new UserDomainEvents.UserPasswordChanged(userId, occurredAt);

        evt.UserId.ShouldBe(userId);
        evt.OccurredAt.ShouldBe(occurredAt);
        evt.EventId.ShouldNotBe(Guid.Empty);
    }
}
