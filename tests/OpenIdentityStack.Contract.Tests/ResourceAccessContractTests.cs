namespace OpenIdentityStack.Contract.Tests;

public sealed class ResourceAccessContractTests
{
    [Fact]
    public void PublicContractDefinesRevisionCheckedGrantRevocation()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "contracts/openapi/identity-resource-access.yaml")))
        {
            directory = directory.Parent;
        }

        directory.ShouldNotBeNull();
        string contract = File.ReadAllText(Path.Combine(directory!.FullName, "contracts/openapi/identity-resource-access.yaml"));
        contract.ShouldContain("""
          /{id}/resource-grants/{resourceId}:
        """);
        int grantPath = contract.IndexOf("/{id}/resource-grants/{resourceId}:", StringComparison.Ordinal);
        int revokeOperation = contract.IndexOf("operationId: RevokeClientResourceGrant", StringComparison.Ordinal);
        int components = contract.IndexOf("components:", StringComparison.Ordinal);
        revokeOperation.ShouldBeGreaterThan(grantPath);
        revokeOperation.ShouldBeLessThan(components);
        contract.ShouldContain("name: expectedRevision");
        contract.ShouldContain("'204':");
        contract.ShouldContain("'409':");
        contract.ShouldContain("An empty subject-specific list withdraws token approval for that flow.");
    }
}
