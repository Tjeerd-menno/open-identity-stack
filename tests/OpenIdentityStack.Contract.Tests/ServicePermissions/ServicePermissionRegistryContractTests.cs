namespace OpenIdentityStack.Contract.Tests.ServicePermissions;

public sealed class ServicePermissionRegistryContractTests : ServicePermissionRegistryContractTestBase
{
    [Fact]
    public void ContractFilePath_IsDefined()
    {
        OpenApiContractPath.ShouldNotBeNullOrWhiteSpace();
    }
}
