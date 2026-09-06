using System.Text.RegularExpressions;

namespace OpenIdentityStack.Contract.Tests.Admin;

public sealed class IdentityBoundaryResponseContractTests
{
    [Fact]
    public void UpstreamIdentityContractIncludesRetainedEvidenceAndQuarantine()
    {
        string contract = ReadContract();
        string response = Schema(contract, "UpstreamIdentityResponse");
        response.ShouldContain("associationEvidence:");
        response.ShouldContain("enum: [Unknown, NewAccountProvisioning]");
        response.ShouldMatch(@"isQuarantined:\s+type: boolean");
        response.ShouldContain("providerId:");
        response.ShouldContain("subjectId:");
        response.ShouldContain("lastLoginAt:");
        Schema(contract, "UpstreamIdentitiesResponse").ShouldContain("required: [items]");
        string operation = contract[contract.IndexOf("operationId: listUserUpstreamIdentities", StringComparison.Ordinal)..];
        operation[..operation.IndexOf("    post:", StringComparison.Ordinal)]
            .ShouldContain("$ref: '#/components/schemas/UpstreamIdentitiesResponse'");
    }

    private static string Schema(string contract, string name) =>
        Regex.Match(contract, @"^    " + name + @":\r?\n[\s\S]*?(?=^    \w|\z)", RegexOptions.Multiline).Value;

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
