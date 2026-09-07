using SharedKernel;

namespace OpenIdentityStack.Application.Abstractions;

public sealed record AdministrativeAccessRequest(string ClientId, UserId? UserId, IReadOnlyList<string> TokenPermissions);

public sealed record AdministrativeAccessEvaluation(
    IReadOnlyList<string> Permissions,
    IReadOnlyDictionary<Guid, long> GrantRevisions);

/// <summary>Evaluates current client entitlement and subject authority within the issued token's ceiling.</summary>
public interface IAdministrativeAccessEvaluator
{
    Task<Result<IReadOnlyList<string>>> EvaluateAsync(AdministrativeAccessRequest request, CancellationToken cancellationToken = default);
}

public interface IAdministrativeAccessProjectionEvaluator : IAdministrativeAccessEvaluator
{
    Task<Result<AdministrativeAccessEvaluation>> EvaluateProjectionAsync(
        AdministrativeAccessRequest request,
        CancellationToken cancellationToken = default);
}
