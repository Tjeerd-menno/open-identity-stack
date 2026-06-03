using System.Text.Json;

namespace OpenIdentityStack.Contract.Tests.Admin.Audit;

public sealed class AuditEntriesEndpointContractTests
{
    [Fact]
    public void ListAuditEntriesResponse_IsPaginatedAndIncludesStateFields()
    {
        string responseJson = """
        {
          "items": [
            {
              "id": "550e8400-e29b-41d4-a716-446655440000",
              "timestamp": "2026-06-01T12:00:00Z",
              "userId": "admin-user",
              "action": "Application.Created",
              "entityType": "Application",
              "entityId": "orders-web",
              "details": "Created application",
              "beforeState": null,
              "afterState": "{\"status\":\"Active\"}"
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
        JsonElement item = items.EnumerateArray().Single();
        item.GetProperty("id").ValueKind.ShouldBe(JsonValueKind.String);
        item.GetProperty("timestamp").ValueKind.ShouldBe(JsonValueKind.String);
        item.GetProperty("userId").GetString().ShouldBe("admin-user");
        item.GetProperty("action").GetString().ShouldBe("Application.Created");
        item.GetProperty("entityType").GetString().ShouldBe("Application");
        item.GetProperty("entityId").GetString().ShouldBe("orders-web");
        item.GetProperty("details").GetString().ShouldBe("Created application");
        item.GetProperty("beforeState").ValueKind.ShouldBe(JsonValueKind.Null);
        item.GetProperty("afterState").GetString().ShouldBe("{\"status\":\"Active\"}");
        root.GetProperty("page").GetInt32().ShouldBe(1);
        root.GetProperty("pageSize").GetInt32().ShouldBe(20);
        root.GetProperty("totalCount").GetInt32().ShouldBe(1);
        root.GetProperty("totalPages").GetInt32().ShouldBe(1);
        root.GetProperty("hasPreviousPage").ValueKind.ShouldBe(JsonValueKind.False);
        root.GetProperty("hasNextPage").ValueKind.ShouldBe(JsonValueKind.False);
    }

    [Fact]
    public void ListAuditEntriesQueryParameters_AreDocumented()
    {
        string[] queryParameters =
        [
            "page",
            "pageSize",
            "from",
            "to",
            "userId",
            "action",
            "entityType",
            "entityId",
            "search"
        ];

        queryParameters.ShouldContain("page");
        queryParameters.ShouldContain("pageSize");
        queryParameters.ShouldContain("search");
        queryParameters.ShouldContain("entityType");
    }
}
