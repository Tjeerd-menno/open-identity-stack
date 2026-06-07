using System.Collections.Immutable;
using System.Globalization;
using System.Security.Claims;

using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Users;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenIdentityStack.Api.Authentication;

public sealed record TokenClaimProjectionRequest(
    ClaimsPrincipal Principal,
    User? PersistedUser,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<GroupClaimDto> GroupClaims,
    IReadOnlyCollection<string> Scopes,
    IReadOnlyCollection<string> RequestedUserInfoClaims,
    DateTimeOffset? AuthenticationTime,
    string? Acr,
    string? SessionId);

public sealed class TokenClaimProjectionService : ITokenClaimProjectionService
{
    public const string LegacySessionIdClaim = "session_id";
    public const string RequestedUserInfoClaim = "requested_userinfo_claim";
    public const string AuthenticationContextClassReferenceClaim = "acr";

    public ClaimsPrincipal ProjectSubjectClaims(TokenClaimProjectionRequest request)
    {
        var identity = new ClaimsIdentity(
            authenticationType: OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        string userIdString = request.Principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Subject claim not found.");
        identity.AddClaim(new Claim(Claims.Subject, userIdString));

        if (request.PersistedUser?.DisplayName is { Length: > 0 } persistedDisplayName)
        {
            identity.AddClaim(new Claim(Claims.Name, persistedDisplayName));
        }
        else if (request.Principal.FindFirstValue(ClaimTypes.Name) is { } name)
        {
            identity.AddClaim(new Claim(Claims.Name, name));
        }

        if (!string.IsNullOrWhiteSpace(request.PersistedUser?.Email))
        {
            identity.AddClaim(new Claim(Claims.Email, request.PersistedUser.Email));
            identity.AddClaim(new Claim(Claims.EmailVerified, "true", ClaimValueTypes.Boolean));
        }
        else if (request.Principal.FindFirstValue(ClaimTypes.Email) is { } email)
        {
            identity.AddClaim(new Claim(Claims.Email, email));
            identity.AddClaim(new Claim(Claims.EmailVerified, "true", ClaimValueTypes.Boolean));
        }

        if (request.PersistedUser is not null)
        {
            AddPersistedProfileClaims(identity, request.PersistedUser);
        }
        else
        {
            AddPrincipalProfileClaims(identity, request.Principal);
        }

        if (request.AuthenticationTime is { } authTime)
        {
            SetAuthenticationTimeClaim(identity, authTime);
        }

        if (!string.IsNullOrWhiteSpace(request.Acr))
        {
            identity.AddClaim(new Claim(AuthenticationContextClassReferenceClaim, request.Acr));
        }

        foreach (string requestedClaim in request.RequestedUserInfoClaims)
        {
            identity.AddClaim(new Claim(RequestedUserInfoClaim, requestedClaim));
        }

        if (!string.IsNullOrWhiteSpace(request.SessionId))
        {
            identity.AddClaim(new Claim("sid", request.SessionId));
            identity.AddClaim(new Claim(LegacySessionIdClaim, request.SessionId));
        }

        foreach (string role in request.Roles)
        {
            identity.AddClaim(new Claim(Claims.Role, role));
        }

        foreach (string permission in request.Permissions)
        {
            identity.AddClaim(new Claim("permission", permission));
        }

        foreach (GroupClaimDto groupClaim in request.GroupClaims)
        {
            identity.AddClaim(CreateGroupClaim(groupClaim));
        }

        ClaimsPrincipal principal = new(identity);
        principal.SetScopes(request.Scopes.ToImmutableArray());
        principal.SetDestinations(GetDestinations);
        return principal;
    }

    public ClaimsPrincipal ProjectExistingPrincipal(
        ClaimsPrincipal principal,
        DateTimeOffset? authenticationTime = null)
    {
        var identity = new ClaimsIdentity(
            principal.Claims.Where(claim =>
                !string.Equals(claim.Type, LegacySessionIdClaim, StringComparison.Ordinal)
                && !string.Equals(claim.Type, Claims.AuthenticationTime, StringComparison.Ordinal)),
            authenticationType: OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        ClaimsPrincipal projected = new(identity);
        projected.SetScopes(principal.GetScopes());
        projected.SetResources(principal.GetResources());

        if (authenticationTime is { } authTime)
        {
            SetAuthenticationTimeClaim(identity, authTime);
        }

        projected.SetDestinations(GetDestinations);
        return projected;
    }

    public IReadOnlyDictionary<string, object> CreateUserInfoResponse(ClaimsPrincipal principal)
    {
        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Claims.Subject] = principal.GetClaim(Claims.Subject)!
        };

        ImmutableHashSet<string> requestedUserInfoClaims = GetRequestedUserInfoClaims(principal);

        AddUserInfoStringClaim(claims, principal, requestedUserInfoClaims, Claims.Name, Scopes.Profile);
        AddUserInfoStringClaim(claims, principal, requestedUserInfoClaims, Claims.GivenName, Scopes.Profile);
        AddUserInfoStringClaim(claims, principal, requestedUserInfoClaims, Claims.FamilyName, Scopes.Profile);
        AddUserInfoStringClaim(claims, principal, requestedUserInfoClaims, Claims.MiddleName, Scopes.Profile);
        AddUserInfoStringClaim(claims, principal, requestedUserInfoClaims, Claims.Nickname, Scopes.Profile);
        AddUserInfoStringClaim(claims, principal, requestedUserInfoClaims, Claims.PreferredUsername, Scopes.Profile);
        AddUserInfoStringClaim(claims, principal, requestedUserInfoClaims, Claims.Profile, Scopes.Profile);
        AddUserInfoStringClaim(claims, principal, requestedUserInfoClaims, Claims.Picture, Scopes.Profile);
        AddUserInfoStringClaim(claims, principal, requestedUserInfoClaims, Claims.Website, Scopes.Profile);
        AddUserInfoStringClaim(claims, principal, requestedUserInfoClaims, Claims.Gender, Scopes.Profile);
        AddUserInfoStringClaim(claims, principal, requestedUserInfoClaims, Claims.Birthdate, Scopes.Profile);
        AddUserInfoStringClaim(claims, principal, requestedUserInfoClaims, Claims.Zoneinfo, Scopes.Profile);
        AddUserInfoStringClaim(claims, principal, requestedUserInfoClaims, Claims.Locale, Scopes.Profile);
        AddUserInfoIntegerClaim(claims, principal, requestedUserInfoClaims, Claims.UpdatedAt, Scopes.Profile);

        AddUserInfoStringClaim(claims, principal, requestedUserInfoClaims, Claims.Email, Scopes.Email);

        if (principal.HasScope(Scopes.Email) || requestedUserInfoClaims.Contains(Claims.EmailVerified))
        {
            if (principal.GetClaim(Claims.EmailVerified) is { } emailVerified)
            {
                claims[Claims.EmailVerified] = string.Equals(emailVerified, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        return claims;
    }

    public static IEnumerable<string> GetDestinations(Claim claim)
    {
        if (IsInternalTokenStateClaim(claim.Type))
        {
            yield break;
        }

        if (claim.Properties.TryGetValue("destinations", out string? destinations))
        {
            if (!string.IsNullOrEmpty(destinations))
            {
                foreach (string dest in destinations.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    yield return dest;
                }

                yield break;
            }
        }

        switch (claim.Type)
        {
            case RequestedUserInfoClaim:
                yield return Destinations.AccessToken;
                yield break;

            case Claims.Name
                or Claims.PreferredUsername
                or Claims.GivenName
                or Claims.FamilyName
                or Claims.MiddleName
                or Claims.Nickname
                or Claims.Profile
                or Claims.Picture
                or Claims.Website
                or Claims.Gender
                or Claims.Birthdate
                or Claims.Zoneinfo
                or Claims.Locale
                or Claims.UpdatedAt:
                if (claim.Subject?.HasScope(Scopes.Profile) == true
                    || claim.Subject?.HasClaim(RequestedUserInfoClaim, claim.Type) == true)
                {
                    yield return Destinations.AccessToken;
                }

                if (claim.Subject?.HasScope(Scopes.Profile) == true)
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case Claims.Email or Claims.EmailVerified:
                if (claim.Subject?.HasScope(Scopes.Email) == true
                    || claim.Subject?.HasClaim(RequestedUserInfoClaim, claim.Type) == true)
                {
                    yield return Destinations.AccessToken;
                }

                if (claim.Subject?.HasScope(Scopes.Email) == true)
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case Claims.Role:
                yield return Destinations.AccessToken;

                if (claim.Subject?.HasScope(Scopes.Roles) == true)
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case "sid":
                if (claim.Subject?.HasScope(Scopes.OpenId) == true)
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case Claims.AuthenticationTime:
                if (claim.Subject?.HasScope(Scopes.OpenId) == true)
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case AuthenticationContextClassReferenceClaim:
                if (claim.Subject?.HasScope(Scopes.OpenId) == true)
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case "AspNet.Identity.SecurityStamp":
                yield break;

            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }

    private static Claim CreateGroupClaim(GroupClaimDto groupClaim)
    {
        var claim = new Claim(groupClaim.Type, groupClaim.Value);
        var destinations = new List<string>();

        if (groupClaim.TokenTarget is TokenTarget.AccessToken or TokenTarget.Both)
        {
            destinations.Add(Destinations.AccessToken);
        }

        if (groupClaim.TokenTarget is TokenTarget.IdToken or TokenTarget.Both)
        {
            destinations.Add(Destinations.IdentityToken);
        }

        if (destinations.Count > 0)
        {
            claim.Properties["destinations"] = string.Join(' ', destinations);
        }

        return claim;
    }

    private static bool IsInternalTokenStateClaim(string claimType) =>
        claimType is LegacySessionIdClaim or "oi_au_id" or "oi_tkn_id"
        || claimType.StartsWith("oi_", StringComparison.Ordinal);

    private static ImmutableHashSet<string> GetRequestedUserInfoClaims(ClaimsPrincipal principal) =>
        principal.FindAll(RequestedUserInfoClaim)
            .Select(claim => claim.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToImmutableHashSet(StringComparer.Ordinal);

    private static void AddPersistedProfileClaims(ClaimsIdentity identity, User user)
    {
        AddStringClaim(identity, Claims.GivenName, user.GivenName);
        AddStringClaim(identity, Claims.FamilyName, user.FamilyName);
        AddStringClaim(identity, Claims.MiddleName, user.MiddleName);
        AddStringClaim(identity, Claims.Nickname, user.Nickname);
        AddStringClaim(identity, Claims.PreferredUsername, user.PreferredUsername);
        AddStringClaim(identity, Claims.Profile, user.Profile);
        AddStringClaim(identity, Claims.Picture, user.Picture);
        AddStringClaim(identity, Claims.Website, user.Website);
        AddStringClaim(identity, Claims.Gender, user.Gender);
        AddStringClaim(identity, Claims.Birthdate, user.Birthdate);
        AddStringClaim(identity, Claims.Zoneinfo, user.ZoneInfo);
        AddStringClaim(identity, Claims.Locale, user.Locale);

        DateTimeOffset updatedAt = user.ModifiedAt ?? user.CreatedAt;
        identity.AddClaim(new Claim(
            Claims.UpdatedAt,
            updatedAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ClaimValueTypes.Integer64));
    }

    private static void AddPrincipalProfileClaims(ClaimsIdentity identity, ClaimsPrincipal principal)
    {
        AddStringClaim(identity, Claims.GivenName, principal.FindFirstValue(Claims.GivenName));
        AddStringClaim(identity, Claims.FamilyName, principal.FindFirstValue(Claims.FamilyName));
        AddStringClaim(identity, Claims.MiddleName, principal.FindFirstValue(Claims.MiddleName));
        AddStringClaim(identity, Claims.Nickname, principal.FindFirstValue(Claims.Nickname));
        AddStringClaim(identity, Claims.PreferredUsername, principal.FindFirstValue(Claims.PreferredUsername));
        AddStringClaim(identity, Claims.Profile, principal.FindFirstValue(Claims.Profile));
        AddStringClaim(identity, Claims.Picture, principal.FindFirstValue(Claims.Picture));
        AddStringClaim(identity, Claims.Website, principal.FindFirstValue(Claims.Website));
        AddStringClaim(identity, Claims.Gender, principal.FindFirstValue(Claims.Gender));
        AddStringClaim(identity, Claims.Birthdate, principal.FindFirstValue(Claims.Birthdate));
        AddStringClaim(identity, Claims.Zoneinfo, principal.FindFirstValue(Claims.Zoneinfo));
        AddStringClaim(identity, Claims.Locale, principal.FindFirstValue(Claims.Locale));

        if (principal.FindFirstValue(Claims.UpdatedAt) is { Length: > 0 } updatedAt)
        {
            identity.AddClaim(new Claim(Claims.UpdatedAt, updatedAt, ClaimValueTypes.Integer64));
        }
    }

    private static void AddStringClaim(ClaimsIdentity identity, string claimType, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            identity.AddClaim(new Claim(claimType, value));
        }
    }

    private static void AddUserInfoStringClaim(
        Dictionary<string, object> claims,
        ClaimsPrincipal principal,
        ImmutableHashSet<string> requestedClaims,
        string claimType,
        string requiredScope)
    {
        if ((principal.HasScope(requiredScope) || requestedClaims.Contains(claimType))
            && principal.GetClaim(claimType) is { } value)
        {
            claims[claimType] = value;
        }
    }

    private static void AddUserInfoIntegerClaim(
        Dictionary<string, object> claims,
        ClaimsPrincipal principal,
        ImmutableHashSet<string> requestedClaims,
        string claimType,
        string requiredScope)
    {
        if (!(principal.HasScope(requiredScope) || requestedClaims.Contains(claimType)))
        {
            return;
        }

        if (principal.GetClaim(claimType) is { } value
            && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number))
        {
            claims[claimType] = number;
        }
    }

    private static void SetAuthenticationTimeClaim(ClaimsIdentity identity, DateTimeOffset authenticationTime)
    {
        foreach (Claim claim in identity.FindAll(Claims.AuthenticationTime).ToList())
        {
            identity.RemoveClaim(claim);
        }

        identity.AddClaim(new Claim(
            Claims.AuthenticationTime,
            authenticationTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
            ClaimValueTypes.Integer64));
    }
}
