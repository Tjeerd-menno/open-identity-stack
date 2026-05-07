using SharedKernel;
namespace OpenIdentityStack.Api.Users;

/// <summary>
/// Response for a single user.
/// </summary>
/// <param name="Id">The user ID.</param>
/// <param name="Email">The user's email.</param>
/// <param name="DisplayName">The user's display name.</param>
/// <param name="Status">The user's status.</param>
/// <param name="MfaEnabled">Whether MFA is enabled.</param>
/// <param name="LastLoginAt">The last login time.</param>
/// <param name="CreatedAt">When the user was created.</param>
/// <param name="ModifiedAt">When the user was last modified.</param>
public sealed record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Status,
    bool MfaEnabled,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt);

/// <summary>
/// Response for a user in a list.
/// </summary>
/// <param name="Id">The user ID.</param>
/// <param name="Email">The user's email.</param>
/// <param name="DisplayName">The user's display name.</param>
/// <param name="Status">The user's status.</param>
/// <param name="CreatedAt">When the user was created.</param>
public sealed record UserListItemResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Status,
    DateTimeOffset CreatedAt);

/// <summary>
/// Paginated response for listing users.
/// </summary>
/// <param name="Items">The list of users.</param>
/// <param name="TotalCount">The total number of users.</param>
/// <param name="Page">The current page number.</param>
/// <param name="PageSize">The page size.</param>
/// <param name="TotalPages">The total number of pages.</param>
public sealed record UserListResponse(
    IReadOnlyList<UserListItemResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

/// <summary>
/// Response for user creation.
/// </summary>
/// <param name="Id">The created user's ID.</param>
/// <param name="Email">The user's email.</param>
/// <param name="DisplayName">The user's display name.</param>
/// <param name="Status">The user's status.</param>
/// <param name="CreatedAt">When the user was created.</param>
public sealed record CreateUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Status,
    DateTimeOffset CreatedAt);

/// <summary>
/// Response for disable/enable operations.
/// </summary>
/// <param name="UserId">The user ID.</param>
/// <param name="Timestamp">When the operation occurred.</param>
public sealed record UserStatusChangeResponse(
    Guid UserId,
    DateTimeOffset Timestamp);

/// <summary>
/// Response for password reset.
/// </summary>
/// <param name="UserId">The user ID.</param>
/// <param name="ResetAt">When the password was reset.</param>
public sealed record PasswordResetResponse(
    Guid UserId,
    DateTimeOffset ResetAt);

/// <summary>
/// Response for a single upstream identity.
/// </summary>
/// <param name="ProviderId">The upstream provider ID.</param>
/// <param name="ProviderName">The provider name.</param>
/// <param name="SubjectId">The subject ID from the upstream provider.</param>
/// <param name="Email">The email from the upstream provider.</param>
/// <param name="LinkedAt">When the identity was linked.</param>
/// <param name="LastLoginAt">The last login time via this identity.</param>
public sealed record UpstreamIdentityResponse(
    Guid ProviderId,
    string ProviderName,
    string SubjectId,
    string? Email,
    DateTimeOffset LinkedAt,
    DateTimeOffset? LastLoginAt);

/// <summary>
/// Response for listing user upstream identities.
/// </summary>
/// <param name="Items">The list of upstream identities.</param>
public sealed record UpstreamIdentitiesResponse(
    IReadOnlyList<UpstreamIdentityResponse> Items);

/// <summary>
/// Response for linking an upstream identity.
/// </summary>
/// <param name="UserId">The user ID.</param>
/// <param name="ProviderId">The upstream provider ID.</param>
/// <param name="SubjectId">The subject ID.</param>
/// <param name="LinkedAt">When the identity was linked.</param>
public sealed record LinkUpstreamIdentityResponse(
    Guid UserId,
    Guid ProviderId,
    string SubjectId,
    DateTimeOffset LinkedAt);
