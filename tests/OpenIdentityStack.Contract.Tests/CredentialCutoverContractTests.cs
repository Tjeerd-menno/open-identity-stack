namespace OpenIdentityStack.Contract.Tests.Security;

public sealed class CredentialCutoverContractTests
{
    [Fact]
    public void PublicContractDefinesReadinessProofAndExplicitExternalWindowReview()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "contracts/openapi/identity-boundaries/credential-cutover.openapi.yaml")))
        {
            directory = directory.Parent;
        }
        directory.ShouldNotBeNull();
        string contract = File.ReadAllText(Path.Combine(directory!.FullName, "contracts/openapi/identity-boundaries/credential-cutover.openapi.yaml"));
        contract.ShouldContain("operationId: GetCredentialCutoverReadiness");
        contract.ShouldContain("operationId: RecordEmergencyAccessEvidence");
        contract.ShouldContain("operationId: ReviewResourceTokenWindow");
        contract.ShouldContain("enum: [OnlineIntrospection, ConsumerRevocation, OfflineExpiry]");
        contract.ShouldContain("'409':");
        contract.ShouldContain("quarantinedLinks, affectedUsers, federationOnlyUsers, passwordCandidates");
        contract.ShouldContain("delegatedPermissions, applicationPermissions, requiresMigrationReview");
        contract.ShouldContain("bearer: { type: http, scheme: bearer }");
        contract.ShouldContain("No supplied user, subject, issuer or session identifier can establish proof.");
        contract.ShouldContain("Changing the emergency password or otherwise rotating its credential revision invalidates the evidence.");
        contract.ShouldNotContain("sessionId:");
        int emergencyEvidence = contract.IndexOf("/api/admin/security/emergency-access-evidence:", StringComparison.Ordinal);
        int resourceReview = contract.IndexOf("/api/admin/security/business-resources/{resourceId}/token-window-review:", StringComparison.Ordinal);
        int execution = contract.IndexOf("/api/admin/security/credential-cutovers:", StringComparison.Ordinal);
        contract[emergencyEvidence..resourceReview].ShouldContain("'409':");
        contract[resourceReview..execution].ShouldContain("'409':");
    }
}
