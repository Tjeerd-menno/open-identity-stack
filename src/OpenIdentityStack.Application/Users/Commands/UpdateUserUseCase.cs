using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Commands;

/// <summary>
/// Implementation of the update user use case.
/// </summary>
public sealed class UpdateUserUseCase : IUpdateUserUseCase
{
    private readonly IUserRepository userRepository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;

    public UpdateUserUseCase(
        IUserRepository userRepository,
        IDateTimeProvider dateTimeProvider,
        IAuditLog auditLog)
    {
        this.userRepository = userRepository;
        this.dateTimeProvider = dateTimeProvider;
        this.auditLog = auditLog;
    }

    /// <inheritdoc />
    public async Task<Result<UpdateUserResult>> ExecuteAsync(
        UpdateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        User? user = await this.userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        bool wasPhoneNumberVerified = user.PhoneNumberVerified;
        string? previousPhoneNumber = user.PhoneNumber;

        if (!string.IsNullOrWhiteSpace(command.DisplayName))
        {
            Result result = user.UpdateDisplayName(command.DisplayName, this.dateTimeProvider);
            if (result.IsFailure)
            {
                return result.Error;
            }
        }

        if (command.Profile is not null)
        {
            // Check if preferred username is being changed and if it's already in use
            string? newPreferredUsername = ResolveProfileValue(command.Profile.PreferredUsername, user.PreferredUsername);
            if (!string.IsNullOrWhiteSpace(newPreferredUsername) &&
                !string.Equals(newPreferredUsername, user.PreferredUsername, StringComparison.OrdinalIgnoreCase))
            {
                User? existingUser = await this.userRepository.GetByPreferredUsernameAsync(
                    newPreferredUsername,
                    cancellationToken);
                if (existingUser is not null && existingUser.Id != user.Id)
                {
                    return UserErrors.PreferredUsernameAlreadyExists;
                }
            }

            string? newPhoneNumber = ResolveProfileValue(command.Profile.PhoneNumber, user.PhoneNumber);

            // A verification attests to one specific number. When the number changes and
            // the caller does not re-assert verification for the new value, the stored
            // flag must not carry over: phone_number_verified would otherwise tell relying
            // parties the OP verified a number it has never seen. Clearing the number
            // resets it for the same reason.
            //
            // The final `newPhoneNumber is not null` is not redundant with that: it also
            // rejects an explicit `PhoneNumberVerified: true` sent with no number at all,
            // which would otherwise emit a bare phone_number_verified=true claim.
            bool newPhoneNumberVerified = (command.Profile.PhoneNumberVerified
                    ?? (string.Equals(newPhoneNumber, user.PhoneNumber, StringComparison.Ordinal)
                        && user.PhoneNumberVerified))
                && newPhoneNumber is not null;

            Result profileResult = user.UpdateProfile(
                new UserProfileData(
                    ResolveProfileValue(command.Profile.GivenName, user.GivenName),
                    ResolveProfileValue(command.Profile.FamilyName, user.FamilyName),
                    ResolveProfileValue(command.Profile.MiddleName, user.MiddleName),
                    ResolveProfileValue(command.Profile.Nickname, user.Nickname),
                    newPreferredUsername,
                    ResolveProfileValue(command.Profile.Profile, user.Profile),
                    ResolveProfileValue(command.Profile.Picture, user.Picture),
                    ResolveProfileValue(command.Profile.Website, user.Website),
                    ResolveProfileValue(command.Profile.Gender, user.Gender),
                    ResolveProfileValue(command.Profile.Birthdate, user.Birthdate),
                    ResolveProfileValue(command.Profile.ZoneInfo, user.ZoneInfo),
                    ResolveProfileValue(command.Profile.Locale, user.Locale),
                    // The address is replaced as a whole -- a partial merge of six
                    // components has no sensible semantics for a postal address.
                    command.Profile.Address ?? user.Address,
                    newPhoneNumber,
                    newPhoneNumberVerified),
                this.dateTimeProvider);

            if (profileResult.IsFailure)
            {
                return profileResult.Error;
            }
        }

        // phone_number_verified is an assertion relying parties act on, so a change
        // to it has to be legible in the audit trail rather than hidden inside a
        // generic User.Updated. The numbers themselves are deliberately not logged.
        string phoneVerificationAudit = DescribePhoneVerificationChange(
            wasPhoneNumberVerified,
            previousPhoneNumber,
            user.PhoneNumberVerified,
            user.PhoneNumber);

        await this.userRepository.SaveChangesAsync(cancellationToken);

        await this.auditLog.LogAsync(
            command.ActorId,
            "User.Updated",
            "User",
            user.Id.Value.ToString(),
            $"Email: {user.Email}{phoneVerificationAudit}",
            cancellationToken);

        return new UpdateUserResult(user.Id, this.dateTimeProvider.UtcNow);
    }

    /// <summary>
    /// Describes a change to the phone verification assertion for the audit trail,
    /// or returns an empty string when nothing about it changed. Never includes the
    /// phone number itself — the audit reader needs to know that verification moved,
    /// not what it moved to.
    /// </summary>
    private static string DescribePhoneVerificationChange(
        bool wasVerified,
        string? previousNumber,
        bool isVerified,
        string? currentNumber)
    {
        bool numberChanged = !string.Equals(previousNumber, currentNumber, StringComparison.Ordinal);

        if (wasVerified == isVerified && !(wasVerified && numberChanged))
        {
            return string.Empty;
        }

        string transition = (wasVerified, isVerified) switch
        {
            (false, true) => "asserted",
            (true, false) => "cleared",

            // Verified before and after, but against a different number: the
            // assertion was carried over deliberately by the caller.
            _ => "transferred",
        };

        return $"; PhoneNumberVerified: {transition}";
    }

    private static string? ResolveProfileValue(string? requestedValue, string? existingValue) =>
        requestedValue is null
            ? existingValue
            : string.IsNullOrWhiteSpace(requestedValue) ? null : requestedValue.Trim();
}
