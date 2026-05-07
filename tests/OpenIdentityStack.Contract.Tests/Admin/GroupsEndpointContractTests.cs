using OpenIdentityStack.Contract.Tests.Fixtures;
using System.Net.Http.Json;
using System.Net;
using Aspire.Hosting.Testing;
using System.Text.Json.Nodes;

namespace OpenIdentityStack.Contract.Tests.Admin;
/// <summary>
/// Contract tests for the /api/admin/groups endpoint.
/// </summary>
public sealed class GroupsEndpointContractTests(AppHostFixture fixture) : IAsyncLifetime
{
    private readonly AppHostFixture _fixture = fixture;
    private HttpClient _client = null!;
    private string? _accessToken;

    private HttpClient Client => this._client;

    public async ValueTask InitializeAsync()
    {
        this._client = this._fixture.App!.CreateHttpClient("api");
        // Authenticate before running tests
        await this.AuthenticateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        this._client?.Dispose();
        await ValueTask.CompletedTask;
    }

    private async Task AuthenticateAsync()
    {
        string clientId = Guid.NewGuid().ToString();
        const string clientSecret = "test-secret-123";

        await this._fixture.CreateServiceAccountAsync(clientId, clientSecret);
        this._accessToken = await this._fixture.GetAccessTokenAsync(clientId, clientSecret);
    }

    private async Task<HttpResponseMessage> SendRequestAsync(HttpMethod method, string url, object? content = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(this._accessToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", this._accessToken);
        }
        
        if (content != null)
        {
             request.Content = JsonContent.Create(content);
        }

        return await this.Client.SendAsync(request);
    }

    [Fact]
    public async Task ListGroups_Returns200WithPaginatedResponse()
    {
        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, "/api/admin/groups");
        
        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? content = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        content.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateGroup_WithValidRequest_Returns201AndLocation()
    {
        // Arrange
        var command = new
        {
            Name = $"Test Group {Guid.NewGuid()}",
            Description = "A test group",
            Email = $"group-{Guid.NewGuid()}@example.com"
        };

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/groups", command);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();

        JsonNode? content = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        content.ShouldNotBeNull();
        content["id"].ShouldNotBeNull();
    }

    [Fact]
    public async Task GetGroup_ExistingId_Returns200AndGroup()
    {
        // Arrange - Create group
        var command = new
        {
            Name = $"Get Group {Guid.NewGuid()}",
            Description = "Get API test group",
            Email = $"get-group-{Guid.NewGuid()}@example.com"
        };
        HttpResponseMessage createResponse = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/groups", command);
        createResponse.EnsureSuccessStatusCode();
        JsonNode? createdGroup = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        string id = createdGroup?["id"]?.GetValue<string>() ?? throw new InvalidOperationException("ID not returned");

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Get, $"/api/admin/groups/{id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? content = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        content.ShouldNotBeNull();
        content["id"]?.GetValue<string>().ShouldBe(id);
        content["name"]?.GetValue<string>().ShouldBe(command.Name);
    }

    [Fact]
    public async Task UpdateGroup_ExistingId_Returns200AndUpdatedGroup()
    {
        // Arrange - Create group
        var command = new
        {
            Name = $"Update Group {Guid.NewGuid()}",
            Description = "Update API test group",
            Email = $"update-group-{Guid.NewGuid()}@example.com"
        };
        HttpResponseMessage createResponse = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/groups", command);
        createResponse.EnsureSuccessStatusCode();
        JsonNode? createdGroup = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        string id = createdGroup?["id"]?.GetValue<string>() ?? throw new InvalidOperationException("ID not returned");

        var updateCommand = new
        {
            Name = $"Updated Group {Guid.NewGuid()}", // Changed Name
            Description = "Updated description" // Changed Description
        };

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Patch, $"/api/admin/groups/{id}", updateCommand);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode? content = await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        content.ShouldNotBeNull();
        content["name"]?.GetValue<string>().ShouldBe(updateCommand.Name);
    }

    [Fact]
    public async Task DeleteGroup_ExistingId_Returns204()
    {
        // Arrange
        var command = new
        {
            Name = $"Delete Group {Guid.NewGuid()}",
            Description = "Delete API test group",
            Email = $"delete-group-{Guid.NewGuid()}@example.com"
        };
        HttpResponseMessage createResponse = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/groups", command);
        createResponse.EnsureSuccessStatusCode();
        JsonNode? createdGroup = await createResponse.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        string id = createdGroup?["id"]?.GetValue<string>() ?? throw new InvalidOperationException("ID not returned");

        // Act
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Delete, $"/api/admin/groups/{id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify it's gone
        HttpResponseMessage getResponse = await this.SendRequestAsync(HttpMethod.Get, $"/api/admin/groups/{id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddGroupMember_Returns201()
    {
        // Arrange - Create group
        var groupRequest = new
        {
            Name = $"Member Group {Guid.NewGuid()}",
            Description = "Group member test",
            Email = $"member-group-{Guid.NewGuid()}@example.com"
        };
        HttpResponseMessage createGroupResponse = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/groups", groupRequest);
        createGroupResponse.EnsureSuccessStatusCode();
        JsonNode? createdGroup = await createGroupResponse.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        string groupId = createdGroup?["id"]?.GetValue<string>() ?? throw new InvalidOperationException("Group ID not returned");

        // Arrange - Create user
        var userRequest = new
        {
            Email = $"group-member-{Guid.NewGuid():N}@example.com",
            DisplayName = "Group Member",
            Password = "TestPassword123!"
        };
        HttpResponseMessage createUserResponse = await this.SendRequestAsync(HttpMethod.Post, "/api/admin/users", userRequest);
        createUserResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        JsonNode? createdUser = await createUserResponse.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonNode>();
        Guid userId = createdUser?["id"]?.GetValue<Guid>() ?? throw new InvalidOperationException("User ID not returned");

        // Act - userId is part of the URL path, not the request body
        HttpResponseMessage response = await this.SendRequestAsync(HttpMethod.Post, $"/api/admin/groups/{groupId}/members/{userId}", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
