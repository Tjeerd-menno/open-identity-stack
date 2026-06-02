using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OpenIdentityStack.Api.Tests.Fixtures;
using OpenIdentityStack.Infrastructure.Audit;

namespace OpenIdentityStack.Api.Tests.Admin.Audit;

public sealed class AuditEntriesEndpointWorkflowTests(AppHostFixture fixture) : IAsyncLifetime
{
    private readonly AppHostFixture fixture = fixture;
    private HttpClient client = null!;
    private string? accessToken;

    public async ValueTask InitializeAsync()
    {
        this.client = this.fixture.HttpClient!;
        await this.AuthenticateAsync();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ListAuditEntries_Returns200WithPaginatedResponseAndStateFields()
    {
        Guid entryId = await this.SeedAuditEntryAsync(
            "admin-user",
            "Application.Created",
            "Application",
            "orders-web",
            new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
            "Created application",
            null,
            "{\"status\":\"Active\"}");

        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, "/api/admin/audit-entries?page=1&pageSize=10");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json.ShouldNotBeNull();
        json["page"]?.GetValue<int>().ShouldBe(1);
        json["pageSize"]?.GetValue<int>().ShouldBe(10);
        json["totalCount"]?.GetValue<int>().ShouldBeGreaterThanOrEqualTo(1);
        JsonNode? item = json["items"]?.AsArray().FirstOrDefault(i => i?["id"]?.GetValue<Guid>() == entryId);
        item.ShouldNotBeNull();
        item?["details"]?.GetValue<string>().ShouldBe("Created application");
        item?.AsObject().ContainsKey("beforeState").ShouldBeTrue();
        item?["afterState"]?.GetValue<string>().ShouldBe("{\"status\":\"Active\"}");
    }

    [Fact]
    public async Task ListAuditEntries_WithFiltersAndSearch_ReturnsMatchingEntries()
    {
        await this.SeedAuditEntryAsync(
            "admin-filter",
            "Application.Updated",
            "Application",
            "orders-web",
            new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
            "Changed redirect URL",
            "{\"redirect\":\"old\"}",
            "{\"redirect\":\"new\"}");
        await this.SeedAuditEntryAsync(
            "admin-filter",
            "Role.Updated",
            "Role",
            "admin",
            new DateTimeOffset(2026, 6, 1, 13, 0, 0, TimeSpan.Zero),
            "Changed role",
            null,
            null);

        const string url = "/api/admin/audit-entries?userId=admin-filter&action=Application.Updated&entityType=Application&entityId=orders-web&search=redirect&from=2026-06-01T00:00:00Z&to=2026-06-02T00:00:00Z";
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, url);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        json.ShouldNotBeNull();
        JsonArray? items = json["items"]?.AsArray();
        items.ShouldNotBeNull();
        items.All(i => i?["entityId"]?.GetValue<string>() == "orders-web").ShouldBeTrue();
    }

    [Fact]
    public async Task ListAuditEntries_WithoutAuth_Returns401Unauthorized()
    {
        HttpResponseMessage response = await this.client.GetAsync("/api/admin/audit-entries");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListAuditEntries_WithoutAuditReadPermission_Returns403Forbidden()
    {
        string clientId = $"audit-limited-{Guid.NewGuid():N}";
        const string clientSecret = "test-secret-123";
        await this.fixture.CreateServiceAccountAsync(clientId, clientSecret, ["limited"]);
        string token = await this.fixture.GetAccessTokenAsync(clientId, clientSecret, "limited");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/audit-entries");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await this.client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private async Task AuthenticateAsync()
    {
        string clientId = $"audit-contract-{Guid.NewGuid():N}";
        const string clientSecret = "test-secret-123";
        await this.fixture.CreateServiceAccountAsync(clientId, clientSecret);
        this.accessToken = await this.fixture.GetAccessTokenAsync(clientId, clientSecret);
    }

    private async Task<HttpResponseMessage> SendRequestAsync(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(this.accessToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", this.accessToken);
        }

        return await this.client.SendAsync(request);
    }

    private async Task<Guid> SeedAuditEntryAsync(
        string userId,
        string action,
        string entityType,
        string entityId,
        DateTimeOffset timestamp,
        string? details,
        string? beforeState,
        string? afterState)
    {
        var id = Guid.NewGuid();
        await this.fixture.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.AuditLogEntries.Add(new AuditLogEntry
            {
                Id = id,
                UserId = userId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Timestamp = timestamp,
                Details = details,
                BeforeState = beforeState,
                AfterState = afterState
            });
            await dbContext.SaveChangesAsync();
        });

        return id;
    }
}
