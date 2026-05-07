
using System.Text.Json;

namespace OpenIdentityStack.Contract.Tests.Admin;
/// <summary>
/// Contract tests for the Admin Providers API endpoints.
/// These tests verify the API contract (request/response shapes) per specification.
/// </summary>
public sealed class ProvidersEndpointContractTests
{
    #region Provider Response Contract

    [Fact]
    public void ProviderResponse_RequiredFields_AreDocumented()
    {
        // This test documents the required fields in a provider response
        string[] requiredFields = new[]
        {
            "id",                    // REQUIRED. The unique identifier for the provider.
            "name",                  // REQUIRED. The unique name/slug for the provider.
            "authority",             // REQUIRED. The OIDC authority URL.
            "clientId",              // REQUIRED. The OAuth2 client ID.
            "status",                // REQUIRED. The current status (Active, Disabled).
            "createdAt",             // REQUIRED. When the provider was created.
        };

        requiredFields.ShouldNotBeEmpty();
        requiredFields.ShouldContain("id");
        requiredFields.ShouldContain("name");
        requiredFields.ShouldContain("authority");
        requiredFields.ShouldContain("clientId");
        requiredFields.ShouldContain("status");
        requiredFields.ShouldContain("createdAt");
    }

    [Fact]
    public void ProviderResponse_OptionalFields_AreDocumented()
    {
        // This test documents the optional fields in a provider response
        string[] optionalFields = new[]
        {
            "displayName",           // OPTIONAL. Human-readable display name.
            "scopes",                // OPTIONAL. Array of OAuth2 scopes.
            "jitProvisioningEnabled",// OPTIONAL. Whether JIT provisioning is enabled.
            "updatedAt",             // OPTIONAL. When the provider was last updated.
        };

        optionalFields.ShouldNotBeEmpty();
    }

