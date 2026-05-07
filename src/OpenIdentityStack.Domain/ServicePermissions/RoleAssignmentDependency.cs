namespace OpenIdentityStack.Domain.ServicePermissions;

public enum DependencyType
{
    Role,
    AdministrativeAssignment,
    AccessReview,
    TokenPolicy
}

public enum DependencyImpact
{
    BlocksDeletion,
    BlocksRetirement,
    WarningOnly
}

public sealed record RoleAssignmentDependency(
    string PermissionKey,
    DependencyType DependencyType,
    string DependentId,
    string DependentName,
    bool IsActive,
    DependencyImpact Impact);
