using System.Text.RegularExpressions;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Federation;

using SharedKernel;
namespace OpenIdentityStack.Domain.Users;

/// <summary>
/// Represents a user who can authenticate to the system.
/// </summary>
public sealed partial class User : AggregateRoot<UserId>
{
    private static readonly Regex emailRegex = GenerateEmailRegex();
    private readonly List<UpstreamIdentity> upstreamIdentities = [];
    private readonly List<RoleId> roleIds = [];
    private readonly List<EmailVerificationEvidence> emailVerificationEvidence = [];

    public IReadOnlyList<EmailVerificationEvidence> EmailVerificationEvidence => this.emailVerificationEvidence.AsReadOnly();

    public bool EmailVerified => !string.IsNullOrWhiteSpace(this.Email) &&
        this.emailVerificationEvidence.Any(e => e.WithdrawnAt is null && e.NormalizedEmail == this.NormalizedEmail);

    /// <summary>
    /// Gets the user's email address.
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the normalized (uppercase) email for lookups.
    /// </summary>
    public string NormalizedEmail { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the user's display name.
    /// </summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the user's given name.
    /// </summary>
    public string? GivenName { get; private set; }

    /// <summary>
    /// Gets the user's family name.
    /// </summary>
    public string? FamilyName { get; private set; }

    /// <summary>
    /// Gets the user's middle name.
    /// </summary>
    public string? MiddleName { get; private set; }

    /// <summary>
    /// Gets the user's nickname.
    /// </summary>
    public string? Nickname { get; private set; }

    /// <summary>
    /// Gets the user's preferred username.
    /// </summary>
    public string? PreferredUsername { get; private set; }

    /// <summary>
    /// Gets the normalized (uppercase) preferred username for case-insensitive lookups.
    /// </summary>
    public string? NormalizedPreferredUsername { get; private set; }

    /// <summary>
    /// Gets the user's profile page URL.
    /// </summary>
    public string? Profile { get; private set; }

    /// <summary>
    /// Gets the user's profile picture URL.
    /// </summary>
    public string? Picture { get; private set; }

    /// <summary>
    /// Gets the user's website URL.
    /// </summary>
    public string? Website { get; private set; }

    /// <summary>
    /// Gets the user's gender.
    /// </summary>
    public string? Gender { get; private set; }

    /// <summary>
    /// Gets the user's birthdate.
    /// </summary>
    public string? Birthdate { get; private set; }

    /// <summary>
    /// Gets the user's time zone identifier.
    /// </summary>
    public string? ZoneInfo { get; private set; }

    /// <summary>
    /// Gets the user's locale.
    /// </summary>
    public string? Locale { get; private set; }

    /// <summary>
    /// Gets the user's postal address, exposed as the OpenID Connect <c>address</c> claim.
    /// </summary>
    public Address? Address { get; private set; }

    /// <summary>
    /// Gets the user's phone number.
    /// </summary>
    public string? PhoneNumber { get; private set; }

    /// <summary>
    /// Gets whether the phone number has been verified. Nothing verifies it today, so it
    /// is stored and defaults to <see langword="false"/> rather than being asserted true.
    /// </summary>
    public bool PhoneNumberVerified { get; private set; }

    /// <summary>
    /// Gets the hashed password. Null for federated-only users.
    /// </summary>
    public string? PasswordHash { get; private set; }

    /// <summary>
    /// Gets the current status of the user account.
    /// </summary>
    public UserStatus Status { get; private set; }

    /// <summary>
    /// Gets whether MFA is enabled for this user.
    /// </summary>
    public bool MfaEnabled { get; private set; }

    /// <summary>
    /// Gets the encrypted TOTP secret if MFA is enabled.
    /// </summary>
    public string? MfaSecret { get; private set; }

    /// <summary>
    /// Gets the last successful login time.
    /// </summary>
    public DateTimeOffset? LastLoginAt { get; private set; }

    /// <summary>
    /// Gets the linked upstream identities for federated login.
    /// </summary>
    public IReadOnlyList<UpstreamIdentity> UpstreamIdentities => this.upstreamIdentities.AsReadOnly();

    /// <summary>
    /// Gets the role IDs assigned to this user.
    /// </summary>
    public IReadOnlyList<RoleId> RoleIds => this.roleIds.AsReadOnly();

    // EF Core constructor
    private User() : base() { }

    private User(
        UserId id,
        string email,
        string displayName,
        string? passwordHash,
        UserProfileData? profile,
        DateTimeOffset createdAt) : base(id)
    {
        this.Email = email;
        this.NormalizedEmail = email.ToUpperInvariant();
        this.DisplayName = displayName;
        ApplyProfileData(profile);
        this.PasswordHash = passwordHash;
        this.Status = passwordHash is null ? UserStatus.Active : UserStatus.PendingVerification;
        this.MfaEnabled = false;
        this.MfaSecret = null;
        this.LastLoginAt = null;
        this.CreatedAt = createdAt;
    }

    /// <summary>
    /// Creates a new local user with email and password.
    /// </summary>
    public static Result<User> CreateLocal(
        string email,
        string displayName,
        string passwordHash,
        IDateTimeProvider dateTimeProvider,
        UserProfileData? profile = null)
    {
        Result validationResult = ValidateUserInput(email, displayName, profile);
        if (validationResult.IsFailure)
        {
            return validationResult.Error;
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            return UserErrors.PasswordRequired;
        }

        var user = new User(
            UserId.Create(),
            email.Trim(),
            displayName.Trim(),
            passwordHash,
            profile,
            dateTimeProvider.UtcNow);

        user.RaiseDomainEvent(new UserDomainEvents.UserCreated(
            user.Id,
            user.Email,
            user.DisplayName,
            dateTimeProvider.UtcNow));

        return user;
    }

    /// <summary>
    /// Creates a new active local account for controlled installation bootstrap.
    /// Activation is independent of email verification and cannot reactivate an existing account.
    /// </summary>
    public static Result<User> CreateBootstrap(
        string email,
        string displayName,
        string passwordHash,
        IDateTimeProvider dateTimeProvider,
        UserProfileData? profile = null)
    {
        Result<User> result = CreateLocal(email, displayName, passwordHash, dateTimeProvider, profile);
        if (result.IsSuccess)
        {
            result.Value.Status = UserStatus.Active;
        }

        return result;
    }

    /// <summary>
    /// Creates a new federated user (no password).
    /// </summary>
    public static Result<User> CreateFederated(
        string email,
        string displayName,
        IDateTimeProvider dateTimeProvider,
        UserProfileData? profile = null)
    {
        Result validationResult = ValidateUserInput(email, displayName, profile);
        if (validationResult.IsFailure)
        {
            return validationResult.Error;
        }

        var user = new User(
            UserId.Create(),
            email.Trim(),
            displayName.Trim(),
            passwordHash: null,
            profile,
            dateTimeProvider.UtcNow);

        user.RaiseDomainEvent(new UserDomainEvents.UserCreated(
            user.Id,
            user.Email,
            user.DisplayName,
            dateTimeProvider.UtcNow));

        return user;
    }

    /// <summary>
    /// Creates a new federated user with an upstream identity.
    /// </summary>
    public static Result<User> CreateFederated(
        string email,
        string displayName,
        UpstreamProviderId providerId,
        string providerName,
        string subjectId,
        UserProfileData? profile = null,
        string? issuer = null)
    {
        Result validationResult = ValidateFederatedUserInput(email, displayName, profile);
        if (validationResult.IsFailure)
        {
            return validationResult.Error;
        }

        Result<UpstreamIdentity> identityResult = UpstreamIdentity.Create(providerId, providerName, subjectId, email, issuer);
        if (identityResult.IsFailure)
        {
            return identityResult.Error;
        }

        var user = new User(
            UserId.Create(),
            email?.Trim() ?? string.Empty,
            displayName?.Trim() ?? email?.Split('@')[0] ?? "User",
            passwordHash: null,
            profile,
            DateTimeOffset.UtcNow);

        user.upstreamIdentities.Add(identityResult.Value);

        return user;
    }

    /// <summary>
    /// Verifies the user's email, activating the account.
    /// </summary>
    public Result VerifyEmail(IDateTimeProvider dateTimeProvider)
    {
        if (this.Status != UserStatus.PendingVerification)
        {
            return UserErrors.InvalidStatusTransition(this.Status, UserStatus.Active);
        }

        this.emailVerificationEvidence.Add(new EmailVerificationEvidence(this.Email, null, null, dateTimeProvider.UtcNow));
        this.Status = UserStatus.Active;
        this.SetModified(dateTimeProvider.UtcNow);

        this.RaiseDomainEvent(new UserDomainEvents.UserEmailVerified(this.Id, dateTimeProvider.UtcNow));

        return Result.Success();
    }

    /// <summary>
    /// Disables the user account.
    /// </summary>
    public Result Disable(string reason, IDateTimeProvider dateTimeProvider)
    {
        if (this.Status == UserStatus.Disabled)
        {
            return UserErrors.AlreadyDisabled;
        }

        if (this.Status == UserStatus.PendingVerification)
        {
            return UserErrors.InvalidStatusTransition(this.Status, UserStatus.Disabled);
        }

        this.Status = UserStatus.Disabled;
        this.SetModified(dateTimeProvider.UtcNow);

        this.RaiseDomainEvent(new UserDomainEvents.UserDisabled(this.Id, reason, dateTimeProvider.UtcNow));

        return Result.Success();
    }

    /// <summary>
    /// Re-enables a disabled user account.
    /// </summary>
    public Result Enable(IDateTimeProvider dateTimeProvider)
    {
        if (this.Status != UserStatus.Disabled)
        {
            return UserErrors.NotDisabled;
        }

        this.Status = UserStatus.Active;
        this.SetModified(dateTimeProvider.UtcNow);

        this.RaiseDomainEvent(new UserDomainEvents.UserEnabled(this.Id, dateTimeProvider.UtcNow));

        return Result.Success();
    }

    /// <summary>
    /// Records a successful login.
    /// </summary>
    public void RecordLogin(IDateTimeProvider dateTimeProvider)
    {
        this.LastLoginAt = dateTimeProvider.UtcNow;
        this.SetModified(dateTimeProvider.UtcNow);

        this.RaiseDomainEvent(new UserDomainEvents.UserLoggedIn(this.Id, dateTimeProvider.UtcNow));
    }

    /// <summary>
    /// Updates the user's password.
    /// </summary>
    public Result SetPassword(string newPasswordHash, IDateTimeProvider dateTimeProvider)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
        {
            return UserErrors.PasswordRequired;
        }

        this.PasswordHash = newPasswordHash;
        this.SetModified(dateTimeProvider.UtcNow);

        this.RaiseDomainEvent(new UserDomainEvents.UserPasswordChanged(this.Id, dateTimeProvider.UtcNow));

        return Result.Success();
    }

