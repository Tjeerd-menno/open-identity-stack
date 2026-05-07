using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Sessions;

using SharedKernel;
namespace OpenIdentityStack.Application.Sessions.Commands;

/// <summary>
/// Command to add a client session to an existing user session.
/// This is called when a user authenticates to a client application.
/// </summary>
/// <param name="SessionId">The ID of the user session.</param>
/// <param name="ClientId">The OpenIddict client ID.</param>
/// <param name="FrontChannelLogoutUri">Optional front-channel logout URI for SLO.</param>
/// <param name="BackChannelLogoutUri">Optional back-channel logout URI for SLO.</param>
public sealed record AddClientSessionCommand(
    SessionId SessionId,
    string ClientId,
    string? FrontChannelLogoutUri = null,
    string? BackChannelLogoutUri = null);

/// <summary>
/// Use case interface for adding client sessions.
/// </summary>
public interface IAddClientSessionUseCase
{
    /// <summary>
    /// Adds a client session to an existing user session.
    /// </summary>
    /// <param name="command">The add client session command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success or failure result.</returns>
    Task<Result> ExecuteAsync(
        AddClientSessionCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of the add client session use case.
/// </summary>
public sealed class AddClientSessionUseCase : IAddClientSessionUseCase
{
    private readonly ISessionRepository sessionRepository;
    private readonly IDateTimeProvider dateTimeProvider;

    public AddClientSessionUseCase(
        ISessionRepository sessionRepository,
        IDateTimeProvider dateTimeProvider)
    {
        this.sessionRepository = sessionRepository;
        this.dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(
        AddClientSessionCommand command,
        CancellationToken cancellationToken = default)
    {
        // Get the session
        UserSession? session = await this.sessionRepository.GetByIdAsync(command.SessionId, cancellationToken);
        if (session is null)
        {
            return SessionErrors.NotFound;
        }

        // Add the client to the session
        Result result = session.AddClientSession(command.ClientId, this.dateTimeProvider);
        if (result.IsFailure)
        {
            return result;
        }

        // Persist changes
        await this.sessionRepository.UpdateAsync(session, cancellationToken);

        return Result.Success();
    }
}
