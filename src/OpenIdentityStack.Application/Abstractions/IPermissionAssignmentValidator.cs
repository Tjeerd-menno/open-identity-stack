namespace OpenIdentityStack.Application.Abstractions;

public interface IPermissionAssignmentValidator
{
    Task<Result> ValidateAssignableAsync(
        IEnumerable<string> permissions,
        CancellationToken cancellationToken = default);

    Task<Result> ValidateAssignableAsync(
        IEnumerable<string> permissions,
        bool acknowledgeWildcardGrant,
        CancellationToken cancellationToken = default);
}
