using OpenIdentityStack.Application.ApplicationPermissions.Dtos;

namespace OpenIdentityStack.Application.Abstractions;

public interface IPermissionDiagnosticsReader
{
    Task<PermissionDiagnosticsDto> ListIssuesAsync(CancellationToken cancellationToken = default);
}
