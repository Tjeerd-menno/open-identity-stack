namespace OpenIdentityStack.Api.Users;

/// <summary>
/// Request to create a new user.
/// </summary>
/// <param name="Email">The user's email address.</param>
/// <param name="DisplayName">The user's display name.</param>
/// <param name="Password">The user's password.</param>
/// <param name="Profile">Optional profile attributes.</param>
public sealed record CreateUserRequest(
    string Email,
    string DisplayName,
    string Password,
    UserProfileRequest? Profile = null);

/// <summary>
/// Request to update a user.
/// </summary>
/// <param name="DisplayName">The new display name (optional).</param>
/// <param name="Profile">Optional profile fields to update.</param>
public sealed record UpdateUserRequest(
    string? DisplayName,
    UserProfileRequest? Profile = null);

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

/// <summary>
/// Optional user profile attributes exposed through OpenID Connect standard profile claims.
/// </summary>
public sealed record UserProfileRequest(
    string? GivenName = null,
    string? FamilyName = null,
    string? MiddleName = null,
    string? Nickname = null,
    string? PreferredUsername = null,
    string? Profile = null,
    string? Picture = null,
    string? Website = null,
    string? Gender = null,
    string? Birthdate = null,
    string? ZoneInfo = null,
    string? Locale = null,
    UserAddressRequest? Address = null,
    string? PhoneNumber = null,
    bool? PhoneNumberVerified = null);

/// <summary>
/// The OpenID Connect standard <c>address</c> claim components.
/// </summary>
public sealed record UserAddressRequest(
    string? Formatted = null,
    string? StreetAddress = null,
    string? Locality = null,
    string? Region = null,
    string? PostalCode = null,
    string? Country = null);
