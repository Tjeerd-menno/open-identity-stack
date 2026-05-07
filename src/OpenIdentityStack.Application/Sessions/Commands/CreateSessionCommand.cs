using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Sessions;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Sessions.Commands;

/// <summary>
/// Command to create a new user session.
/// </summary>
/// <param name="UserId">The ID of the user who owns the session.</param>
/// <param name="IpAddress">The IP address from which the session was created.</param>
/// <param name="UserAgent">The user agent string from the client.</param>
/// <param name="Duration">Optional session duration. Defaults to 24 hours if not specified.</param>
public sealed record CreateSessionCommand(
    UserId UserId,
    string IpAddress,
    string UserAgent,
    TimeSpan? Duration = null);

/// <summary>
/// Result of session creation.
/// </summary>
/// <param name="SessionId">The ID of the created session.</param>
public sealed record CreateSessionResult(SessionId SessionId);

/// <summary>
/// Use case interface for creating user sessions.
/// </summary>
public interface ICreateSessionUseCase
{
    /// <summary>
    /// Creates a new user session.
    /// </summary>
    /// <param name="command">The create session command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result containing the new session ID.</returns>
    Task<Result<CreateSessionResult>> ExecuteAsync(
        CreateSessionCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of the create session use case.
/// </summary>
public sealed class CreateSessionUseCase : ICreateSessionUseCase
{
    private readonly ISessionRepository sessionRepository;
    private readonly IUserRepository userRepository;
    private readonly IDateTimeProvider dateTimeProvider;

    public CreateSessionUseCase(
        ISessionRepository sessionRepository,
        IUserRepository userRepository,
        IDateTimeProvider dateTimeProvider)
    {
        this.sessionRepository = sessionRepository;
        this.userRepository = userRepository;
        this.dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<Result<CreateSessionResult>> ExecuteAsync(
        CreateSessionCommand command,
        CancellationToken cancellationToken = default)
    {
        // Verify user exists
        User? user = await this.userRepository.GetByIdAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return DomainError.NotFound("User.NotFound", "User not found.");
        }

        // Create the session
        Result<UserSession> sessionResult = UserSession.Create(
            command.UserId,
            command.IpAddress,
            command.UserAgent,
            this.dateTimeProvider,
            command.Duration);

        if (sessionResult.IsFailure)
        {
            return sessionResult.Error;
        }

        UserSession session = sessionResult.Value;

        // Persist the session
        await this.sessionRepository.AddAsync(session, cancellationToken);

        return new CreateSessionResult(session.Id);
    }
}
