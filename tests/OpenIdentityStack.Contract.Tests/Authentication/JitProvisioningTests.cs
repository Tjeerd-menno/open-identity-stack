namespace OpenIdentityStack.Contract.Tests.Authentication;

/// <summary>
/// Contract tests documenting JIT provisioning expectations.
/// </summary>
public sealed class JitProvisioningTests
{
    [Fact]
    public void JitProvisioning_RequiredClaims_AreDocumented()
    {
        // Minimum claims required to provision or link a user
        string[] requiredClaims = new[]
        {
            "sub", // Subject identifier
            "iss"  // Issuer identifier
        };

        requiredClaims.ShouldContain("sub");
        requiredClaims.ShouldContain("iss");
    }

    [Fact]
    public void JitProvisioning_OptionalClaims_AreDocumented()
    {
        // Optional claims used to improve user profile
        string[] optionalClaims = new[]
        {
            "email",
            "name",
            "preferred_username"
        };

        optionalClaims.ShouldContain("email");
        optionalClaims.ShouldContain("name");
    }

    [Fact]
    public void JitProvisioning_ResultFields_AreDocumented()
    {
        // Expected fields returned by JIT provisioning use case
        string[] resultFields = new[]
        {
            "userId",
            "isNewUser",
            "email",
            "displayName"
        };

        resultFields.ShouldContain("userId");
        resultFields.ShouldContain("isNewUser");
    }
}
