namespace OpenIdentityStack.Contract.Tests.ApplicationPermissions;

public sealed class ApplicationPermissionRegistryContractTests : ApplicationPermissionRegistryContractTestBase
{
    [Fact]
    public void ContractFilePath_IsDefined()
    {
        OpenApiContractPath.ShouldNotBeNullOrWhiteSpace();
    }
}
