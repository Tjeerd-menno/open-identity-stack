using OpenIdentityStack.Domain.ApplicationPermissions;

namespace OpenIdentityStack.Domain.Tests.ApplicationPermissions;

public sealed class RoleAssignmentDependencyTests
{
    [Theory]
    [InlineData(DependencyType.Role, DependencyImpact.BlocksDeletion, true)]
    [InlineData(DependencyType.AdministrativeAssignment, DependencyImpact.BlocksRetirement, false)]
    [InlineData(DependencyType.AccessReview, DependencyImpact.WarningOnly, true)]
    [InlineData(DependencyType.TokenPolicy, DependencyImpact.WarningOnly, false)]
    public void Constructor_SetsAllDependencyDetails(
        DependencyType dependencyType,
        DependencyImpact impact,
        bool isActive)
    {
        var dependency = new RoleAssignmentDependency(
            "iam.users.read",
            dependencyType,
            "dependent-1",
            "Dependent name",
            isActive,
            impact);

        dependency.PermissionKey.ShouldBe("iam.users.read");
        dependency.DependencyType.ShouldBe(dependencyType);
        dependency.DependentId.ShouldBe("dependent-1");
        dependency.DependentName.ShouldBe("Dependent name");
        dependency.IsActive.ShouldBe(isActive);
        dependency.Impact.ShouldBe(impact);
    }
}
