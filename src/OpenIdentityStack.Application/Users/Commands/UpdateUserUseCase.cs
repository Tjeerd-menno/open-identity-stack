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

        if (command.Profile?.PhoneNumberVerified == true)
        {
            return UserErrors.PhoneVerificationEvidenceRequired;
        }

        bool wasPhoneNumberVerified = user.PhoneNumberVerified;

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
                    PhoneNumberVerified: false),
                this.dateTimeProvider);

            if (profileResult.IsFailure)
            {
                return profileResult.Error;
            }
        }

        // phone_number_verified is an assertion relying parties act on, so a change
        // to it has to be legible in the audit trail rather than hidden inside a
        // generic User.Updated. The numbers themselves are deliberately not logged.
        string phoneVerificationAudit = wasPhoneNumberVerified && !user.PhoneNumberVerified
            ? "; PhoneNumberVerified: cleared"
            : string.Empty;

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

    private static string? ResolveProfileValue(string? requestedValue, string? existingValue) =>
        requestedValue is null
            ? existingValue
            : string.IsNullOrWhiteSpace(requestedValue) ? null : requestedValue.Trim();
}
