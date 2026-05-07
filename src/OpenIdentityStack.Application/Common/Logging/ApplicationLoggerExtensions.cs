using Microsoft.Extensions.Logging;

using SharedKernel;
namespace OpenIdentityStack.Application.Common.Logging;

/// <summary>
/// High-performance structured logging extensions for the application layer.
/// Uses source-generated LoggerMessage for optimal performance (CA1848 compliance).
/// </summary>
public static partial class ApplicationLoggerExtensions
{
    // User operations
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "User created successfully. UserId: {UserId}, Email: {Email}")]
    public static partial void LogUserCreated(this ILogger logger, Guid userId, string email);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "User updated successfully. UserId: {UserId}")]
    public static partial void LogUserUpdated(this ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "User disabled. UserId: {UserId}")]
    public static partial void LogUserDisabled(this ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "User enabled. UserId: {UserId}")]
    public static partial void LogUserEnabled(this ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "User deleted. UserId: {UserId}")]
    public static partial void LogUserDeleted(this ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 1006,
        Level = LogLevel.Warning,
        Message = "User operation failed. Operation: {Operation}, Error: {ErrorCode}")]
    public static partial void LogUserOperationFailed(this ILogger logger, string operation, string errorCode);

    [LoggerMessage(
        EventId = 1007,
        Level = LogLevel.Information,
        Message = "User credentials validated. UserId: {UserId}")]
    public static partial void LogCredentialsValidated(this ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 1008,
        Level = LogLevel.Warning,
        Message = "User credentials validation failed. Email: {Email}, Reason: {Reason}")]
    public static partial void LogCredentialsValidationFailed(this ILogger logger, string email, string reason);

    // Role operations
    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Role created. RoleId: {RoleId}, Name: {RoleName}")]
    public static partial void LogRoleCreated(this ILogger logger, Guid roleId, string roleName);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Role assigned to user. RoleId: {RoleId}, UserId: {UserId}")]
    public static partial void LogRoleAssigned(this ILogger logger, Guid roleId, Guid userId);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Information,
        Message = "Role unassigned from user. RoleId: {RoleId}, UserId: {UserId}")]
    public static partial void LogRoleUnassigned(this ILogger logger, Guid roleId, Guid userId);

    // Group operations
    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Group created. GroupId: {GroupId}, Name: {GroupName}")]
    public static partial void LogGroupCreated(this ILogger logger, Guid groupId, string groupName);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "User added to group. GroupId: {GroupId}, UserId: {UserId}")]
    public static partial void LogUserAddedToGroup(this ILogger logger, Guid groupId, Guid userId);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "User removed from group. GroupId: {GroupId}, UserId: {UserId}")]
    public static partial void LogUserRemovedFromGroup(this ILogger logger, Guid groupId, Guid userId);

    [LoggerMessage(
        EventId = 3004,
        Level = LogLevel.Information,
        Message = "Group mapping added. GroupId: {GroupId}, MappingType: {MappingType}")]
    public static partial void LogGroupMappingAdded(this ILogger logger, Guid groupId, string mappingType);

    // Session operations
    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "Session created. SessionId: {SessionId}, UserId: {UserId}")]
    public static partial void LogSessionCreated(this ILogger logger, Guid sessionId, Guid userId);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Information,
        Message = "Session revoked. SessionId: {SessionId}")]
    public static partial void LogSessionRevoked(this ILogger logger, Guid sessionId);

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Information,
        Message = "All user sessions revoked. UserId: {UserId}, Count: {Count}")]
    public static partial void LogAllUserSessionsRevoked(this ILogger logger, Guid userId, int count);

    [LoggerMessage(
        EventId = 4004,
        Level = LogLevel.Information,
        Message = "Session logout processed. SessionId: {SessionId}, BackChannelNotifications: {BackChannelCount}, FrontChannelNotifications: {FrontChannelCount}")]
    public static partial void LogLogoutProcessed(this ILogger logger, Guid sessionId, int backChannelCount, int frontChannelCount);

    // Service account operations
    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Information,
        Message = "Service account created. ClientId: {ClientId}")]
    public static partial void LogServiceAccountCreated(this ILogger logger, string clientId);

    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Information,
        Message = "Service account secret rotated. ClientId: {ClientId}")]
    public static partial void LogServiceAccountSecretRotated(this ILogger logger, string clientId);

    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Information,
        Message = "Service account disabled. ClientId: {ClientId}")]
    public static partial void LogServiceAccountDisabled(this ILogger logger, string clientId);

    // Federation operations
    [LoggerMessage(
        EventId = 6001,
        Level = LogLevel.Information,
        Message = "Upstream provider created. ProviderId: {ProviderId}, Name: {ProviderName}")]
    public static partial void LogProviderCreated(this ILogger logger, Guid providerId, string providerName);

    [LoggerMessage(
        EventId = 6002,
        Level = LogLevel.Information,
        Message = "User JIT provisioned from upstream provider. UserId: {UserId}, ProviderId: {ProviderId}")]
    public static partial void LogUserJitProvisioned(this ILogger logger, Guid userId, Guid providerId);

    [LoggerMessage(
        EventId = 6003,
        Level = LogLevel.Information,
        Message = "Upstream identity linked. UserId: {UserId}, ProviderId: {ProviderId}, SubjectId: {SubjectId}")]
    public static partial void LogUpstreamIdentityLinked(this ILogger logger, Guid userId, Guid providerId, string subjectId);

    [LoggerMessage(
        EventId = 6004,
        Level = LogLevel.Information,
        Message = "Upstream identity unlinked. UserId: {UserId}, ProviderId: {ProviderId}")]
    public static partial void LogUpstreamIdentityUnlinked(this ILogger logger, Guid userId, Guid providerId);

    // Authorization/permission operations
    [LoggerMessage(
        EventId = 7001,
        Level = LogLevel.Debug,
        Message = "Permission check. UserId: {UserId}, Permission: {Permission}, Result: {HasPermission}")]
    public static partial void LogPermissionCheck(this ILogger logger, Guid userId, string permission, bool hasPermission);

    [LoggerMessage(
        EventId = 7002,
        Level = LogLevel.Warning,
        Message = "Permission denied. UserId: {UserId}, RequiredPermission: {Permission}")]
    public static partial void LogPermissionDenied(this ILogger logger, Guid userId, string permission);
}
