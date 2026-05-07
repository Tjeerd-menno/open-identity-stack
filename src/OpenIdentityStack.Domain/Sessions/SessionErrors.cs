using OpenIdentityStack.Domain.Common;

using SharedKernel;
namespace OpenIdentityStack.Domain.Sessions;

/// <summary>
/// Domain errors related to sessions.
/// </summary>
public static class SessionErrors
{
    public static readonly DomainError UserIdRequired = DomainError.Validation(
        "Session.UserIdRequired",
        "User ID is required to create a session.");

    public static readonly DomainError ClientIdRequired = DomainError.Validation(
        "Session.ClientIdRequired",
        "Client ID is required to create a client session.");

    public static readonly DomainError AlreadyRevoked = DomainError.Conflict(
        "Session.AlreadyRevoked",
        "The session has already been revoked.");

    public static readonly DomainError AlreadyExpired = DomainError.Conflict(
        "Session.AlreadyExpired",
        "The session has already expired.");

    public static readonly DomainError AlreadyLoggedOut = DomainError.Conflict(
        "Session.AlreadyLoggedOut",
        "The session has already been logged out.");

    public static readonly DomainError SessionNotActive = DomainError.Conflict(
        "Session.SessionNotActive",
        "The session is not active.");

    public static readonly DomainError NotFound = DomainError.NotFound(
        "Session.NotFound",
        "The session was not found.");
}
