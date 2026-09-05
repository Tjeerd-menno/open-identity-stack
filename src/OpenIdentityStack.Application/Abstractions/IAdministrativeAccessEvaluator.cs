using SharedKernel;

namespace OpenIdentityStack.Application.Abstractions;

public sealed record AdministrativeAccessRequest(string ClientId, UserId? UserId, IReadOnlyList<string> TokenPermissions);

/// <summary>Evaluates current client entitlement and subject authority within the issued token's ceiling.</summary>
public interface IAdministrativeAccessEvaluator
{
    Task<Result<IReadOnlyList<string>>> EvaluateAsync(AdministrativeAccessRequest request, CancellationToken cancellationToken = default);
}
