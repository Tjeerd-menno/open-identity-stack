using OpenIdentityStack.Application.Authorization;

namespace OpenIdentityStack.Application.Tests.Authorization;

public sealed class ApplicationPermissionMappingTests
{
    [Fact]
    public void GetAllPermissions_IncludesUnifiedApplicationPermissions()
    {
        IReadOnlyList<string> permissions = Permissions.GetAllPermissions();

        permissions.ShouldContain(Permissions.Applications.Read);
        permissions.ShouldContain(Permissions.Applications.Write);
        permissions.ShouldContain(Permissions.Applications.Delete);
        permissions.ShouldContain(Permissions.Applications.ManageCredentials);
        permissions.ShouldContain(Permissions.Applications.ManageCertificates);
    }

    [Fact]
    public void GetAllPermissions_DoesNotAdvertiseLegacyClientOrServiceAccountPermissions()
    {
        IReadOnlyList<string> permissions = Permissions.GetAllPermissions();

        permissions.ShouldNotContain(permission => permission.StartsWith("clients:", StringComparison.Ordinal));
        permissions.ShouldNotContain(permission => permission.StartsWith("service-accounts:", StringComparison.Ordinal));
    }

    [Fact]
    public void Matches_ApplicationsWildcard_CoversAllApplicationOperations()
    {
        Permissions.Matches(Permissions.Applications.All, Permissions.Applications.Read).ShouldBeTrue();
        Permissions.Matches(Permissions.Applications.All, Permissions.Applications.Write).ShouldBeTrue();
        Permissions.Matches(Permissions.Applications.All, Permissions.Applications.Delete).ShouldBeTrue();
        Permissions.Matches(Permissions.Applications.All, Permissions.Applications.ManageCredentials).ShouldBeTrue();
        Permissions.Matches(Permissions.Applications.All, Permissions.Applications.ManageCertificates).ShouldBeTrue();
        Permissions.Matches(Permissions.Applications.All, Permissions.Users.Read).ShouldBeFalse();
    }
}
