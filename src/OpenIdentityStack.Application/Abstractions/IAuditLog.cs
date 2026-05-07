namespace OpenIdentityStack.Application.Abstractions;

/// <summary>
/// Provides audit logging capabilities for tracking administrative actions.
/// </summary>
public interface IAuditLog
{
    /// <summary>
    /// Logs an action performed by a user.
    /// </summary>
    /// <param name="userId">The ID of the user performing the action.</param>
    /// <param name="action">The action being performed (e.g., "CreateUser", "DeleteRole").</param>
    /// <param name="entityType">The type of entity being acted upon.</param>
    /// <param name="entityId">The ID of the entity being acted upon.</param>
    /// <param name="details">Optional additional details about the action.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LogAsync(
        string userId,
        string action,
        string entityType,
        string entityId,
        string? details = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Logs an action with before and after state.
    /// </summary>
    /// <param name="userId">The ID of the user performing the action.</param>
    /// <param name="action">The action being performed.</param>
    /// <param name="entityType">The type of entity being acted upon.</param>
    /// <param name="entityId">The ID of the entity being acted upon.</param>
    /// <param name="beforeState">The state before the action (serialized as JSON).</param>
    /// <param name="afterState">The state after the action (serialized as JSON).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LogChangeAsync(
        string userId,
        string action,
        string entityType,
        string entityId,
        string? beforeState,
        string? afterState,
        CancellationToken cancellationToken = default);
}