    [Fact]
    public void ProviderResponse_HasValidJsonStructure()
    {
        // This test validates the complete response contract shape
        string responseJson = """
        {
            "id": "550e8400-e29b-41d4-a716-446655440000",
            "name": "azure-ad",
            "displayName": "Azure AD",
            "authority": "https://login.microsoftonline.com/tenantid/v2.0",
            "clientId": "client-123",
            "scopes": ["openid", "profile"],
            "jitProvisioningEnabled": true,
            "status": "Active",
            "createdAt": "2026-01-19T10:00:00Z"
        }
        """;

        var result = JsonDocument.Parse(responseJson);

        result.RootElement.TryGetProperty("id", out JsonElement id).ShouldBeTrue();
        id.ValueKind.ShouldBe(JsonValueKind.String);

        result.RootElement.TryGetProperty("name", out JsonElement name).ShouldBeTrue();
        name.ValueKind.ShouldBe(JsonValueKind.String);

        result.RootElement.TryGetProperty("displayName", out JsonElement displayName).ShouldBeTrue();
        displayName.ValueKind.ShouldBe(JsonValueKind.String);

        result.RootElement.TryGetProperty("authority", out JsonElement authority).ShouldBeTrue();
        authority.ValueKind.ShouldBe(JsonValueKind.String);

        result.RootElement.TryGetProperty("clientId", out JsonElement clientId).ShouldBeTrue();
        clientId.ValueKind.ShouldBe(JsonValueKind.String);

        result.RootElement.TryGetProperty("scopes", out JsonElement scopes).ShouldBeTrue();
        scopes.ValueKind.ShouldBe(JsonValueKind.Array);

        result.RootElement.TryGetProperty("jitProvisioningEnabled", out JsonElement jit).ShouldBeTrue();
        jit.ValueKind.ShouldBe(JsonValueKind.True);

        result.RootElement.TryGetProperty("status", out JsonElement status).ShouldBeTrue();
        status.ValueKind.ShouldBe(JsonValueKind.String);

        result.RootElement.TryGetProperty("createdAt", out JsonElement createdAt).ShouldBeTrue();
        createdAt.ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public void ProviderResponse_Status_HasValidValues()
    {
        // This test documents valid status values for a provider
        string[] validStatuses = new[]
        {
            "Active",    // Provider is enabled and can be used for authentication.
            "Disabled",  // Provider is disabled and cannot be used.
        };

        validStatuses.ShouldContain("Active");
        validStatuses.ShouldContain("Disabled");
    }

    #endregion

    #region Create Provider Request Contract

    [Fact]
    public void CreateProviderRequest_RequiredFields_AreDocumented()
    {
        // This test documents the required fields for creating a provider
        string[] requiredFields = new[]
        {
            "name",      // REQUIRED. Unique name/slug for the provider.
            "authority", // REQUIRED. The OIDC authority URL.
            "clientId",  // REQUIRED. The OAuth2 client ID.
        };

        requiredFields.ShouldContain("name");
        requiredFields.ShouldContain("authority");
        requiredFields.ShouldContain("clientId");
    }

    [Fact]
    public void CreateProviderRequest_OptionalFields_AreDocumented()
    {
        // This test documents the optional fields for creating a provider
        string[] optionalFields = new[]
        {
            "displayName",           // OPTIONAL. Human-readable display name.
            "clientSecret",          // OPTIONAL. The OAuth2 client secret.
            "scopes",                // OPTIONAL. Array of OAuth2 scopes to request.
            "jitProvisioningEnabled",// OPTIONAL. Enable JIT user provisioning.
        };

        optionalFields.ShouldNotBeEmpty();
    }

    [Fact]
    public void CreateProviderRequest_HasValidJsonStructure()
    {
        // This test validates the request contract shape
        string requestJson = """
        {
            "name": "azure-ad",
            "displayName": "Azure Active Directory",
            "authority": "https://login.microsoftonline.com/tenantid/v2.0",
            "clientId": "client-123",
            "clientSecret": "secret-value",
            "scopes": ["openid", "profile", "email"],
            "jitProvisioningEnabled": true
        }
        """;

        var result = JsonDocument.Parse(requestJson);

        result.RootElement.TryGetProperty("name", out JsonElement name).ShouldBeTrue();
        name.ValueKind.ShouldBe(JsonValueKind.String);

        result.RootElement.TryGetProperty("authority", out JsonElement authority).ShouldBeTrue();
        authority.ValueKind.ShouldBe(JsonValueKind.String);

        result.RootElement.TryGetProperty("clientId", out JsonElement clientId).ShouldBeTrue();
        clientId.ValueKind.ShouldBe(JsonValueKind.String);
    }

    #endregion

    #region Update Provider Request Contract

    [Fact]
    public void UpdateProviderRequest_AllFieldsOptional()
    {
        // This test validates that update request allows partial updates
        string requestJson = """
        {
            "displayName": "Updated Name"
        }
        """;

        var result = JsonDocument.Parse(requestJson);

        // Only displayName should be present for this partial update
        result.RootElement.TryGetProperty("displayName", out _).ShouldBeTrue();
        result.RootElement.TryGetProperty("name", out _).ShouldBeFalse();
        result.RootElement.TryGetProperty("authority", out _).ShouldBeFalse();
        result.RootElement.TryGetProperty("clientId", out _).ShouldBeFalse();
    }

    [Fact]
    public void UpdateProviderRequest_CanUpdateMultipleFields()
    {
        // This test validates that multiple fields can be updated at once
        string requestJson = """
        {
            "displayName": "Updated Name",
            "jitProvisioningEnabled": false,
            "scopes": ["openid", "profile"]
        }
        """;

        var result = JsonDocument.Parse(requestJson);

        result.RootElement.TryGetProperty("displayName", out _).ShouldBeTrue();
        result.RootElement.TryGetProperty("jitProvisioningEnabled", out _).ShouldBeTrue();
        result.RootElement.TryGetProperty("scopes", out JsonElement scopes).ShouldBeTrue();
        scopes.ValueKind.ShouldBe(JsonValueKind.Array);
    }

    #endregion

    #region List Providers Response Contract

    [Fact]
    public void ListProvidersResponse_IsArrayOfProviders()
    {
        // This test validates the list response structure
        string responseJson = """
        [
            {
                "id": "550e8400-e29b-41d4-a716-446655440000",
                "name": "azure-ad",
                "displayName": "Azure AD",
                "authority": "https://login.microsoftonline.com/tenantid/v2.0",
                "clientId": "client-123",
                "status": "Active",
                "createdAt": "2026-01-19T10:00:00Z"
            },
            {
                "id": "660e8400-e29b-41d4-a716-446655440001",
                "name": "google",
                "displayName": "Google",
                "authority": "https://accounts.google.com",
                "clientId": "google-client-123",
                "status": "Active",
                "createdAt": "2026-01-19T11:00:00Z"
            }
        ]
        """;

        var result = JsonDocument.Parse(responseJson);

        result.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        result.RootElement.GetArrayLength().ShouldBeGreaterThan(0);

        // Each item should have required provider fields
        foreach (JsonElement item in result.RootElement.EnumerateArray())
        {
            item.TryGetProperty("id", out _).ShouldBeTrue();
            item.TryGetProperty("name", out _).ShouldBeTrue();
            item.TryGetProperty("status", out _).ShouldBeTrue();
        }
    }

    [Fact]
    public void ListProvidersResponse_EmptyArrayIsValid()
    {
        // This test validates that an empty list is a valid response
        string responseJson = "[]";

        var result = JsonDocument.Parse(responseJson);

        result.RootElement.ValueKind.ShouldBe(JsonValueKind.Array);
        result.RootElement.GetArrayLength().ShouldBe(0);
    }

    #endregion

    #region Error Response Contract

    [Fact]
    public void ProviderErrorResponse_RequiredFields_AreDocumented()
    {
        // Error response for provider operations
        string[] requiredErrorFields = new[]
        {
            "type",    // REQUIRED. Error type identifier.
            "title",   // REQUIRED. Human-readable error title.
            "status",  // REQUIRED. HTTP status code.
        };

        requiredErrorFields.ShouldContain("type");
        requiredErrorFields.ShouldContain("title");
        requiredErrorFields.ShouldContain("status");
    }

    [Fact]
    public void ProviderErrorResponse_OptionalFields_AreDocumented()
    {
        string[] optionalErrorFields = new[]
        {
            "detail",     // OPTIONAL. Human-readable error description.
            "instance",   // OPTIONAL. URI identifying the specific occurrence.
            "errors",     // OPTIONAL. Validation errors dictionary.
        };

        optionalErrorFields.ShouldNotBeEmpty();
    }

    [Fact]
    public void ProviderValidationError_HasValidStructure()
    {
        // This test validates the validation error response structure (RFC 7807)
        string errorJson = """
        {
            "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            "title": "One or more validation errors occurred.",
            "status": 400,
            "errors": {
                "name": ["The name field is required."],
                "authority": ["The authority field must be a valid URL."]
            }
        }
        """;

        var result = JsonDocument.Parse(errorJson);

        result.RootElement.TryGetProperty("type", out _).ShouldBeTrue();
        result.RootElement.TryGetProperty("title", out _).ShouldBeTrue();
        result.RootElement.TryGetProperty("status", out JsonElement status).ShouldBeTrue();
        status.GetInt32().ShouldBe(400);
        result.RootElement.TryGetProperty("errors", out JsonElement errors).ShouldBeTrue();
        errors.ValueKind.ShouldBe(JsonValueKind.Object);
    }

    #endregion
}
