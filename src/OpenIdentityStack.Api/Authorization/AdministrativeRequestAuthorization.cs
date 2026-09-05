using System.Security.Claims;
using OpenIdentityStack.Application.Abstractions;
using SharedKernel;

namespace OpenIdentityStack.Api.Authorization;

public sealed class AdministrativeRequestAuthorization(IAdministrativeAccessEvaluator evaluator)
{
    private ClaimsPrincipal? evaluatedPrincipal;
    private Task<IReadOnlyList<string>>? evaluation;

    public Task<IReadOnlyList<string>> EvaluateAsync(ClaimsPrincipal user)
    {
        if (ReferenceEquals(this.evaluatedPrincipal, user) && this.evaluation is not null) { return this.evaluation; }
        this.evaluatedPrincipal = user;
        return this.evaluation = this.EvaluateCurrentAsync(user);
    }

    private async Task<IReadOnlyList<string>> EvaluateCurrentAsync(ClaimsPrincipal user)
    {
        if (!AdministrativeTokenBoundary.TryRead(user, out string clientId, out UserId? userId)) { return []; }
        string[] permissions = user.FindAll("permission").Select(claim => claim.Value).ToArray();
        Result<IReadOnlyList<string>> current = await evaluator.EvaluateAsync(new(clientId, userId, permissions));
        return current.IsSuccess ? current.Value : [];
    }
}
