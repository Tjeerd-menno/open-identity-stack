using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using OpenIdentityStack.Api.Tests.Fixtures;
using OpenIdentityStack.Application.Authorization;
using OpenIdentityStack.Domain.Common;
using OpenIdentityStack.Domain.Roles;
using OpenIdentityStack.Infrastructure.Persistence;
using SharedKernel;

namespace OpenIdentityStack.Api.Tests.Admin.ApplicationPermissions;

public sealed class ApplicationPermissionsApiTests(AppHostFixture fixture) : IAsyncLifetime
{
    private readonly AppHostFixture fixture = fixture;
    private HttpClient client = null!;
    private string? accessToken;
    private Guid actorId;

    public async ValueTask InitializeAsync()
    {
        this.client = this.fixture.HttpClient!;
        this.actorId = Guid.NewGuid();
        string clientId = this.actorId.ToString();
        const string clientSecret = "test-secret-123";

        await this.SeedRegistryAdminRoleAsync(this.actorId);
        await this.fixture.CreateServiceAccountAsync(clientId, clientSecret);
        this.accessToken = await this.fixture.GetAccessTokenAsync(clientId, clientSecret);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task ManifestPreviewAndApply_WithOmittedPermission_ReturnsDestructiveResult()
    {
        string applicationIdentifier = $"orders-{Guid.NewGuid():N}"[..20];
        Guid applicationId = await this.CreateApplicationAsync(applicationIdentifier);

        object updateRequest = CreateManifestRequest(
            applicationIdentifier,
            "2.0.0",
            [("order:read", "Read orders")]);

        HttpResponseMessage previewResponse = await this.SendRequestAsync(
            HttpMethod.Post,
            $"/api/admin/application-permissions/applications/{applicationId}/manifest/preview",
            updateRequest);

        previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode previewJson = await ReadJsonAsync(previewResponse);
        previewJson["isDestructive"]!.GetValue<bool>().ShouldBeTrue();
        previewJson["removals"]!.AsArray().Count.ShouldBe(1);
        previewJson["removals"]![0]!["fullPermissionKey"]!.GetValue<string>().ShouldBe($"{applicationIdentifier}:order:cancel");

        HttpResponseMessage applyResponse = await this.SendRequestAsync(
            HttpMethod.Post,
            $"/api/admin/application-permissions/applications/{applicationId}/manifest",
            updateRequest);

        applyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode applyJson = await ReadJsonAsync(applyResponse);
        applyJson["application"]!["manifestVersion"]!.GetValue<string>().ShouldBe("2.0.0");
        applyJson["result"]!["removedPermissions"]!.AsArray().Count.ShouldBe(1);
        applyJson["result"]!["manifestVersionAdvanced"]!.GetValue<bool>().ShouldBeTrue();
    }

    [Fact]
    public async Task PermissionDeletionImpactAndApply_ReturnsDestructiveResult()
    {
        string applicationIdentifier = $"orders-{Guid.NewGuid():N}"[..20];
        Guid applicationId = await this.CreateApplicationAsync(applicationIdentifier);
        JsonNode detailJson = await this.GetApplicationAsync(applicationId);
        Guid permissionId = detailJson["permissions"]!.AsArray()
            .Single(permission => permission!["permissionKey"]!.GetValue<string>() == "order:cancel")!["id"]!
            .GetValue<Guid>();

        HttpResponseMessage impactResponse = await this.SendRequestAsync(
            HttpMethod.Get,
            $"/api/admin/application-permissions/permissions/{permissionId}/deletion-impact");

        impactResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode impactJson = await ReadJsonAsync(impactResponse);
        impactJson["targetId"]!.GetValue<Guid>().ShouldBe(permissionId);
        impactJson["targetType"]!.GetValue<string>().ShouldBe("permission");

        HttpResponseMessage deleteResponse = await this.SendRequestAsync(
            HttpMethod.Delete,
            $"/api/admin/application-permissions/permissions/{permissionId}",
            new { Reason = "No longer supported.", ConcurrencyToken = (uint?)null });

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode deleteJson = await ReadJsonAsync(deleteResponse);
        deleteJson["removedPermissions"]!.AsArray().Count.ShouldBe(1);
        deleteJson["removedPermissions"]![0]!["fullPermissionKey"]!.GetValue<string>().ShouldBe($"{applicationIdentifier}:order:cancel");
    }

    [Fact]
    public async Task HistoryAndReplacementGuidance_ForRemovedPermission_ReturnsTombstoneDetails()
    {
        string applicationIdentifier = $"orders-{Guid.NewGuid():N}"[..20];
        Guid applicationId = await this.CreateApplicationAsync(applicationIdentifier);
        JsonNode detailJson = await this.GetApplicationAsync(applicationId);
        Guid permissionId = detailJson["permissions"]!.AsArray()
            .Single(permission => permission!["permissionKey"]!.GetValue<string>() == "order:cancel")!["id"]!
            .GetValue<Guid>();

        HttpResponseMessage deleteResponse = await this.SendRequestAsync(
            HttpMethod.Delete,
            $"/api/admin/application-permissions/permissions/{permissionId}",
            new { Reason = "No longer supported.", ConcurrencyToken = (uint?)null });
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        HttpResponseMessage replacementResponse = await this.SendRequestAsync(
            HttpMethod.Patch,
            $"/api/admin/application-permissions/permissions/{permissionId}/replacement",
            new
            {
                ReplacementFullPermissionKey = "users:read",
                ReplacementNote = "Use the platform user read permission.",
                ConcurrencyToken = (uint?)null,
            });

        replacementResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode replacementJson = await ReadJsonAsync(replacementResponse);
        replacementJson["fullPermissionKey"]!.GetValue<string>().ShouldBe($"{applicationIdentifier}:order:cancel");
        replacementJson["replacementFullPermissionKey"]!.GetValue<string>().ShouldBe("users:read");

        HttpResponseMessage historyResponse = await this.SendRequestAsync(
            HttpMethod.Get,
            $"/api/admin/application-permissions/history?applicationIdentifier={applicationIdentifier}");

        historyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode historyJson = await ReadJsonAsync(historyResponse);
        JsonNode removedPermission = historyJson["removedPermissions"]!.AsArray().Single()!;
        removedPermission["fullPermissionKey"]!.GetValue<string>().ShouldBe($"{applicationIdentifier}:order:cancel");
        removedPermission["replacementFullPermissionKey"]!.GetValue<string>().ShouldBe("users:read");
    }

    [Fact]
    public async Task Diagnostics_WithMalformedRolePermission_ReturnsIntegrityIssue()
    {
        await this.fixture.ExecuteDbContextAsync(async dbContext =>
        {
            Role role = Role.Create($"diagnostic-role-{Guid.NewGuid():N}"[..30], "Diagnostic Role").Value;
            role.SetPermissions(["not-valid"]);
            dbContext.Roles.Add(role);
            await dbContext.SaveChangesAsync();
        });

        HttpResponseMessage response = await this.SendRequestAsync(
            HttpMethod.Get,
            "/api/admin/application-permissions/diagnostics");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode json = await ReadJsonAsync(response);
        JsonNode issue = json["issues"]!.AsArray().Single(item => item!["permission"]!.GetValue<string>() == "not-valid")!;
        issue["kind"]!.GetValue<string>().ShouldBe("malformed");
        issue["assignmentStore"]!.GetValue<string>().ShouldBe("role");
    }

    [Fact]
    public async Task ApplicationDeletionImpactAndApply_ReturnsDestructiveResult()
    {
        string applicationIdentifier = $"orders-{Guid.NewGuid():N}"[..20];
        Guid applicationId = await this.CreateApplicationAsync(applicationIdentifier);

        HttpResponseMessage impactResponse = await this.SendRequestAsync(
            HttpMethod.Get,
            $"/api/admin/application-permissions/applications/{applicationId}/deletion-impact");

        impactResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode impactJson = await ReadJsonAsync(impactResponse);
        impactJson["targetId"]!.GetValue<Guid>().ShouldBe(applicationId);
        impactJson["targetType"]!.GetValue<string>().ShouldBe("application");

        HttpResponseMessage deleteResponse = await this.SendRequestAsync(
            HttpMethod.Delete,
            $"/api/admin/application-permissions/applications/{applicationId}",
            new { Reason = "Application retired.", ConcurrencyToken = (uint?)null });

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode deleteJson = await ReadJsonAsync(deleteResponse);
        deleteJson["removedPermissions"]!.AsArray().Count.ShouldBe(2);

        HttpResponseMessage getResponse = await this.SendRequestAsync(
            HttpMethod.Get,
            $"/api/admin/application-permissions/applications/{applicationId}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode getJson = await ReadJsonAsync(getResponse);
        getJson["permissions"]!.AsArray().Count.ShouldBe(0);
    }

    [Fact]
    public async Task RemoteManifestPreview_WithoutTrustedBaseUrl_ReturnsBadRequest()
    {
        string applicationIdentifier = $"orders-{Guid.NewGuid():N}"[..20];
        Guid applicationId = await this.CreateApplicationAsync(applicationIdentifier);

        HttpResponseMessage response = await this.SendRequestAsync(
            HttpMethod.Post,
            $"/api/admin/application-permissions/applications/{applicationId}/import/preview",
            new { ConcurrencyToken = (uint?)null });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        JsonNode json = await ReadJsonAsync(response);
        json["error"]!.GetValue<string>().ShouldBe("Validation.PermissionManifest.ManifestBaseUrlRequired");
    }

    [Fact]
    public async Task RemoteManifestPreviewAndApply_WithTrustedLocalFixture_ReturnsManifestResults()
    {
        string applicationIdentifier = $"orders-{Guid.NewGuid():N}"[..20];
        await using RemoteManifestFixtureServer server = await RemoteManifestFixtureServer.StartAsync(
            CreateManifestJson(
                applicationIdentifier,
                "2.0.0",
                [("order:read", "Read orders")]));
        Guid applicationId = await this.CreateApplicationAsync(applicationIdentifier, server.BaseUrl);

        HttpResponseMessage previewResponse = await this.SendRequestAsync(
            HttpMethod.Post,
            $"/api/admin/application-permissions/applications/{applicationId}/import/preview",
            new { ConcurrencyToken = (uint?)null });

        previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode previewJson = await ReadJsonAsync(previewResponse);
        previewJson["requestedManifestVersion"]!.GetValue<string>().ShouldBe("2.0.0");
        previewJson["isDestructive"]!.GetValue<bool>().ShouldBeTrue();

        HttpResponseMessage applyResponse = await this.SendRequestAsync(
            HttpMethod.Post,
            $"/api/admin/application-permissions/applications/{applicationId}/import",
            new { ConcurrencyToken = (uint?)null });

        applyResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonNode applyJson = await ReadJsonAsync(applyResponse);
        applyJson["application"]!["manifestVersion"]!.GetValue<string>().ShouldBe("2.0.0");
        applyJson["result"]!["removedPermissions"]!.AsArray().Count.ShouldBe(1);
    }

    private async Task<Guid> CreateApplicationAsync(string applicationIdentifier, string? manifestBaseUrl = null)
    {
        HttpResponseMessage response = await this.SendRequestAsync(
            HttpMethod.Post,
            "/api/admin/application-permissions/applications",
            new
            {
                Manifest = new
                {
                    SchemaVersion = "1.0.0",
                    Application = new
                    {
                        Id = applicationIdentifier,
                        DisplayName = "Orders API",
                        Description = (string?)null,
                        Version = "1.0.0",
                    },
                    Permissions = new[]
                    {
                        new { Key = "order:read", DisplayName = "Read orders", Description = (string?)null, Category = (string?)null },
                        new { Key = "order:cancel", DisplayName = "Cancel orders", Description = (string?)null, Category = (string?)null },
                    },
                },
                OwnerId = this.actorId.ToString(),
                OwnerType = "User",
                ManifestBaseUrl = manifestBaseUrl,
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        JsonNode json = await ReadJsonAsync(response);
        return json["id"]!.GetValue<Guid>();
    }

    private async Task<JsonNode> GetApplicationAsync(Guid applicationId)
    {
        HttpResponseMessage response = await this.SendRequestAsync(
            HttpMethod.Get,
            $"/api/admin/application-permissions/applications/{applicationId}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await ReadJsonAsync(response);
    }

    private async Task<HttpResponseMessage> SendRequestAsync(HttpMethod method, string url, object? content = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(this.accessToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", this.accessToken);
        }

        if (content is not null)
        {
            request.Content = JsonContent.Create(content);
        }

        return await this.client.SendAsync(request);
    }

    private async Task SeedRegistryAdminRoleAsync(Guid userId)
    {
        await this.fixture.ExecuteDbContextAsync(async dbContext =>
        {
            Role role = Role.Create($"application-permission-admin-{Guid.NewGuid():N}", "Application permission admin").Value;
            role.SetPermissions([Permissions.ApplicationPermissions.All]);
            dbContext.Roles.Add(role);

            Result<RoleAssignment> assignmentResult = RoleAssignment.Create(
                new UserId(userId),
                role.Id,
                DateTimeOffset.UtcNow);
            assignmentResult.IsSuccess.ShouldBeTrue();
            dbContext.RoleAssignments.Add(assignmentResult.Value);

            await dbContext.SaveChangesAsync();
        });
    }

    private static object CreateManifestRequest(
        string applicationIdentifier,
        string version,
        IReadOnlyList<(string Key, string DisplayName)> permissions)
    {
        return new
        {
            Manifest = new
            {
                SchemaVersion = "1.0.0",
                Application = new
                {
                    Id = applicationIdentifier,
                    DisplayName = "Orders API",
                    Description = (string?)null,
                    Version = version,
                },
                Permissions = permissions.Select(permission => new
                {
                    permission.Key,
                    permission.DisplayName,
                    Description = (string?)null,
                    Category = (string?)null,
                }).ToArray(),
            },
            ConcurrencyToken = (uint?)null,
        };
    }

    private static string CreateManifestJson(
        string applicationIdentifier,
        string version,
        IReadOnlyList<(string Key, string DisplayName)> permissions)
    {
        string permissionsJson = string.Join(
            ",",
            permissions.Select(permission =>
                $$"""
                {
                  "key": "{{permission.Key}}",
                  "displayName": "{{permission.DisplayName}}",
                  "description": null,
                  "category": null
                }
                """));

        return $$"""
            {
              "schemaVersion": "1.0.0",
              "application": {
                "id": "{{applicationIdentifier}}",
                "displayName": "Orders API",
                "description": null,
                "version": "{{version}}"
              },
              "permissions": [{{permissionsJson}}]
            }
            """;
    }

    private static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response)
    {
        JsonNode? json = await response.Content.ReadFromJsonAsync<JsonNode>();
        return json ?? throw new InvalidOperationException("Response did not contain JSON.");
    }

    private sealed class RemoteManifestFixtureServer : IAsyncDisposable
    {
        private readonly HttpListener listener;
        private readonly CancellationTokenSource cancellationTokenSource = new();
        private readonly Task listenTask;
        private readonly string responseJson;

        private RemoteManifestFixtureServer(HttpListener listener, string baseUrl, string responseJson)
        {
            this.listener = listener;
            this.BaseUrl = baseUrl;
            this.responseJson = responseJson;
            this.listenTask = Task.Run(this.ListenAsync);
        }

        public string BaseUrl { get; }

        public static Task<RemoteManifestFixtureServer> StartAsync(string responseJson)
        {
            int port = GetFreePort();
            string baseUrl = $"http://localhost:{port}";
            var listener = new HttpListener();
            listener.Prefixes.Add($"{baseUrl}/");
            listener.Start();
            return Task.FromResult(new RemoteManifestFixtureServer(listener, baseUrl, responseJson));
        }

        public async ValueTask DisposeAsync()
        {
            await this.cancellationTokenSource.CancelAsync();
            this.listener.Stop();
            this.listener.Close();
            try
            {
                await this.listenTask;
            }
            catch (HttpListenerException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            finally
            {
                this.cancellationTokenSource.Dispose();
            }
        }

        private async Task ListenAsync()
        {
            while (!this.cancellationTokenSource.IsCancellationRequested)
            {
                HttpListenerContext context = await this.listener.GetContextAsync();
                if (!string.Equals(context.Request.Url?.AbsolutePath, "/.well-known/permissions", StringComparison.Ordinal))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                    continue;
                }

                byte[] body = System.Text.Encoding.UTF8.GetBytes(this.responseJson);
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = body.Length;
                await context.Response.OutputStream.WriteAsync(body);
                context.Response.Close();
            }
        }

        private static int GetFreePort()
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
