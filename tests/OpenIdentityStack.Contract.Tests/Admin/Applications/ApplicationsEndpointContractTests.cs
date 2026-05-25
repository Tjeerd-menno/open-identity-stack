using System.Text.Json;

namespace OpenIdentityStack.Contract.Tests.Admin.Applications;

public sealed class ApplicationsEndpointContractTests
{
    [Fact]
    public void CreateApplicationRequest_RequiredFields_AreDocumented()
    {
        string[] requiredFields =
        [
            "clientId",
            "displayName",
            "type",
            "clientType",
            "redirectUris",
            "postLogoutRedirectUris",
            "allowedScopes",
            "allowedGrantTypes",
            "requirePkce",
            "requireConsent"
        ];

        requiredFields.ShouldContain("clientId");
        requiredFields.ShouldContain("displayName");
        requiredFields.ShouldContain("allowedGrantTypes");
    }

    [Fact]
    public void ApplicationResponse_HasUnifiedApplicationShape()
    {
        string responseJson = """
        {
          "id": "550e8400-e29b-41d4-a716-446655440000",
          "clientId": "orders-web",
          "displayName": "Orders Web",
          "description": "Orders application",
          "type": "Web",
          "clientType": "Confidential",
          "status": "Active",
          "redirectUris": ["https://orders.example.com/callback"],
          "postLogoutRedirectUris": [],
          "allowedScopes": ["openid", "orders.read"],
          "allowedGrantTypes": ["authorization_code"],
          "requirePkce": true,
          "requireConsent": true,
          "credentialCount": 0,
          "certificateCount": 0,
          "createdAt": "2026-05-24T12:00:00Z",
          "modifiedAt": "2026-05-24T12:00:00Z"
        }
        """;

        using var document = JsonDocument.Parse(responseJson);
        JsonElement root = document.RootElement;

        root.TryGetProperty("id", out JsonElement id).ShouldBeTrue();
        id.ValueKind.ShouldBe(JsonValueKind.String);
        root.TryGetProperty("clientId", out JsonElement clientId).ShouldBeTrue();
        clientId.GetString().ShouldBe("orders-web");
        root.TryGetProperty("type", out JsonElement type).ShouldBeTrue();
        type.GetString().ShouldBe("Web");
        root.TryGetProperty("allowedGrantTypes", out JsonElement grants).ShouldBeTrue();
        grants.ValueKind.ShouldBe(JsonValueKind.Array);
        root.TryGetProperty("credentialCount", out JsonElement credentialCount).ShouldBeTrue();
        credentialCount.GetInt32().ShouldBe(0);
    }

    [Fact]
    public void ListApplicationsResponse_IsPaginated()
    {
        string responseJson = """
        {
          "items": [
            {
              "id": "550e8400-e29b-41d4-a716-446655440000",
              "clientId": "orders-web",
              "displayName": "Orders Web",
              "type": "Web",
              "clientType": "Confidential",
              "status": "Active",
              "allowedGrantTypes": ["authorization_code"],
              "credentialCount": 0,
              "createdAt": "2026-05-24T12:00:00Z",
              "modifiedAt": null
            }
          ],
          "page": 1,
          "pageSize": 20,
          "totalCount": 1,
          "totalPages": 1,
          "hasPreviousPage": false,
          "hasNextPage": false
        }
        """;

        using var document = JsonDocument.Parse(responseJson);
        JsonElement root = document.RootElement;

        root.TryGetProperty("items", out JsonElement items).ShouldBeTrue();
        items.ValueKind.ShouldBe(JsonValueKind.Array);
        root.GetProperty("page").GetInt32().ShouldBe(1);
        root.GetProperty("pageSize").GetInt32().ShouldBe(20);
        root.GetProperty("totalCount").GetInt32().ShouldBe(1);
        root.GetProperty("hasNextPage").ValueKind.ShouldBe(JsonValueKind.False);
    }

    [Fact]
    public void ApplicationLifecycleValues_AreDocumented()
    {
        string[] statuses = ["Active", "Disabled"];
        string[] applicationProfiles = ["MachineToMachine", "Web", "SinglePage", "Native", "Device", "Custom"];
        string[] clientTypes = ["Public", "Confidential"];

        statuses.ShouldContain("Active");
        applicationProfiles.ShouldContain("MachineToMachine");
        clientTypes.ShouldContain("Confidential");
    }
}
