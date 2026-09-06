using System.Security.Claims;
using OpenIdentityStack.Application.Abstractions;
using SharedKernel;

namespace OpenIdentityStack.Api.Authorization;

public sealed class AdministrativeRequestAuthorization(IAdministrativeAccessEvaluator evaluator)
{
    private ClaimsPrincipal? evaluatedPrincipal;
    private Task<AdministrativeAccessEvaluation?>? evaluation;

    public async Task<IReadOnlyList<string>> EvaluateAsync(ClaimsPrincipal user) =>
        (await this.EvaluateProjectionAsync(user))?.Permissions ?? [];

    public Task<AdministrativeAccessEvaluation?> EvaluateProjectionAsync(ClaimsPrincipal user)
    {
        if (ReferenceEquals(this.evaluatedPrincipal, user) && this.evaluation is not null) { return this.evaluation; }
        this.evaluatedPrincipal = user;
        return this.evaluation = this.EvaluateCurrentAsync(user);
    }

    private async Task<AdministrativeAccessEvaluation?> EvaluateCurrentAsync(ClaimsPrincipal user)
    {
        if (!AdministrativeTokenBoundary.TryRead(user, out string clientId, out UserId? userId)) { return null; }
        string[] permissions = user.FindAll("permission").Select(claim => claim.Value).ToArray();
        if (evaluator is IAdministrativeAccessProjectionEvaluator projectionEvaluator)
        {
            Result<AdministrativeAccessEvaluation> projection = await projectionEvaluator.EvaluateProjectionAsync(
                new(clientId, userId, permissions));
            return projection.IsSuccess ? projection.Value : null;
        }

        Result<IReadOnlyList<string>> current = await evaluator.EvaluateAsync(new(clientId, userId, permissions));
        return current.IsSuccess ? new AdministrativeAccessEvaluation(current.Value, new Dictionary<Guid, long>()) : null;
    }
}
