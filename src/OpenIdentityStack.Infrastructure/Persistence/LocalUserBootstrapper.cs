using System.Data;
using Microsoft.EntityFrameworkCore;
using OpenIdentityStack.Application.Abstractions;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Domain.Users;
using OpenIdentityStack.Infrastructure.Audit;
using SharedKernel;

namespace OpenIdentityStack.Infrastructure.Persistence;

/// <summary>
/// Creates local installation accounts. An existing email never authorizes changes to that account.
/// </summary>
public sealed class LocalUserBootstrapper(
    OpenIdentityStackDbContext db,
    IPasswordHasher passwordHasher,
    IPasswordPolicyValidator passwordPolicyValidator,
    IDateTimeProvider clock)
{
    public async Task<bool> CreateIfAbsentAsync(
        string email,
        string displayName,
        string password,
        bool assignAdministrator,
        UserProfileData? profile = null,
        IReadOnlyList<string>? additionalAdministratorPermissions = null,
        CancellationToken cancellationToken = default)
    {
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        string normalizedEmail = email.Trim().ToUpperInvariant();
        if (await db.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken)
            || (assignAdministrator && await db.Users.AnyAsync(cancellationToken)))
        {
            return false;
        }

        Result validation = passwordPolicyValidator.ValidatePassword(password);
        if (validation.IsFailure)
        {
            throw new InvalidOperationException($"Bootstrap password does not satisfy the password policy: {validation.Error.Description}");
        }

        Role? role = assignAdministrator
            ? await db.Roles.SingleOrDefaultAsync(candidate => candidate.Name == SeedData.SystemRoles.SuperAdmin, cancellationToken)
            : null;
        if (assignAdministrator && (role is null || !role.IsSystemRole || !role.Permissions.Contains("*", StringComparer.Ordinal)))
        {
            throw new InvalidOperationException("Controlled administration bootstrap requires the existing explicit all-permissions system role.");
        }

        if (role is not null && additionalAdministratorPermissions is { Count: > 0 })
        {
            string[] permissions = role.Permissions.Concat(additionalAdministratorPermissions)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            role.SetPermissions(permissions);
        }

        Result<User> creation = User.CreateBootstrap(email, displayName, passwordHasher.HashPassword(password), clock, profile);
        if (creation.IsFailure)
        {
            throw new InvalidOperationException($"Bootstrap user configuration is invalid: {creation.Error.Description}");
        }

        User user = creation.Value;
        db.Users.Add(user);
        if (role is not null)
        {
            Result<RoleAssignment> assignment = RoleAssignment.Create(user.Id, role.Id, clock.UtcNow);
            if (assignment.IsFailure)
            {
                throw new InvalidOperationException("Bootstrap administrator assignment is invalid.");
            }

            db.RoleAssignments.Add(assignment.Value);
        }

        db.AuditLogEntries.Add(new AuditLogEntry
        {
            UserId = "installation-bootstrap",
            Action = "User.BootstrapCreated",
            EntityType = "User",
            EntityId = user.Id.Value.ToString(),
            Details = assignAdministrator ? "Created initial administrator; email remains unverified." : "Created installation test account; email remains unverified.",
            Timestamp = clock.UtcNow
        });

        // Serializable isolation protects the empty-installation check across competing
        // initial administrator requests. No conflict is retried with stale authority.
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
