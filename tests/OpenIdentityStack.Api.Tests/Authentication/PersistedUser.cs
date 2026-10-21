using OpenIdentityStack.Application.Sessions.Queries;
using OpenIdentityStack.Application.Settings.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Settings;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Api.Tests.Authentication;

internal static class PersistedUser
{
    public static GetUserResult From(User user) => new(
        user.Id,
        user.Email,
        user.DisplayName,
        user.Status,
        user.MfaEnabled,
        user.LastLoginAt,
        user.CreatedAt,
        user.ModifiedAt,
        user.GetProfileData())
    {
        EmailVerified = user.EmailVerified,
        CredentialRevision = user.CredentialRevision,
    };

    public static AuthenticationSettingsDto From(AuthenticationSettings settings) =>
        new(settings.DefaultProviderId, settings.IsLocalDefault, settings.LocalFallbackEnabled, settings.UpdatedAt);

    public static GetSessionResult From(UserSession session) => new(
        session.Id,
        session.UserId,
        session.IpAddress,
        session.UserAgent,
        session.Status,
        session.ClientSessions.Count,
        session.LastActivityAt,
        session.ExpiresAt,
        session.CreatedAt);
}
