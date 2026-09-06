using System.Security.Claims;

using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIdentityStack.Api.Authorization;

namespace OpenIdentityStack.Api.CurrentUser;

public sealed record CurrentUserResponse(
    string Subject,
    string UserName,
    string DisplayName,
    string? Email,
    IReadOnlyList<string> Permissions);

public static partial class CurrentUserApi
{
    public static IEndpointRouteBuilder MapCurrentUserApi(this IEndpointRouteBuilder app)
    {
        app.MapGet("api/me", GetCurrentUser)
            .RequireAuthorization(AuthorizationOptionsExtensions.AdminPolicy)
            .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags(nameof(CurrentUserApi))
            .WithName("GetCurrentUser")
            .WithSummary("Gets the authenticated current user");

        return app;
    }

    public static IResult GetCurrentUser(ClaimsPrincipal user, [FromServices] ILoggerFactory loggerFactory)
    {
        ILogger logger = loggerFactory.CreateLogger(nameof(CurrentUserApi));
        string? subject = FirstClaimValue(user, OpenIddictConstants.Claims.Subject, ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject))
        {
            MissingSubject(logger);
            return TypedResults.Unauthorized();
        }

        string? name = FirstClaimValue(user, OpenIddictConstants.Claims.Name, ClaimTypes.Name);
        string? preferredUserName = FirstClaimValue(user, OpenIddictConstants.Claims.PreferredUsername);
        string? email = FirstClaimValue(user, OpenIddictConstants.Claims.Email, ClaimTypes.Email);

        var response = new CurrentUserResponse(
            subject,
            FirstNonBlank(preferredUserName, name, email, subject),
            FirstNonBlank(name, preferredUserName, email, subject),
            email,
            CollectPermissions(user));

        return TypedResults.Ok(response);
    }

    private static string[] CollectPermissions(ClaimsPrincipal user)
    {
        var permissions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Claim claim in user.Claims)
        {
            if (!IsPermissionClaim(claim))
            {
                continue;
            }

            foreach (string permission in SplitPermissionClaim(claim.Value))
            {
                permissions.TryAdd(permission, permission);
            }
        }

        return permissions.Values.ToArray();
    }

    private static bool IsPermissionClaim(Claim claim) =>
        string.Equals(claim.Type, "permission", StringComparison.Ordinal)
        || string.Equals(claim.Type, "permissions", StringComparison.Ordinal);

    private static IEnumerable<string> SplitPermissionClaim(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static permission => !string.IsNullOrWhiteSpace(permission));

    private static string? FirstClaimValue(ClaimsPrincipal user, params string[] claimTypes)
    {
        foreach (string claimType in claimTypes)
        {
            string? value = user.FindFirstValue(claimType);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string FirstNonBlank(params string?[] values) =>
        values.First(static value => !string.IsNullOrWhiteSpace(value))!;

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Current-user request principal is missing a subject claim.")]
    private static partial void MissingSubject(ILogger logger);
}
