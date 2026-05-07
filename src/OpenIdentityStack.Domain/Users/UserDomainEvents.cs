using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Users;

/// <summary>
/// User domain events.
/// </summary>
public static class UserDomainEvents
{
    /// <summary>
    /// Raised when a new user is created.
    /// </summary>
    public sealed record UserCreated(
        UserId UserId,
        string Email,
        string DisplayName,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    /// <summary>
    /// Raised when a user's email is verified.
    /// </summary>
    public sealed record UserEmailVerified(
        UserId UserId,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    /// <summary>
    /// Raised when a user account is disabled.
    /// </summary>
    public sealed record UserDisabled(
        UserId UserId,
        string Reason,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    /// <summary>
    /// Raised when a disabled user account is re-enabled.
    /// </summary>
    public sealed record UserEnabled(
        UserId UserId,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    /// <summary>
    /// Raised when a user successfully logs in.
    /// </summary>
    public sealed record UserLoggedIn(
        UserId UserId,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);

    /// <summary>
    /// Raised when a user's password is changed.
    /// </summary>
    public sealed record UserPasswordChanged(
        UserId UserId,
        DateTimeOffset OccurredAt) : DomainEvent(OccurredAt);
}
