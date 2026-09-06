using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Users.Queries;

/// <summary>
/// Implementation of the get user query handler.
/// </summary>
public sealed class GetUserQueryHandler : IGetUserQueryHandler
{
    private readonly IUserRepository userRepository;

    public GetUserQueryHandler(IUserRepository userRepository)
    {
        this.userRepository = userRepository;
    }

    /// <inheritdoc />
    public async Task<Result<GetUserResult>> HandleAsync(
        GetUserQuery query,
        CancellationToken cancellationToken = default)
    {
        User? user = await this.userRepository.GetByIdAsync(query.UserId, cancellationToken);
        if (user is null)
        {
            return UserErrors.NotFound;
        }

        return new GetUserResult(
            user.Id,
            user.Email,
            user.DisplayName,
            user.Status,
            user.MfaEnabled,
            user.LastLoginAt,
            user.CreatedAt,
            user.ModifiedAt,
            user.GetProfileData())
        {
            EmailVerified = user.EmailVerified,
            EmailVerificationEvidence = user.EmailVerificationEvidence.Select(e => new EmailVerificationEvidenceDto(
                e.NormalizedEmail, e.ProviderId, e.Issuer, e.VerifiedAt, e.WithdrawnAt)).ToList()
        };
    }
}
