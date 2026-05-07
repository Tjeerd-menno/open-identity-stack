using OpenIdentityStack.Domain.Federation;

using SharedKernel;
namespace OpenIdentityStack.Application.Federation.Commands;

/// <summary>
/// Command for JIT provisioning a user from an upstream identity provider.
/// </summary>
/// <param name="ProviderId">The upstream provider ID.</param>
/// <param name="SubjectId">The subject ID from the upstream provider.</param>
/// <param name="Email">The email from the upstream claims (optional).</param>
/// <param name="DisplayName">The display name from the upstream claims (optional).</param>
public sealed record JitProvisionUserCommand(
    UpstreamProviderId ProviderId,
    string SubjectId,
    string? Email,
    string? DisplayName);

/// <summary>
/// Result of JIT provisioning.
/// </summary>
/// <param name="UserId">The user ID (new or existing).</param>
/// <param name="IsNewUser">Whether a new user was created.</param>
/// <param name="Email">The user's email.</param>
/// <param name="DisplayName">The user's display name.</param>
public sealed record JitProvisionUserResult(
    UserId UserId,
    bool IsNewUser,
    string? Email,
    string DisplayName);

/// <summary>
/// Interface for JIT provisioning use case.
/// </summary>
public interface IJitProvisionUserUseCase
{
    /// <summary>
    /// Provisions or retrieves a user based on upstream identity.
    /// </summary>
    Task<Result<JitProvisionUserResult>> ExecuteAsync(
        JitProvisionUserCommand command,
        CancellationToken cancellationToken = default);
}
