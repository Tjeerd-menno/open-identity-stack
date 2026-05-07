namespace OpenIdentityStack.Api.Users;

/// <summary>
/// Request to create a new user.
/// </summary>
/// <param name="Email">The user's email address.</param>
/// <param name="DisplayName">The user's display name.</param>
/// <param name="Password">The user's password.</param>
public sealed record CreateUserRequest(
    string Email,
    string DisplayName,
    string Password);

/// <summary>
/// Request to update a user.
/// </summary>
/// <param name="DisplayName">The new display name (optional).</param>
public sealed record UpdateUserRequest(string? DisplayName);

/// <summary>
/// Request to disable a user.
/// </summary>
/// <param name="Reason">The reason for disabling the user.</param>
public sealed record DisableUserRequest(string Reason);

/// <summary>
/// Request to reset a user's password.
/// </summary>
/// <param name="NewPassword">The new password.</param>
public sealed record ResetPasswordRequest(string NewPassword);

/// <summary>
/// Request to link an upstream identity to a user.
/// </summary>
/// <param name="ProviderId">The upstream provider ID.</param>
/// <param name="SubjectId">The subject ID from the upstream provider.</param>
/// <param name="Email">The email from the upstream provider (optional).</param>
public sealed record LinkUpstreamIdentityRequest(
    Guid ProviderId,
    string SubjectId,
    string? Email);
