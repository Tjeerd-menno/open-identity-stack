using System.Security.Claims;

namespace OpenIdentityStack.Api.Authentication;

public interface ITokenClaimProjectionService
{
    ClaimsPrincipal ProjectSubjectClaims(TokenClaimProjectionRequest request);

    ClaimsPrincipal ProjectExistingPrincipal(
        ClaimsPrincipal principal,
        DateTimeOffset? authenticationTime = null,
        OpenIdentityStack.Domain.Users.User? persistedUser = null);

    IReadOnlyDictionary<string, object> CreateUserInfoResponse(ClaimsPrincipal principal);
}
