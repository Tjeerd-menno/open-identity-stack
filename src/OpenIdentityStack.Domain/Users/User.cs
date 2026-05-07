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
        DateTimeOffset createdAt) : base(id)
    {
        this.Email = email;
        this.NormalizedEmail = email.ToUpperInvariant();
        this.DisplayName = displayName;
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
        IDateTimeProvider dateTimeProvider)
    {
        Result validationResult = ValidateUserInput(email, displayName);
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
            dateTimeProvider.UtcNow);

        user.RaiseDomainEvent(new UserDomainEvents.UserCreated(
            user.Id,
            user.Email,
            user.DisplayName,
            dateTimeProvider.UtcNow));

        return user;
    }

    /// <summary>
    /// Creates a new federated user (no password).
    /// </summary>
    public static Result<User> CreateFederated(
        string email,
        string displayName,
        IDateTimeProvider dateTimeProvider)
    {
        Result validationResult = ValidateUserInput(email, displayName);
        if (validationResult.IsFailure)
        {
            return validationResult.Error;
        }

        var user = new User(
            UserId.Create(),
            email.Trim(),
            displayName.Trim(),
            passwordHash: null,
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
        string subjectId)
    {
        Result validationResult = ValidateFederatedUserInput(email, displayName);
        if (validationResult.IsFailure)
        {
            return validationResult.Error;
        }

        Result<UpstreamIdentity> identityResult = UpstreamIdentity.Create(providerId, providerName, subjectId, email);
        if (identityResult.IsFailure)
        {
            return identityResult.Error;
        }

        var user = new User(
            UserId.Create(),
            email?.Trim() ?? string.Empty,
            displayName?.Trim() ?? email?.Split('@')[0] ?? "User",
            passwordHash: null,
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
        string? email)
    {
        if (this.upstreamIdentities.Any(i => i.ProviderId == providerId))
        {
            return UpstreamIdentityErrors.AlreadyLinked;
        }

        Result<UpstreamIdentity> identityResult = UpstreamIdentity.Create(providerId, providerName, subjectId, email);
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

    private static Result ValidateUserInput(string email, string displayName)
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

        return Result.Success();
    }

    private static Result ValidateFederatedUserInput(string? email, string? displayName)
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

        return Result.Success();
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

    public static readonly DomainError PasswordRequired =
        DomainError.Validation("User.PasswordRequired", "Password is required for local users.");

    public static readonly DomainError AlreadyDisabled =
        DomainError.Conflict("User.AlreadyDisabled", "User is already disabled.");

    public static readonly DomainError NotDisabled =
        DomainError.Conflict("User.NotDisabled", "User is not disabled.");

    public static readonly DomainError EmailAlreadyExists =
        DomainError.Conflict("User.EmailAlreadyExists", "A user with this email already exists.");

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
