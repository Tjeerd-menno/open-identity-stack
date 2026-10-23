using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Session fields required to decide whether a credential is still usable.
/// </summary>
internal sealed record SessionValidationSnapshot(
    UserId UserId,
    SessionStatus Status,
    DateTimeOffset ExpiresAt,
    long UserSecurityVersion)
{
    public bool IsExpired(DateTimeOffset utcNow) => utcNow > this.ExpiresAt;

    public bool HasUsableSession(UserId userId, DateTimeOffset utcNow) =>
        this.UserId == userId
        && this.Status == SessionStatus.Active
        && !this.IsExpired(utcNow);

    public bool Accepts(UserId userId, bool canAuthenticate, long securityVersion, DateTimeOffset utcNow) =>
        this.HasUsableSession(userId, utcNow)
        && canAuthenticate
        && this.UserSecurityVersion == securityVersion;
}
