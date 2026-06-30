
using Microsoft.Extensions.Logging;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Application.Common.Logging;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Users;

using SharedKernel;
namespace OpenIdentityStack.Application.Roles.Commands;
/// <summary>
/// Use case interface for assigning roles.
/// </summary>
public interface IAssignRoleUseCase
{
    /// <summary>
    /// Executes the assign role command.
    /// </summary>
    /// <param name="command">The command containing user and role IDs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> ExecuteAsync(AssignRoleCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Use case for assigning a role to a user.
/// </summary>
public sealed class AssignRoleUseCase : IAssignRoleUseCase
{
    private readonly IUserRepository userRepository;
    private readonly IRoleRepository roleRepository;
    private readonly IDateTimeProvider dateTimeProvider;
    private readonly IAuditLog auditLog;
    private readonly ILogger<AssignRoleUseCase> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssignRoleUseCase"/> class.
    /// </summary>
    /// <param name="userRepository">The user repository.</param>
    /// <param name="roleRepository">The role repository.</param>
    /// <param name="dateTimeProvider">The date/time provider.</param>
    /// <param name="auditLog">The audit log.</param>
    /// <param name="logger">The logger.</param>
    public AssignRoleUseCase(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IDateTimeProvider dateTimeProvider,
        IAuditLog auditLog,
        ILogger<AssignRoleUseCase> logger)
    {
        this.userRepository = userRepository;
        this.roleRepository = roleRepository;
        this.dateTimeProvider = dateTimeProvider;
        this.auditLog = auditLog;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> ExecuteAsync(
        AssignRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        // Fetch entities sequentially to avoid DbContext concurrency issues
        User? user = await this.userRepository.GetByIdAsync(command.UserId, cancellationToken);
        Role? role = await this.roleRepository.GetByIdAsync(command.RoleId, cancellationToken);

        // Validate user exists
        if (user is null)
        {
            return Result.Failure(
                DomainError.NotFound("UserNotFound", "User not found."));
        }

        // Validate user is active
        if (user.Status == UserStatus.Disabled)
        {
            return Result.Failure(
                DomainError.Validation("UserDisabled", "Cannot assign role to a disabled user."));
        }

        // Validate role exists
        if (role is null)
        {
            return Result.Failure(
                DomainError.NotFound("RoleNotFound", "Role not found."));
        }

        // Validate role is active
        if (!role.IsActive)
        {
            return Result.Failure(
                DomainError.Validation("RoleDisabled", "Cannot assign a disabled role."));
        }

        // Check if already assigned
        bool isAssigned = await this.roleRepository.IsRoleAssignedAsync(
            command.UserId,
            command.RoleId,
            cancellationToken);

        if (isAssigned)
        {
            return Result.Failure(
                DomainError.Conflict("RoleAlreadyAssigned", "Role is already assigned to this user."));
        }

        // Create and save the assignment
        Result<RoleAssignment> assignmentResult = RoleAssignment.Create(
            command.UserId,
            command.RoleId,
            this.dateTimeProvider.UtcNow);

        if (assignmentResult.IsFailure)
        {
            return Result.Failure(assignmentResult.Error);
        }

        await this.roleRepository.AssignRoleAsync(assignmentResult.Value, cancellationToken);
        await this.roleRepository.SaveChangesAsync(cancellationToken);

        this.logger.LogRoleAssigned(command.RoleId.Value, command.UserId.Value);

        await this.auditLog.LogAsync(
            command.ActorId,
            "User.RoleAssigned",
            "User",
            command.UserId.Value.ToString(),
            $"RoleId: {command.RoleId.Value}, Role: {role.Name}",
            cancellationToken);

        return Result.Success();
    }
}
