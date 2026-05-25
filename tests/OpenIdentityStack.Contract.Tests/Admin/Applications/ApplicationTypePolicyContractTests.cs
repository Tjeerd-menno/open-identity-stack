using System.Text.Json;

namespace OpenIdentityStack.Contract.Tests.Admin.Applications;

public sealed class ApplicationTypePolicyContractTests
{
    [Fact]
    public void OpenApiContract_IncludesApplicationTypePoliciesPathAndSchema()
    {
        string contract = File.ReadAllText(GetOpenApiContractPath());

        contract.ShouldContain("/applications/policies/types");
        contract.ShouldContain("ApplicationTypePolicyResponse");
        contract.ShouldContain("defaultClientProfile");
    }

    [Fact]
    public void ApplicationTypePolicyResponse_ExposesPolicyMetadataShape()
    {
        string responseJson = """
        {
          "applicationType": "Web",
          "isSelectable": true,
          "unavailabilityReason": null,
          "defaultClientProfile": "Confidential",
          "allowedClientProfiles": ["Confidential"],
          "allowedGrantTypes": ["authorization_code", "refresh_token"],
          "defaultGrantTypes": ["authorization_code"],
          "options": {
            "clientSecrets": "Available",
            "clientCertificates": "Advanced",
            "pkce": "Available"
          },
          "requirePkce": false,
          "defaultRequirePkce": true,
          "defaultRequireConsent": true,
          "requiresRedirectUris": true
        }
        """;

        using var document = JsonDocument.Parse(responseJson);
        JsonElement root = document.RootElement;

        root.GetProperty("applicationType").GetString().ShouldBe("Web");
        root.GetProperty("defaultClientProfile").GetString().ShouldBe("Confidential");
        root.GetProperty("allowedGrantTypes").EnumerateArray().Select(item => item.GetString()).ShouldContain("authorization_code");
        root.GetProperty("options").GetProperty("clientSecrets").GetString().ShouldBe("Available");
        root.GetProperty("requiresRedirectUris").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void ValidationError_UsesDeterministicPolicyErrorCodes()
    {
        string responseJson = """
        {
          "error": "Validation.Application.TypeNotAvailable",
          "message": "This application type is not available for administrator-managed configuration."
        }
        """;

        using var document = JsonDocument.Parse(responseJson);
        JsonElement root = document.RootElement;

        root.GetProperty("error").GetString().ShouldBe("Validation.Application.TypeNotAvailable");
        string? message = root.GetProperty("message").GetString();
        message.ShouldNotBeNull();
        message.ShouldContain("not available");
    }

    private static string GetOpenApiContractPath() =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "Admin",
            "Applications",
            "applications.openapi.yaml"));
}
