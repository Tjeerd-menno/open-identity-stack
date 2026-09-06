using System.Text.RegularExpressions;

namespace OpenIdentityStack.Contract.Tests.Admin;

public sealed class IdentityBoundaryResponseContractTests
{
    [Fact]
    public void IdentityMigrationInventoryIsPartOfCanonicalAdminContract()
    {
        string contract = ReadContract();
        int pathStart = contract.IndexOf("  /users/identity-migration-inventory:", StringComparison.Ordinal);
        pathStart.ShouldBeGreaterThan(0);
        int pathEnd = contract.IndexOf("\n  /", pathStart + 1, StringComparison.Ordinal);
        string operation = contract[pathStart..pathEnd];

        operation.ShouldContain("operationId: GetIdentityMigrationInventory");
        operation.ShouldContain("$ref: '#/components/schemas/IdentityMigrationInventoryResponse'");
        operation.ShouldContain("x-required-permissions: ['users:read']");

        string inventory = Schema(contract, "IdentityMigrationInventoryResponse");
        inventory.ShouldContain("required: [items, totalCount, page, pageSize]");
        inventory.ShouldContain("$ref: '#/components/schemas/IdentityMigrationUser'");

        string user = Schema(contract, "IdentityMigrationUser");
        user.ShouldContain("required: [userId, displayName, status, hasPasswordCredential, candidateFederationProviderIds, migrationBlocked, recoveryRequired, identities]");
        user.ShouldContain("$ref: '#/components/schemas/IdentityMigrationLink'");

        string link = Schema(contract, "IdentityMigrationLink");
        link.ShouldContain("required: [providerId, providerName, subjectId, issuer, associationEvidence, isQuarantined]");
        link.ShouldMatch(@"issuer:\s+type: \[string, 'null'\]");
        link.ShouldNotContain("nullable: true");
    }

    [Fact]
    public void ProviderTrustContractExposesMutationAndVerificationEvidence()
    {
        string contract = ReadContract();
        contract.ShouldContain("/providers/{providerId}/email-verification-trust:");
        contract.ShouldContain("operationId: setProviderEmailVerificationTrust");
        Schema(contract, "ProviderEmailVerificationTrustRequest").ShouldMatch(@"trusted:\s+type: boolean");
        Schema(contract, "ProviderResponse").ShouldMatch(@"trustEmailVerification:\s+type: boolean");
        Schema(contract, "UserResponse").ShouldMatch(@"emailVerified:\s+type: boolean");
        Schema(contract, "UserResponse").ShouldContain("$ref: '#/components/schemas/EmailVerificationEvidenceResponse'");
        string evidence = Schema(contract, "EmailVerificationEvidenceResponse");
        foreach (string field in new[] { "email", "providerId", "issuer", "verifiedAt", "withdrawnAt" })
        {
            evidence.ShouldContain(field + ":");
        }
    }

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
        response.ShouldMatch(@"issuer:\s+type: \[string, 'null'\]");
        response.ShouldMatch(@"email:\s+type: \[string, 'null'\]");
        response.ShouldMatch(@"lastLoginAt:\s+type: \[string, 'null'\]\s+format: date-time");
        response.ShouldNotContain("nullable: true");
        Schema(contract, "UpstreamIdentitiesResponse").ShouldContain("required: [items]");
        string operation = contract[contract.IndexOf("operationId: listUserUpstreamIdentities", StringComparison.Ordinal)..];
        operation[..operation.IndexOf("    post:", StringComparison.Ordinal)]
            .ShouldContain("$ref: '#/components/schemas/UpstreamIdentitiesResponse'");
    }

    [Fact]
    public void DeleteAndProviderIdentityUnlinkDocumentQuarantineRetentionProblem()
    {
        string contract = ReadContract();
        string deleteUser = ContractPathBlock(contract, "/users/{userId}:");
        string unlink = ContractPathBlock(contract, "/users/{userId}/upstream-identities/{providerId}:");

        deleteUser.ShouldContain("code: Forbidden.UpstreamIdentity.QuarantineRetentionRequired");
        unlink.ShouldContain("operationId: unlinkUpstreamIdentity");
        unlink.ShouldContain("code: Forbidden.UpstreamIdentity.QuarantineRetentionRequired");
    }

    private static string Schema(string contract, string name) =>
        Regex.Match(contract, @"^    " + name + @":\r?\n[\s\S]*?(?=^    \w|\z)", RegexOptions.Multiline).Value;

    private static string ContractPathBlock(string contract, string path) =>
        Regex.Match(contract, @"^  " + Regex.Escape(path) + @"\r?\n[\s\S]*?(?=^  /|^components:|\z)", RegexOptions.Multiline).Value;

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
