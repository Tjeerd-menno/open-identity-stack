namespace OpenIdentityStack.Contract.Tests.Admin.Applications;

public sealed class AdministrativeAccessContractTests
{
    [Fact]
    public void SaveAdministrativeAccess_DocumentsTypedApprovalAndConflictResponses()
    {
        string contract = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "contracts",
            "openapi",
            "identity-boundaries",
            "administrative-access.openapi.yaml")));

        contract.ShouldContain("application/problem+json:");
        contract.ShouldContain("#/components/schemas/AdministrativeApprovalProblemDetails");
        contract.ShouldContain("#/components/schemas/ProblemDetails");
        contract.ShouldContain("#/components/schemas/ErrorResponse");
        contract.ShouldContain("Forbidden.AdministrativeApproval.AcknowledgementRequired");
    }
}
