using System.Security.Claims;
using OpenIdentityStack.Application.Abstractions;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Authorization;

internal sealed class ApprovedAdministrativeAccess : IAdministrativeAccessEvaluator
{
    public Task<Result<IReadOnlyList<string>>> EvaluateAsync(AdministrativeAccessRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult((Result<IReadOnlyList<string>>)request.TokenPermissions.ToList());

    public static ClaimsPrincipal Principal(ClaimsIdentity identity)
    {
        identity.AddClaims([new("aud", "urn:openidentitystack:admin-api"), new("scope", "ois.admin"), new("client_id", "approved-client"), new("sub", "approved-client")]);
        return new ClaimsPrincipal(identity);
    }
}
