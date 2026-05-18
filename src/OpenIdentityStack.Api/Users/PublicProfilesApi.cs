using System.Text;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Mvc;

using OpenIdentityStack.Application.Abstractions;

namespace OpenIdentityStack.Api.Users;

/// <summary>
/// Public profile endpoints for users who expose profile URLs in OpenID Connect claims.
/// </summary>
internal static class PublicProfilesApi
{
    public static IEndpointRouteBuilder MapPublicProfilesApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("/profiles/{preferredUsername}", GetProfile)
            .WithName("GetPublicProfile")
            .WithSummary("Gets a public user profile page.");

        app.MapGet("/profiles/{preferredUsername}/avatar.svg", GetAvatar)
            .WithName("GetPublicProfileAvatar")
            .WithSummary("Gets a public user profile avatar.");

        return app;
    }

    private static async Task<Domain.Users.User?> GetPublicUserByPreferredUsernameAsync(
        IUserRepository userRepository,
        string preferredUsername,
        CancellationToken cancellationToken)
    {
        Domain.Users.User? user = await userRepository.GetByPreferredUsernameAsync(preferredUsername, cancellationToken);
        if (user is null || !user.CanAuthenticate())
        {
            return null;
        }

        return user;
    }

    private static async Task<IResult> GetProfile(
        [FromServices] IUserRepository userRepository,
        string preferredUsername,
        CancellationToken cancellationToken)
    {
        Domain.Users.User? user = await GetPublicUserByPreferredUsernameAsync(userRepository, preferredUsername, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        string displayName = HtmlEncoder.Default.Encode(user.DisplayName);
        string givenName = HtmlEncoder.Default.Encode(user.GivenName ?? string.Empty);
        string familyName = HtmlEncoder.Default.Encode(user.FamilyName ?? string.Empty);
        string locale = HtmlEncoder.Default.Encode(user.Locale ?? string.Empty);
        string zoneInfo = HtmlEncoder.Default.Encode(user.ZoneInfo ?? string.Empty);
        string website = HtmlEncoder.Default.Encode(user.Website ?? string.Empty);
        string avatarUrl = $"/profiles/{Uri.EscapeDataString(preferredUsername)}/avatar.svg";
        string websiteMarkup = string.IsNullOrWhiteSpace(user.Website)
            ? "<span>Not provided</span>"
            : $"""<a href="{website}">{website}</a>""";

        string html = $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>{{displayName}}</title>
              <style>
                body { font-family: Arial, sans-serif; margin: 0; background: #f6f7f9; color: #111827; }
                main { max-width: 680px; margin: 48px auto; background: #ffffff; padding: 32px; border: 1px solid #d1d5db; border-radius: 8px; }
                header { display: flex; gap: 20px; align-items: center; margin-bottom: 24px; }
                img { width: 88px; height: 88px; border-radius: 50%; border: 1px solid #d1d5db; background: #eef2ff; }
                h1 { margin: 0 0 4px; font-size: 1.75rem; }
                p { margin: 0; color: #4b5563; }
                dl { display: grid; grid-template-columns: 140px 1fr; gap: 10px 16px; margin: 0; }
                dt { font-weight: 600; color: #374151; }
                dd { margin: 0; }
                a { color: #1d4ed8; text-decoration: none; }
                a:hover { text-decoration: underline; }
              </style>
            </head>
            <body>
              <main>
                <header>
                  <img src="{{avatarUrl}}" alt="">
                  <div>
                    <h1>{{displayName}}</h1>
                    <p>@{{HtmlEncoder.Default.Encode(user.PreferredUsername ?? preferredUsername)}}</p>
                  </div>
                </header>
                <dl>
                  <dt>Given name</dt>
                  <dd>{{givenName}}</dd>
                  <dt>Family name</dt>
                  <dd>{{familyName}}</dd>
                  <dt>Locale</dt>
                  <dd>{{locale}}</dd>
                  <dt>Time zone</dt>
                  <dd>{{zoneInfo}}</dd>
                  <dt>Website</dt>
                  <dd>{{websiteMarkup}}</dd>
                </dl>
              </main>
            </body>
            </html>
            """;

        return Results.Content(html, "text/html; charset=utf-8");
    }

    private static async Task<IResult> GetAvatar(
        [FromServices] IUserRepository userRepository,
        string preferredUsername,
        CancellationToken cancellationToken)
    {
        Domain.Users.User? user = await GetPublicUserByPreferredUsernameAsync(userRepository, preferredUsername, cancellationToken);
        if (user is null)
        {
            return Results.NotFound();
        }

        string initials = GetInitials(user);
        string background = GetAvatarBackground(preferredUsername);
        string svg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256" role="img" aria-label="{{HtmlEncoder.Default.Encode(user.DisplayName)}}">
              <rect width="256" height="256" rx="32" fill="{{background}}" />
              <text x="50%" y="53%" text-anchor="middle" dominant-baseline="middle"
                    font-family="Arial, sans-serif" font-size="96" fill="#ffffff">{{initials}}</text>
            </svg>
            """;

        return Results.Content(svg, "image/svg+xml; charset=utf-8");
    }

    private static string GetInitials(Domain.Users.User user)
    {
        var builder = new StringBuilder(2);

        if (!string.IsNullOrWhiteSpace(user.GivenName))
        {
            builder.Append(char.ToUpperInvariant(user.GivenName[0]));
        }

        if (!string.IsNullOrWhiteSpace(user.FamilyName))
        {
            builder.Append(char.ToUpperInvariant(user.FamilyName[0]));
        }

        if (builder.Length == 0 && !string.IsNullOrWhiteSpace(user.DisplayName))
        {
            string[] parts = user.DisplayName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (string part in parts.Take(2))
            {
                builder.Append(char.ToUpperInvariant(part[0]));
            }
        }

        return builder.Length == 0 ? "U" : builder.ToString();
    }

    private static string GetAvatarBackground(string preferredUsername)
    {
        int hash = string.IsNullOrWhiteSpace(preferredUsername)
            ? 0
            : preferredUsername.ToUpperInvariant().Aggregate(17, (current, character) => current * 31 + character);

        string[] palette =
        [
            "#2563eb",
            "#047857",
            "#b45309",
            "#7c3aed",
            "#be123c",
            "#0f766e"
        ];

        return palette[Math.Abs(hash) % palette.Length];
    }
}
