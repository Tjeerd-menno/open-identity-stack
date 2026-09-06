using OpenIdentityStack.Application.Security.Commands;
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
        endpoints.MapGet("/api/admin/security/cutover-readiness", async (CredentialCutoverReadiness workflow, CancellationToken cancellationToken) =>
            Results.Ok(await workflow.EvaluateAsync(cancellationToken)))
            .RequireAuthorization(AuthorizationOptionsExtensions.AdminPolicy, Permissions.Sessions.Revoke, Permissions.Users.Read, Permissions.Applications.Read)
            .WithTags("Security").WithName("GetCredentialCutoverReadiness").Produces<CredentialCutoverPreflight>()
            .ProducesProblem(401).ProducesProblem(403);
        endpoints.MapPost("/api/admin/security/emergency-access-evidence", async (CredentialCutoverReadiness workflow, CancellationToken cancellationToken) =>
        {
            SharedKernel.Result<EmergencyAccessEvidence> result = await workflow.RecordEmergencyAccessAsync(cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : ErrorResultMapper.ToErrorResult(result.Error);
        }).RequireAuthorization(AuthorizationOptionsExtensions.AdminPolicy, Permissions.Sessions.Revoke)
            .WithTags("Security").WithName("RecordEmergencyAccessEvidence").Produces<EmergencyAccessEvidence>()
            .ProducesProblem(401).ProducesProblem(403);
        endpoints.MapPut("/api/admin/security/business-resources/{resourceId:guid}/token-window-review", async (
            Guid resourceId, ResourceTokenWindowReviewRequest request, CredentialCutoverReadiness workflow, CancellationToken cancellationToken) =>
        {
            SharedKernel.Result result = await workflow.ReviewResourceWindowAsync(new(resourceId, request.Mechanism, request.ResidualSeconds, request.EvidenceReference), cancellationToken);
            return result.IsSuccess ? Results.NoContent() : ErrorResultMapper.ToErrorResult(result.Error);
        }).RequireAuthorization(AuthorizationOptionsExtensions.AdminPolicy, Permissions.Sessions.Revoke)
            .WithTags("Security").WithName("ReviewResourceTokenWindow").Produces(204)
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404);
        endpoints.MapPost("/api/admin/security/credential-cutovers", async (CredentialCutoverRequest request, IExecuteCredentialCutoverUseCase useCase, CancellationToken cancellationToken) =>
        {
            SharedKernel.Result<CredentialCutoverResult> result = await useCase.ExecuteAsync(request.OperationId, cancellationToken);
            return result.IsSuccess ? Results.Ok(result.Value) : ErrorResultMapper.ToErrorResult(result.Error);
        }).RequireAuthorization(AuthorizationOptionsExtensions.AdminPolicy, Permissions.Sessions.Revoke)
          .WithTags("Security").WithName("ExecuteCredentialCutover")
          .Produces<CredentialCutoverResult>().ProducesProblem(403).ProducesProblem(400).ProducesProblem(409);
    }
}


public sealed record ResourceTokenWindowReviewRequest
{
    public required string Mechanism { get; init; }
    public required int ResidualSeconds { get; init; }
    public required string EvidenceReference { get; init; }
}
