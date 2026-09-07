using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Queries;

/// <summary>
/// Query to get a user by ID.
/// </summary>
/// <param name="UserId">The ID of the user to retrieve.</param>
public sealed record GetUserQuery(UserId UserId);

/// <summary>
/// Result of getting a user.
/// </summary>
/// <param name="Id">The user ID.</param>
/// <param name="Email">The user's email.</param>
/// <param name="DisplayName">The user's display name.</param>
/// <param name="Status">The user's status.</param>
/// <param name="MfaEnabled">Whether MFA is enabled.</param>
/// <param name="LastLoginAt">The last login time.</param>
/// <param name="CreatedAt">When the user was created.</param>
/// <param name="ModifiedAt">When the user was last modified.</param>
/// <param name="Profile">Optional profile attributes.</param>
public sealed record GetUserResult(
    UserId Id,
    string Email,
    string DisplayName,
    UserStatus Status,
    bool MfaEnabled,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt,
    UserProfileData Profile)
{
    public bool EmailVerified { get; init; }
    public IReadOnlyList<EmailVerificationEvidenceDto> EmailVerificationEvidence { get; init; } = [];
}

public sealed record EmailVerificationEvidenceDto(string Email, Guid? ProviderId, string? Issuer, DateTimeOffset VerifiedAt, DateTimeOffset? WithdrawnAt);

/// <summary>
/// Interface for the get user query handler.
/// </summary>
public interface IGetUserQueryHandler
{
    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    /// <param name="query">The get user query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the user or an error.</returns>
    Task<Result<GetUserResult>> HandleAsync(
        GetUserQuery query,
        CancellationToken cancellationToken = default);
}
