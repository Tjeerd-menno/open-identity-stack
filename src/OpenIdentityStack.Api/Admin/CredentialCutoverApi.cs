using OpenIdentityStack.Api.Authorization;
using OpenIdentityStack.Api.Common;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Authorization;

namespace OpenIdentityStack.Api.Admin;

public sealed record CredentialCutoverRequest(Guid OperationId);

public static class CredentialCutoverApi
{
    public static void MapCredentialCutoverApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/admin/security/credential-cutovers", async (CredentialCutoverRequest request, ExecuteCredentialCutover useCase, CancellationToken cancellationToken) =>
        {
            SharedKernel.Result<CredentialCutoverResult> result = await useCase.ExecuteAsync(request.OperationId, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : ErrorResultMapper.ToErrorResult(result.Error);
        }).RequireAuthorization(AuthorizationOptionsExtensions.AdminPolicy, Permissions.Sessions.Revoke)
          .WithTags("Security").WithName("ExecuteCredentialCutover")
          .Produces<CredentialCutoverResult>().ProducesProblem(403).ProducesProblem(400);
    }
}
