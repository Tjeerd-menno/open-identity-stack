using OpenIdentityStack.Application.Sessions.Queries;
using OpenIdentityStack.Application.Settings.Queries;
using OpenIdentityStack.Application.Users.Queries;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Settings;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Api.Tests.Authentication;

internal static class PersistedUser
{
    public static GetUserResult From(User user) => GetUserResult.From(user);

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
