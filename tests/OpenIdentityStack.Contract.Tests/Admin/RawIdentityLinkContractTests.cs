namespace OpenIdentityStack.Contract.Tests.Admin;

public sealed class RawIdentityLinkContractTests
{
    [Fact]
    public void RawLinkContractAdvertisesProofRequiredDenialWithoutSuccess()
    {
        string directory = AppContext.BaseDirectory;
        string? path = null;
        while (!string.IsNullOrEmpty(directory))
        {
            string candidate = Path.Combine(directory, "contracts", "openapi", "001-openiddict-iam", "admin-api.yaml");
            if (File.Exists(candidate)) { path = candidate; break; }
            directory = Directory.GetParent(directory)?.FullName ?? string.Empty;
        }
        string contract = File.ReadAllText(path ?? throw new FileNotFoundException("Admin API contract not found."));
        int operation = contract.IndexOf("operationId: linkUpstreamIdentity", StringComparison.Ordinal);
        operation.ShouldBeGreaterThan(0);
        int start = contract.LastIndexOf("    post:", operation, StringComparison.Ordinal);
        int end = contract.IndexOf("\n  /", operation, StringComparison.Ordinal);
        string linking = contract[start..end];

        linking.ShouldContain("deprecated: true");
        linking.ShouldContain("'403':");
        linking.ShouldContain("application/problem+json:");
        linking.ShouldContain("Forbidden.UpstreamIdentity.ProofRequired");
        linking.ShouldNotContain("'201':");
        linking.ShouldNotContain("'409':");
    }
}
