namespace OpenIdentityStack.Domain.Users;

/// <summary>
/// Optional profile attributes that can be exposed as OpenID Connect standard profile claims.
/// </summary>
public sealed record UserProfileData(
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
    Address? Address = null,
    string? PhoneNumber = null,
    // Nullable so an omitted value means "unchanged", matching how the string fields
    // behave on update; the entity itself stores a plain bool defaulting to false.
    bool? PhoneNumberVerified = null);
