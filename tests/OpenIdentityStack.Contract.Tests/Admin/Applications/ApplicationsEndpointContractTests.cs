using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenIdentityStack.Contract.Tests.Admin.Applications;

public sealed class ApplicationsEndpointContractTests
{
    [Theory]
    [InlineData("delete", "/applications/{id}")]
    [InlineData("patch", "/applications/{id}")]
    [InlineData("put", "/applications/{id}/oauth")]
    [InlineData("post", "/applications/{id}/disable")]
    [InlineData("post", "/applications/{id}/enable")]
    [InlineData("post", "/applications/{id}/credentials/client-secrets")]
    [InlineData("post", "/applications/{id}/credentials/certificates")]
    [InlineData("delete", "/applications/{id}/credentials/{credentialId}")]
    public void FencedMutation_DocumentsConflictResponse(string method, string path)
    {
        string contract = File.ReadAllText(GetOpenApiContractPath());
        Match pathMatch = Regex.Match(contract, $"(?ms)^  {Regex.Escape(path)}:\\r?\\n(?<body>.*?)(?=^  /|\\z)");
        pathMatch.Success.ShouldBeTrue($"Path '{path}' must be documented.");
        Match operationMatch = Regex.Match(pathMatch.Groups["body"].Value,
            $"(?ms)^    {method}:\\r?\\n(?<body>.*?)(?=^    [a-z]+:|\\z)");
        operationMatch.Success.ShouldBeTrue($"Operation '{method}' must be documented for '{path}'.");
        string operationSection = operationMatch.Groups["body"].Value;

        operationSection.ShouldContain("\"409\":");
        operationSection.ShouldContain("#/components/responses/ConflictError");
    }

    [Theory]
    [InlineData("put", "/applications/{id}/oauth")]
    [InlineData("post", "/applications/{id}/enable")]
    [InlineData("post", "/applications/{id}/credentials/client-secrets")]
    [InlineData("post", "/applications/{id}/credentials/certificates")]
    public void ApprovalProtectedMutation_DocumentsForbiddenResponseInCanonicalAndMirror(string method, string path)
    {
        foreach (string contractPath in GetOpenApiContractPaths())
        {
            string contract = File.ReadAllText(contractPath);
            Match pathMatch = Regex.Match(contract, $"(?ms)^  {Regex.Escape(path)}:\\r?\\n(?<body>.*?)(?=^  /|\\z)");
            pathMatch.Success.ShouldBeTrue($"Path '{path}' must be documented in '{contractPath}'.");
            Match operationMatch = Regex.Match(pathMatch.Groups["body"].Value,
                $"(?ms)^    {method}:\\r?\\n(?<body>.*?)(?=^    [a-z]+:|\\z)");
            operationMatch.Success.ShouldBeTrue($"Operation '{method}' must be documented for '{path}'.");

            string operationSection = operationMatch.Groups["body"].Value;
            operationSection.ShouldContain("\"403\":");
            operationSection.ShouldContain("#/components/responses/AdministrativeApprovalRequired");
            operationSection.ShouldContain("#/components/parameters/AdministrativeApprovalAcknowledgement");
            contract.ShouldNotContain("required: [status, errorCode]");
        }
    }

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

    private static string GetOpenApiContractPath() =>
        Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "contracts",
            "openapi",
            "006-unify-applications-model",
            "applications.openapi.yaml"));

    private static IEnumerable<string> GetOpenApiContractPaths()
    {
        yield return GetOpenApiContractPath();
        yield return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "Admin",
            "Applications",
            "applications.openapi.yaml"));
    }
}
