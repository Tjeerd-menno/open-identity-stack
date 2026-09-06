namespace OpenIdentityStack.Contract.Tests.Admin;

public sealed class AdministrativeApprovalContractTests
{
    [Theory]
    [InlineData("createRole")]
    [InlineData("updateRole")]
    [InlineData("enableRole")]
    [InlineData("setRolePermissions")]
    [InlineData("addRolePermission")]
    [InlineData("enableUser")]
    [InlineData("resetUserPassword")]
    [InlineData("assignRoleToUser")]
    [InlineData("addGroupMember")]
    [InlineData("createGroupMapping")]
    public void ApprovalProtectedMutationDocumentsForbiddenProblem(string operationId)
    {
        string contract = ReadContract();
        int operation = contract.IndexOf($"operationId: {operationId}", StringComparison.Ordinal);
        operation.ShouldBeGreaterThan(0);
        int responses = contract.IndexOf("      responses:", operation, StringComparison.Ordinal);
        responses.ShouldBeGreaterThan(operation);
        int operationEnd = contract.IndexOf("      x-required-permissions:", responses, StringComparison.Ordinal);
        operationEnd.ShouldBeGreaterThan(responses);
        string responseContract = contract[responses..operationEnd];

        responseContract.ShouldContain("        '403':");
        responseContract.ShouldContain("$ref: '#/components/responses/Forbidden'");
    }

    private static string ReadContract()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName, "contracts", "openapi", "001-openiddict-iam", "admin-api.yaml");
            if (File.Exists(path)) { return File.ReadAllText(path); }
        }
        throw new FileNotFoundException("Admin API contract not found.");
    }
}