    /// <summary>
    /// Updates the user's display name.
    /// </summary>
    public Result UpdateDisplayName(string displayName, IDateTimeProvider dateTimeProvider)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return UserErrors.DisplayNameRequired;
        }

        if (displayName.Length > 256)
        {
            return UserErrors.DisplayNameTooLong;
        }

        this.DisplayName = displayName.Trim();
        this.SetModified(dateTimeProvider.UtcNow);

        return Result.Success();
    }

    /// <summary>
    /// Replaces the user's optional profile attributes.
    /// </summary>
    public Result UpdateProfile(UserProfileData profile, IDateTimeProvider dateTimeProvider)
    {
        Result validationResult = ValidateProfileData(profile);
        if (validationResult.IsFailure)
        {
            return validationResult.Error;
        }

        ApplyProfileData(profile);
        this.SetModified(dateTimeProvider.UtcNow);
        return Result.Success();
    }

    /// <summary>
    /// Returns the user's optional profile attributes.
    /// </summary>
    public UserProfileData GetProfileData() => new(
        this.GivenName,
        this.FamilyName,
        this.MiddleName,
        this.Nickname,
        this.PreferredUsername,
        this.Profile,
        this.Picture,
        this.Website,
        this.Gender,
        this.Birthdate,
        this.ZoneInfo,
        this.Locale,
        this.Address,
        this.PhoneNumber,
        this.PhoneNumberVerified);

    /// <summary>
    /// Checks if the user can authenticate.
    /// </summary>
    public bool CanAuthenticate() => this.Status == UserStatus.Active;

    /// <summary>
    /// Checks if the user has a password set.
    /// </summary>
    public bool HasPassword() => this.PasswordHash is not null;

    /// <summary>
    /// Links an upstream identity to this user.
    /// </summary>
    public Result LinkUpstreamIdentity(
        UpstreamProviderId providerId,
        string providerName,
        string subjectId,
        string? email, string? issuer = null)
    {
        if (this.upstreamIdentities.Any(i => i.ProviderId == providerId))
        {
            return UpstreamIdentityErrors.AlreadyLinked;
        }

        Result<UpstreamIdentity> identityResult = UpstreamIdentity.Create(providerId, providerName, subjectId, email, issuer);
        if (identityResult.IsFailure)
        {
            return identityResult.Error;
        }

        this.upstreamIdentities.Add(identityResult.Value);
        return Result.Success();
    }

    /// <summary>
    /// Checks if the user has an upstream identity for the given provider.
    /// </summary>
    public bool HasUpstreamIdentity(UpstreamProviderId providerId) =>
        this.upstreamIdentities.Any(i => i.ProviderId == providerId);

    /// <summary>
    /// Gets the upstream identity for a specific provider.
    /// </summary>
    public UpstreamIdentity? GetUpstreamIdentity(UpstreamProviderId providerId) =>
        this.upstreamIdentities.FirstOrDefault(i => i.ProviderId == providerId);

    /// <summary>
    /// Unlinks an upstream identity from this user.
    /// </summary>
    public Result UnlinkUpstreamIdentity(UpstreamProviderId providerId)
    {
        UpstreamIdentity? identity = this.upstreamIdentities.FirstOrDefault(i => i.ProviderId == providerId);
        if (identity is null)
        {
            return UpstreamIdentityErrors.NotLinked;
        }

        this.upstreamIdentities.Remove(identity);
        return Result.Success();
    }

    private static Result ValidateUserInput(string email, string displayName, UserProfileData? profile)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return UserErrors.EmailRequired;
        }

        string trimmedEmail = email.Trim();
        if (trimmedEmail.Length > 256)
        {
            return UserErrors.EmailTooLong;
        }

        if (!emailRegex.IsMatch(trimmedEmail))
        {
            return UserErrors.EmailInvalidFormat;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return UserErrors.DisplayNameRequired;
        }

        string trimmedDisplayName = displayName.Trim();
        if (trimmedDisplayName.Length > 256)
        {
            return UserErrors.DisplayNameTooLong;
        }

        return ValidateProfileData(profile);
    }

    private static Result ValidateFederatedUserInput(string? email, string? displayName, UserProfileData? profile)
    {
        // Email is optional for federated users
        if (!string.IsNullOrWhiteSpace(email))
        {
            string trimmedEmail = email.Trim();
            if (trimmedEmail.Length > 256)
            {
                return UserErrors.EmailTooLong;
            }

            if (!emailRegex.IsMatch(trimmedEmail))
            {
                return UserErrors.EmailInvalidFormat;
            }
        }

        // Display name is optional - we'll use email prefix or default
        if (!string.IsNullOrWhiteSpace(displayName) && displayName.Trim().Length > 256)
        {
            return UserErrors.DisplayNameTooLong;
        }

        return ValidateProfileData(profile);
    }

    private static Result ValidateProfileData(UserProfileData? profile)
    {
        if (profile is null)
        {
            return Result.Success();
        }

        Result validationResult = ValidateMaxLength(profile.GivenName, nameof(User.GivenName), 256);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        validationResult = ValidateMaxLength(profile.FamilyName, nameof(User.FamilyName), 256);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        validationResult = ValidateMaxLength(profile.MiddleName, nameof(User.MiddleName), 256);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        validationResult = ValidateMaxLength(profile.Nickname, nameof(User.Nickname), 256);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        validationResult = ValidatePreferredUsername(profile.PreferredUsername);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        validationResult = ValidateAbsoluteUri(profile.Profile, nameof(User.Profile));
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        validationResult = ValidateAbsoluteUri(profile.Picture, nameof(User.Picture));
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        validationResult = ValidateAbsoluteUri(profile.Website, nameof(User.Website));
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        validationResult = ValidateMaxLength(profile.Gender, nameof(User.Gender), 64);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        validationResult = ValidateMaxLength(profile.Birthdate, nameof(User.Birthdate), 32);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        validationResult = ValidateMaxLength(profile.ZoneInfo, nameof(User.ZoneInfo), 128);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        validationResult = ValidateMaxLength(profile.Locale, nameof(User.Locale), 35);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        validationResult = ValidateAddress(profile.Address);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        return ValidateMaxLength(profile.PhoneNumber, nameof(User.PhoneNumber), 64);
    }

    private static Result ValidateAddress(Address? address)
    {
        if (address is null)
        {
            return Result.Success();
        }

        Result validationResult = ValidateMaxLength(address.Formatted, "AddressFormatted", 512);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        validationResult = ValidateMaxLength(address.StreetAddress, "AddressStreetAddress", 256);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        validationResult = ValidateMaxLength(address.Locality, "AddressLocality", 256);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        validationResult = ValidateMaxLength(address.Region, "AddressRegion", 256);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        validationResult = ValidateMaxLength(address.PostalCode, "AddressPostalCode", 64);
        if (validationResult.IsFailure)
        {
            return validationResult;
        }

        return ValidateMaxLength(address.Country, "AddressCountry", 256);
    }

    private static Result ValidateMaxLength(string? value, string fieldName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Success();
        }

        return value.Trim().Length <= maxLength
            ? Result.Success()
            : Result.Failure(UserErrors.ProfileFieldTooLong(fieldName, maxLength));
    }

    private static Result ValidatePreferredUsername(string? preferredUsername)
    {
        if (string.IsNullOrWhiteSpace(preferredUsername))
        {
            return Result.Success();
        }

        string trimmed = preferredUsername.Trim();
        if (trimmed.Length > 64)
        {
            return Result.Failure(UserErrors.ProfileFieldTooLong(nameof(User.PreferredUsername), 64));
        }

        foreach (char character in trimmed)
        {
            if (char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
            {
                continue;
            }

            return Result.Failure(UserErrors.PreferredUsernameInvalid);
        }

        return Result.Success();
    }

    private static Result ValidateAbsoluteUri(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Success();
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri))
        {
            return Result.Failure(UserErrors.ProfileUriInvalid(fieldName));
        }

        // Only allow HTTP and HTTPS schemes to prevent XSS vectors like javascript: or data:
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return Result.Failure(UserErrors.ProfileUriInvalid(fieldName));
        }

        return Result.Success();
    }

    private static string? NormalizeOptionalValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void ApplyProfileData(UserProfileData? profile)
    {
        this.GivenName = NormalizeOptionalValue(profile?.GivenName);
        this.FamilyName = NormalizeOptionalValue(profile?.FamilyName);
        this.MiddleName = NormalizeOptionalValue(profile?.MiddleName);
        this.Nickname = NormalizeOptionalValue(profile?.Nickname);
        this.PreferredUsername = NormalizeOptionalValue(profile?.PreferredUsername);
        this.NormalizedPreferredUsername = this.PreferredUsername?.ToUpperInvariant();
        this.Profile = NormalizeOptionalValue(profile?.Profile);
        this.Picture = NormalizeOptionalValue(profile?.Picture);
        this.Website = NormalizeOptionalValue(profile?.Website);
        this.Gender = NormalizeOptionalValue(profile?.Gender);
        this.Birthdate = NormalizeOptionalValue(profile?.Birthdate);
        this.ZoneInfo = NormalizeOptionalValue(profile?.ZoneInfo);
        this.Locale = NormalizeOptionalValue(profile?.Locale);
        this.Address = NormalizeAddress(profile?.Address);
        this.PhoneNumber = NormalizeOptionalValue(profile?.PhoneNumber);
        this.PhoneNumberVerified = profile?.PhoneNumberVerified ?? false;
    }

    private static Address? NormalizeAddress(Address? address)
    {
        if (address is null)
        {
            return null;
        }

        Address normalized = new(
            NormalizeOptionalValue(address.Formatted),
            NormalizeOptionalValue(address.StreetAddress),
            NormalizeOptionalValue(address.Locality),
            NormalizeOptionalValue(address.Region),
            NormalizeOptionalValue(address.PostalCode),
            NormalizeOptionalValue(address.Country));

        // An address with nothing in it must not surface as an empty `address` claim.
        return normalized.IsEmpty ? null : normalized;
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex GenerateEmailRegex();

    /// <summary>
    /// Assigns a role to this user.
    /// </summary>
    /// <param name="roleId">The role ID to assign.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Result AssignRole(RoleId roleId)
    {
        if (this.roleIds.Contains(roleId))
        {
            return Result.Failure(DomainError.Conflict("User.RoleAlreadyAssigned", "Role is already assigned to this user."));
        }

        this.roleIds.Add(roleId);
        return Result.Success();
    }

    /// <summary>
    /// Unassigns a role from this user.
    /// </summary>
    /// <param name="roleId">The role ID to unassign.</param>
    /// <returns>A result indicating success or failure.</returns>
    public Result UnassignRole(RoleId roleId)
    {
        if (!this.roleIds.Remove(roleId))
        {
            return Result.Failure(DomainError.NotFound("User.RoleNotAssigned", "Role is not assigned to this user."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Checks if the user has a specific role.
    /// </summary>
    /// <param name="roleId">The role ID to check.</param>
    /// <returns>True if the user has the role.</returns>
    public bool HasRole(RoleId roleId) => this.roleIds.Contains(roleId);
}

/// <summary>
/// User-related domain errors.
/// </summary>
public static class UserErrors
{
    public static readonly DomainError EmailRequired =
        DomainError.Validation("User.EmailRequired", "Email is required.");

    public static readonly DomainError EmailTooLong =
        DomainError.Validation("User.EmailTooLong", "Email cannot exceed 256 characters.");

    public static readonly DomainError EmailInvalidFormat =
        DomainError.Validation("User.EmailInvalidFormat", "Email format is invalid.");

    public static readonly DomainError DisplayNameRequired =
        DomainError.Validation("User.DisplayNameRequired", "Display name is required.");

    public static readonly DomainError DisplayNameTooLong =
        DomainError.Validation("User.DisplayNameTooLong", "Display name cannot exceed 256 characters.");

    public static DomainError ProfileFieldTooLong(string fieldName, int maxLength) =>
        DomainError.Validation($"User.{fieldName}TooLong", $"{fieldName} cannot exceed {maxLength} characters.");

    public static DomainError ProfileUriInvalid(string fieldName) =>
        DomainError.Validation($"User.{fieldName}Invalid", $"{fieldName} must be an absolute URI.");

    public static readonly DomainError PreferredUsernameInvalid =
        DomainError.Validation(
            "User.PreferredUsernameInvalid",
            "PreferredUsername may only contain letters, digits, hyphens, underscores, and periods.");

    public static readonly DomainError PasswordRequired =
        DomainError.Validation("User.PasswordRequired", "Password is required for local users.");

    public static readonly DomainError AlreadyDisabled =
        DomainError.Conflict("User.AlreadyDisabled", "User is already disabled.");

    public static readonly DomainError NotDisabled =
        DomainError.Conflict("User.NotDisabled", "User is not disabled.");

    public static readonly DomainError EmailAlreadyExists =
        DomainError.Conflict("User.EmailAlreadyExists", "A user with this email already exists.");

    public static readonly DomainError PreferredUsernameAlreadyExists =
        DomainError.Conflict("User.PreferredUsernameAlreadyExists", "A user with this preferred username already exists.");

    public static readonly DomainError NotFound =
        DomainError.NotFound("User.NotFound", "User not found.");

    public static readonly DomainError InvalidCredentials =
        DomainError.Unauthorized("User.InvalidCredentials", "Invalid email or password.");

    public static readonly DomainError AccountDisabled =
        DomainError.Forbidden("User.AccountDisabled", "This account has been disabled.");

    public static readonly DomainError AccountNotVerified =
        DomainError.Forbidden("User.AccountNotVerified", "This account has not been verified.");

    public static DomainError InvalidStatusTransition(UserStatus from, UserStatus to) =>
        DomainError.Conflict("User.InvalidStatusTransition", $"Cannot transition from {from} to {to}.");
}
