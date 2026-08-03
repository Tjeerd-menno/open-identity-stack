using System.Security.Claims;

using OpenIdentityStack.Api.Authentication;
using OpenIdentityStack.Application.Groups.Queries;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Groups;
using OpenIdentityStack.Domain.Users;
using OpenIddict.Abstractions;

using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Authentication;

public sealed class TokenClaimProjectionServiceTests
{
    private const string RequestedUserInfoClaim = "requested_userinfo_claim";

    private readonly IDateTimeProvider dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly TokenClaimProjectionService service = new();

    public TokenClaimProjectionServiceTests()
    {
        this.dateTimeProvider.UtcNow.Returns(new DateTimeOffset(2026, 1, 18, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void ProjectSubjectClaims_ProfileAndEmailClaimsAreDestinationlessWithoutScopeOrClaimsRequest()
    {
        User user = CreateUser();
        ClaimsPrincipal cookiePrincipal = CreateCookiePrincipal(user.Id.Value);

        ClaimsPrincipal projected = this.service.ProjectSubjectClaims(new TokenClaimProjectionRequest(
            cookiePrincipal,
            user,
            Roles: [],
            Permissions: [],
            GroupClaims: [],
            Scopes: [],
            RequestedUserInfoClaims: [],
            AuthenticationTime: null,
            Acr: null,
            SessionId: null));

        projected.FindFirst(OpenIddictConstants.Claims.Name)!.GetDestinations().ShouldBeEmpty();
        projected.FindFirst(OpenIddictConstants.Claims.Email)!.GetDestinations().ShouldBeEmpty();
        projected.FindFirst(OpenIddictConstants.Claims.GivenName)!.GetDestinations().ShouldBeEmpty();
    }

    [Fact]
    public void ProjectSubjectClaims_RequestedUserInfoClaimsAreAccessTokenOnly()
    {
        User user = CreateUser();
        ClaimsPrincipal cookiePrincipal = CreateCookiePrincipal(user.Id.Value);

        ClaimsPrincipal projected = this.service.ProjectSubjectClaims(new TokenClaimProjectionRequest(
            cookiePrincipal,
            user,
            Roles: [],
            Permissions: [],
            GroupClaims: [],
            Scopes: [OpenIddictConstants.Scopes.OpenId],
            RequestedUserInfoClaims: [OpenIddictConstants.Claims.Email],
            AuthenticationTime: null,
            Acr: null,
            SessionId: null));

        projected.FindFirst(OpenIddictConstants.Claims.Email)!.GetDestinations()
            .ShouldBe([OpenIddictConstants.Destinations.AccessToken]);
        projected.FindFirst(RequestedUserInfoClaim)!.GetDestinations()
            .ShouldBe([OpenIddictConstants.Destinations.AccessToken]);
    }

    [Fact]
    public void ProjectSubjectClaims_ProfileAndEmailScopesAddAccessTokenAndIdentityTokenDestinations()
    {
        User user = CreateUser();
        ClaimsPrincipal cookiePrincipal = CreateCookiePrincipal(user.Id.Value);

        ClaimsPrincipal projected = this.service.ProjectSubjectClaims(new TokenClaimProjectionRequest(
            cookiePrincipal,
            user,
            Roles: [],
            Permissions: [],
            GroupClaims: [],
            Scopes: [OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Email],
            RequestedUserInfoClaims: [],
            AuthenticationTime: null,
            Acr: null,
            SessionId: null));

        projected.FindFirst(OpenIddictConstants.Claims.Name)!.GetDestinations()
            .ShouldBe([OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken]);
        projected.FindFirst(OpenIddictConstants.Claims.Email)!.GetDestinations()
            .ShouldBe([OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken]);
    }

    [Fact]
    public void ProjectSubjectClaims_GroupClaimsHonorTokenTargets()
    {
        User user = CreateUser();
        ClaimsPrincipal cookiePrincipal = CreateCookiePrincipal(user.Id.Value);

        ClaimsPrincipal projected = this.service.ProjectSubjectClaims(new TokenClaimProjectionRequest(
            cookiePrincipal,
            user,
            Roles: [],
            Permissions: [],
            GroupClaims:
            [
                new GroupClaimDto("access-claim", "access", TokenTarget.AccessToken),
                new GroupClaimDto("id-claim", "id", TokenTarget.IdToken),
                new GroupClaimDto("both-claim", "both", TokenTarget.Both)
            ],
            Scopes: [],
            RequestedUserInfoClaims: [],
            AuthenticationTime: null,
            Acr: null,
            SessionId: null));

        projected.FindFirst("access-claim")!.GetDestinations().ShouldBe([OpenIddictConstants.Destinations.AccessToken]);
        projected.FindFirst("id-claim")!.GetDestinations().ShouldBe([OpenIddictConstants.Destinations.IdentityToken]);
        projected.FindFirst("both-claim")!.GetDestinations()
            .ShouldBe([OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken]);
    }

    [Fact]
    public void ProjectSubjectClaims_InternalClaimsNeverReceivePublicDestinations()
    {
        User user = CreateUser();
        var sessionId = Guid.NewGuid();
        ClaimsPrincipal cookiePrincipal = CreateCookiePrincipal(user.Id.Value);

        ClaimsPrincipal projected = this.service.ProjectSubjectClaims(new TokenClaimProjectionRequest(
            cookiePrincipal,
            user,
            Roles: [],
            Permissions: [],
            GroupClaims: [],
            Scopes: [OpenIddictConstants.Scopes.OpenId],
            RequestedUserInfoClaims: [],
            AuthenticationTime: null,
            Acr: null,
            SessionId: sessionId.ToString()));

        projected.FindFirst("session_id")!.GetDestinations().ShouldBeEmpty();

        var internalClaim = new Claim("oi_tkn_id", "token-id");
        ClaimsPrincipal refreshed = this.service.ProjectExistingPrincipal(new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(OpenIddictConstants.Claims.Subject, user.Id.Value.ToString()),
            internalClaim
        ])));

        refreshed.FindFirst("oi_tkn_id")!.GetDestinations().ShouldBeEmpty();
    }

