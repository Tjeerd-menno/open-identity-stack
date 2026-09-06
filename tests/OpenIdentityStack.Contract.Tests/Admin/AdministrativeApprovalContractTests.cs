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
    public void ApprovalProtectedMutationDocumentsApprovalProblem(string operationId)
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
        responseContract.ShouldContain("$ref: '#/components/responses/AdministrativeApprovalRequired'");
    }

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
    public void ApprovalProtectedMutationDocumentsConflict(string operationId)
    {
        string contract = ReadContract();
        int operation = contract.IndexOf($"operationId: {operationId}", StringComparison.Ordinal);
        operation.ShouldBeGreaterThan(0);
        int responses = contract.IndexOf("      responses:", operation, StringComparison.Ordinal);
        responses.ShouldBeGreaterThan(operation);
        int operationEnd = contract.IndexOf("      x-required-permissions:", responses, StringComparison.Ordinal);
        operationEnd.ShouldBeGreaterThan(responses);

        contract[responses..operationEnd].ShouldContain("        '409':");
    }

    [Fact]
    public void ConflictResponseReferenceResolvesToProblemDetails()
    {
        string contract = ReadContract();
        int response = contract.IndexOf("    Conflict:", StringComparison.Ordinal);
        response.ShouldBeGreaterThan(0);
        int responseEnd = contract.IndexOf("\n    NotFound:", response, StringComparison.Ordinal);
        responseEnd.ShouldBeGreaterThan(response);

        string definition = contract[response..responseEnd];
        definition.ShouldContain("application/problem+json:");
        definition.ShouldContain("$ref: '#/components/schemas/ProblemDetails'");
    }

    [Fact]
    public void ApprovalProblemDeclaresErrorCodeDiscriminatorAndSupportedCodes()
    {
        string contract = ReadContract();
        int schema = contract.IndexOf("    AdministrativeApprovalProblemDetails:", StringComparison.Ordinal);
        schema.ShouldBeGreaterThan(0);
        string definition = contract[schema..];

        definition.ShouldContain("required: [errorCode]");
        definition.ShouldContain("propertyName: errorCode");
        definition.ShouldContain("Forbidden.AdministrativeApproval.HumanRequired");
        definition.ShouldContain("Forbidden.AdministrativeApproval.AuthorityRequired");
        definition.ShouldContain("Forbidden.AdministrativeApproval.ReauthenticationRequired");
        definition.ShouldContain("Forbidden.AdministrativeApproval.AcknowledgementRequired");
    }

    [Fact]
    public void ApprovalProtectedForbiddenResponseAlsoAllowsOrdinaryPermissionDenials()
    {
        string contract = ReadContract();
        int response = contract.IndexOf("    AdministrativeApprovalRequired:", StringComparison.Ordinal);
        response.ShouldBeGreaterThan(0);
        int responseEnd = contract.IndexOf("\n    NotFound:", response, StringComparison.Ordinal);
        responseEnd.ShouldBeGreaterThan(response);
        string definition = contract[response..responseEnd];

        definition.ShouldContain("oneOf:");
        definition.ShouldContain("$ref: '#/components/schemas/ProblemDetails'");
        definition.ShouldContain("$ref: '#/components/schemas/AdministrativeApprovalProblemDetails'");
    }

    [Fact]
    public void EnableRoleDocumentsRuntimeWritePermission()
    {
        string contract = ReadContract();
        int operation = contract.IndexOf("operationId: enableRole", StringComparison.Ordinal);
        operation.ShouldBeGreaterThan(0);
        int operationEnd = contract.IndexOf("operationId:", operation + "operationId: enableRole".Length, StringComparison.Ordinal);
        operationEnd.ShouldBeGreaterThan(operation);

        contract[operation..operationEnd].ShouldContain("x-required-permissions: ['roles:write']");
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
