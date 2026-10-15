using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Users;

namespace OpenIdentityStack.Application.Users.Queries;

/// <summary>
/// Loads the public profile for a preferred username when that account can authenticate.
/// </summary>
/// <param name="PreferredUsername">The preferred username from the profile URL.</param>
public sealed record GetPublicProfileQuery(string PreferredUsername);

/// <summary>
/// Profile fields rendered on the public profile page and avatar.
/// </summary>
public sealed record PublicProfileResult(
    string DisplayName,
    string? GivenName,
    string? FamilyName,
    string? Locale,
    string? ZoneInfo,
    string? Website,
    string? PreferredUsername);

/// <summary>
/// Query handler for a public profile lookup.
/// </summary>
public interface IGetPublicProfileQueryHandler
{
    /// <summary>
    /// Returns the public profile, or null when the account is missing or cannot authenticate.
    /// </summary>
    Task<PublicProfileResult?> HandleAsync(
        GetPublicProfileQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Loads a public profile through the user repository.
/// </summary>
public sealed class GetPublicProfileQueryHandler : IGetPublicProfileQueryHandler
{
    private readonly IUserRepository userRepository;

    public GetPublicProfileQueryHandler(IUserRepository userRepository)
    {
        this.userRepository = userRepository;
    }

    /// <inheritdoc />
    public async Task<PublicProfileResult?> HandleAsync(
        GetPublicProfileQuery query,
        CancellationToken cancellationToken = default)
    {
        User? user = await this.userRepository.GetByPreferredUsernameAsync(query.PreferredUsername, cancellationToken);
        if (user is null || !user.CanAuthenticate())
        {
            return null;
        }

        return new PublicProfileResult(
            user.DisplayName,
            user.GivenName,
            user.FamilyName,
            user.Locale,
            user.ZoneInfo,
            user.Website,
            user.PreferredUsername);
    }
}
