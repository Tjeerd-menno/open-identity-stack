using System.Text.Json;

namespace OpenIdentityStack.Contract.Tests.Admin.Applications;

public sealed class ApplicationCredentialsContractTests
{
    [Fact]
    public void ApplicationCredentialResponse_ContainsMetadataOnly()
    {
        string responseJson = """
        {
          "id": "550e8400-e29b-41d4-a716-446655440001",
          "applicationId": "550e8400-e29b-41d4-a716-446655440000",
          "type": "ClientSecret",
          "thumbprint": null,
          "subject": null,
          "description": "Primary secret",
          "expiresAt": "2026-06-24T12:00:00Z",
          "createdAt": "2026-05-24T12:00:00Z",
          "lastUsedAt": null,
          "revokedAt": null
        }
        """;

        using var document = JsonDocument.Parse(responseJson);
        JsonElement root = document.RootElement;

        root.GetProperty("type").GetString().ShouldBe("ClientSecret");
        root.GetProperty("description").GetString().ShouldBe("Primary secret");
        root.TryGetProperty("clientSecret", out _).ShouldBeFalse();
        root.TryGetProperty("secretHash", out _).ShouldBeFalse();
    }

    [Fact]
    public void AddApplicationSecretResponse_ReturnsOneTimeSecretOnly()
    {
        string responseJson = """
        {
          "credentialId": "550e8400-e29b-41d4-a716-446655440001",
          "clientSecret": "one-time-secret"
        }
        """;

        using var document = JsonDocument.Parse(responseJson);
        JsonElement root = document.RootElement;

        root.GetProperty("credentialId").GetString().ShouldNotBeNullOrWhiteSpace();
        root.GetProperty("clientSecret").GetString().ShouldBe("one-time-secret");
        root.TryGetProperty("secretHash", out _).ShouldBeFalse();
    }
}