    [Fact]
    public void CreateUserInfoResponse_UsesSameScopeAndRequestedClaimGates()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(OpenIddictConstants.Claims.Subject, "user-123"),
            new Claim(OpenIddictConstants.Claims.Name, "Alice Example"),
            new Claim(OpenIddictConstants.Claims.Email, "alice@example.test"),
            new Claim(RequestedUserInfoClaim, OpenIddictConstants.Claims.Email)
        ]));
        principal.SetScopes(OpenIddictConstants.Scopes.OpenId);

        IReadOnlyDictionary<string, object> userInfo = this.service.CreateUserInfoResponse(principal);

        userInfo.ShouldContainKey(OpenIddictConstants.Claims.Subject);
        userInfo.ShouldNotContainKey(OpenIddictConstants.Claims.Name);
        userInfo[OpenIddictConstants.Claims.Email].ShouldBe("alice@example.test");
    }

    [Fact]
    public void ProjectSubjectClaims_AddressAndPhoneFollowTheirOwnScopes()
    {
        User user = CreateUser();
        ClaimsPrincipal cookiePrincipal = CreateCookiePrincipal(user.Id.Value);

        ClaimsPrincipal projected = this.service.ProjectSubjectClaims(new TokenClaimProjectionRequest(
            cookiePrincipal,
            user,
            Roles: [],
            Permissions: [],
            GroupClaims: [],
            Scopes: [OpenIddictConstants.Scopes.Address, OpenIddictConstants.Scopes.Phone],
            RequestedUserInfoClaims: [],
            AuthenticationTime: null,
            Acr: null,
            SessionId: null));

        // #322 settled one uniform rule for all four section 5.4 scopes: both destinations.
        projected.FindFirst(OpenIddictConstants.Claims.Address)!.GetDestinations()
            .ShouldBe([OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken]);
        projected.FindFirst(OpenIddictConstants.Claims.PhoneNumber)!.GetDestinations()
            .ShouldBe([OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken]);
        projected.FindFirst(OpenIddictConstants.Claims.PhoneNumberVerified)!.GetDestinations()
            .ShouldBe([OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken]);
    }

    [Fact]
    public void ProjectSubjectClaims_AddressAndPhoneAreDestinationlessWithoutTheirScopes()
    {
        User user = CreateUser();
        ClaimsPrincipal cookiePrincipal = CreateCookiePrincipal(user.Id.Value);

        ClaimsPrincipal projected = this.service.ProjectSubjectClaims(new TokenClaimProjectionRequest(
            cookiePrincipal,
            user,
            Roles: [],
            Permissions: [],
            GroupClaims: [],
            Scopes: [OpenIddictConstants.Scopes.Profile, OpenIddictConstants.Scopes.Email],
            RequestedUserInfoClaims: [],
            AuthenticationTime: null,
            Acr: null,
            SessionId: null));

        projected.FindFirst(OpenIddictConstants.Claims.Address)!.GetDestinations().ShouldBeEmpty();
        projected.FindFirst(OpenIddictConstants.Claims.PhoneNumber)!.GetDestinations().ShouldBeEmpty();
        projected.FindFirst(OpenIddictConstants.Claims.PhoneNumberVerified)!.GetDestinations().ShouldBeEmpty();
    }

    [Fact]
    public void CreateUserInfoResponse_EmitsAddressAsObjectAndPhoneVerifiedAsBoolean()
    {
        User user = CreateUser();
        ClaimsPrincipal cookiePrincipal = CreateCookiePrincipal(user.Id.Value);

        ClaimsPrincipal projected = this.service.ProjectSubjectClaims(new TokenClaimProjectionRequest(
            cookiePrincipal,
            user,
            Roles: [],
            Permissions: [],
            GroupClaims: [],
            Scopes: [OpenIddictConstants.Scopes.Address, OpenIddictConstants.Scopes.Phone],
            RequestedUserInfoClaims: [],
            AuthenticationTime: null,
            Acr: null,
            SessionId: null));

        IReadOnlyDictionary<string, object> userInfo = this.service.CreateUserInfoResponse(projected);

        // The suite validates `address` as a JSON object at FAILURE level; a bare string is a hard fail.
        IReadOnlyDictionary<string, object> address =
            userInfo[OpenIddictConstants.Claims.Address].ShouldBeAssignableTo<IReadOnlyDictionary<string, object>>()!;
        address["formatted"].ShouldBe("Keizersgracht 1\n1015 CJ Amsterdam\nNetherlands");
        address["street_address"].ShouldBe("Keizersgracht 1");
        address["locality"].ShouldBe("Amsterdam");
        address["region"].ShouldBe("Noord-Holland");
        address["postal_code"].ShouldBe("1015 CJ");
        address["country"].ShouldBe("Netherlands");

        userInfo[OpenIddictConstants.Claims.PhoneNumber].ShouldBe("+31 20 555 0100");
        // Boolean, not the string "false" -- the suite checks the type.
        userInfo[OpenIddictConstants.Claims.PhoneNumberVerified].ShouldBe(false);
    }

    [Fact]
    public void CreateUserInfoResponse_OmitsAddressAndPhoneWithoutTheirScopes()
    {
        User user = CreateUser();
        ClaimsPrincipal cookiePrincipal = CreateCookiePrincipal(user.Id.Value);

        ClaimsPrincipal projected = this.service.ProjectSubjectClaims(new TokenClaimProjectionRequest(
            cookiePrincipal,
            user,
            Roles: [],
            Permissions: [],
            GroupClaims: [],
            Scopes: [OpenIddictConstants.Scopes.Profile],
            RequestedUserInfoClaims: [],
            AuthenticationTime: null,
            Acr: null,
            SessionId: null));

        IReadOnlyDictionary<string, object> userInfo = this.service.CreateUserInfoResponse(projected);

        userInfo.ShouldNotContainKey(OpenIddictConstants.Claims.Address);
        userInfo.ShouldNotContainKey(OpenIddictConstants.Claims.PhoneNumber);
        userInfo.ShouldNotContainKey(OpenIddictConstants.Claims.PhoneNumberVerified);
    }

    [Fact]
    public void CreateUserInfoResponse_OmitsAddressEntirelyWhenTheUserHasNone()
    {
        Result<User> result = User.CreateFederated(
            "carol@example.test",
            "Carol Example",
            this.dateTimeProvider,
            new UserProfileData(GivenName: "Carol"));
        result.IsSuccess.ShouldBeTrue();

        ClaimsPrincipal projected = this.service.ProjectSubjectClaims(new TokenClaimProjectionRequest(
            CreateCookiePrincipal(result.Value.Id.Value),
            result.Value,
            Roles: [],
            Permissions: [],
            GroupClaims: [],
            Scopes: [OpenIddictConstants.Scopes.Address, OpenIddictConstants.Scopes.Phone],
            RequestedUserInfoClaims: [],
            AuthenticationTime: null,
            Acr: null,
            SessionId: null));

        IReadOnlyDictionary<string, object> userInfo = this.service.CreateUserInfoResponse(projected);

        userInfo.ShouldNotContainKey(OpenIddictConstants.Claims.Address);
        userInfo.ShouldNotContainKey(OpenIddictConstants.Claims.PhoneNumber);
        // phone_number_verified is a stored bool, so it is emitted whenever `phone` is granted.
        userInfo[OpenIddictConstants.Claims.PhoneNumberVerified].ShouldBe(false);
    }

    private User CreateUser()
    {
        UserProfileData profile = new(
            GivenName: "Alice",
            FamilyName: "Example",
            PreferredUsername: "alice.example",
            Locale: "en-NL",
            Address: new Address(
                Formatted: "Keizersgracht 1\n1015 CJ Amsterdam\nNetherlands",
                StreetAddress: "Keizersgracht 1",
                Locality: "Amsterdam",
                Region: "Noord-Holland",
                PostalCode: "1015 CJ",
                Country: "Netherlands"),
            PhoneNumber: "+31 20 555 0100");

        Result<User> result = User.CreateFederated("alice@example.test", "Alice Example", this.dateTimeProvider, profile);
        result.IsSuccess.ShouldBeTrue();
        return result.Value;
    }

    private static ClaimsPrincipal CreateCookiePrincipal(Guid userId) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "Cookie Name"),
            new Claim(ClaimTypes.Email, "cookie@example.test")
        ],
        "Cookies"));
}
