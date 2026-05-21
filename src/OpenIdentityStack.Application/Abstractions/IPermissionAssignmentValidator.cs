namespace OpenIdentityStack.Application.Abstractions;

public interface IPermissionAssignmentValidator
{
    Task<Result> ValidateAssignableAsync(IEnumerable<string> permissions, CancellationToken cancellationToken = default);
}
