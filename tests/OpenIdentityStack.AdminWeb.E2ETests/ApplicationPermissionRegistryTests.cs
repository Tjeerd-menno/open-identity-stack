using System.Text.RegularExpressions;
using System.Net;
using Microsoft.Playwright;
using OpenIdentityStack.AdminWeb.E2ETests.Fixtures;
using OpenIdentityStack.AdminWeb.E2ETests.Helpers;

namespace OpenIdentityStack.AdminWeb.E2ETests;

public sealed class ApplicationPermissionRegistryTests : IAsyncLifetime
{
    private readonly AdminWebAppHostFixture fixture;
    private IBrowserContext? context;
    private IPage? page;

    public ApplicationPermissionRegistryTests(AdminWebAppHostFixture fixture)
    {
        this.fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        context = await fixture.CreateBrowserContextAsync();
        page = await context.NewPageAsync();

        page.Console += (_, msg) =>
        {
            if (msg.Type.Equals("error", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[BROWSER ERROR] {msg.Text}");
            }
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (page is not null)
        {
            await page.CloseAsync();
        }

        if (context is not null)
        {
            await context.CloseAsync();
        }
    }

    [Fact]
    public async Task InlineManifestFlow_ShouldCreateViewUpdateAndRejectSameVersion()
    {
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);

        string uniqueId = Guid.NewGuid().ToString("N")[..8].ToLowerInvariant();
        string applicationId = $"patient-api-{uniqueId}";
        string displayName = $"Patient API {uniqueId}";

        await page!.GotoAsync(new Uri(new Uri(fixture.AdminWebUrl!), "/application-permissions").ToString());
        await page.GetByRole(AriaRole.Heading, new() { Name = "Permissions", Exact = true, Level = 1 })
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await page!.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Add.*Application", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.WaitForURLAsync("**/application-permissions/new");

        await page.GetByLabel("Application ID", new() { Exact = true }).FillAsync(applicationId);
        await page.GetByLabel("Display Name", new() { Exact = true }).FillAsync(displayName);
        await page.GetByLabel("Description", new() { Exact = true }).FillAsync("Patient records registry");
        await page.GetByLabel("Version", new() { Exact = true }).FillAsync("1.0.0");
        await page.GetByLabel("Owner ID", new() { Exact = true }).FillAsync("admin@test.com");
        await page.GetByLabel("Manifest Base URL", new() { Exact = true }).FillAsync($"https://{applicationId}.example/api");
        await page.GetByLabel("Permission key 1", new() { Exact = true }).FillAsync("patient:read");
        await page.GetByLabel("Permission display name 1", new() { Exact = true }).FillAsync("Read patients");
        await page.GetByLabel("Permission description 1", new() { Exact = true }).FillAsync("Allows reading patient records");
        await page.GetByLabel("Permission category 1", new() { Exact = true }).FillAsync("Patients");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add Application", Exact = true }).ClickAsync();

        await page.WaitForURLAsync(new Regex(@"/application-permissions/[0-9a-fA-F-]{36}$"), new() { Timeout = 15000 });
        await TestHelpers.WaitForDetailPageAsync(page, expectedHeadingText: displayName);
        await page.GetByText($"{applicationId}:patient:read").ShouldBeVisibleAsync("Created permission should be shown on the detail page.");

        string updatedManifest = $$"""
            {
              "schemaVersion": "1.0.0",
              "application": {
                "id": "{{applicationId}}",
                "displayName": "{{displayName}}",
                "description": "Patient records registry",
                "version": "1.1.0"
              },
              "permissions": [
                {
                  "key": "patient:read",
                  "displayName": "Read patients",
                  "description": "Allows reading patient records",
                  "category": "Patients"
                },
                {
                  "key": "patient:write",
                  "displayName": "Write patients",
                  "description": "Allows updating patient records",
                  "category": "Patients"
                }
              ]
            }
            """;

        await page.GetByLabel("Manifest JSON", new() { Exact = true }).FillAsync(updatedManifest);
        await page.GetByRole(AriaRole.Button, new() { Name = "Preview Manifest", Exact = true }).ClickAsync();
        await page.GetByText("Preview ready").ShouldBeVisibleAsync("Newer non-destructive manifest preview should succeed.");

        await page.GetByRole(AriaRole.Button, new() { Name = "Apply Manifest", Exact = true }).ClickAsync();
        await page.GetByText($"{applicationId}:patient:write").ShouldBeVisibleAsync("Applied manifest should add the new permission.");
        await page.Locator("section").Filter(new() { HasText = "Manifest Version" })
            .GetByText("1.1.0", new() { Exact = true })
            .ShouldBeVisibleAsync("Applied manifest version should be shown.");

        await page.GetByLabel("Manifest JSON", new() { Exact = true }).FillAsync(updatedManifest);
        await page.GetByRole(AriaRole.Button, new() { Name = "Preview Manifest", Exact = true }).ClickAsync();
        await page.GetByText("Manifest version must be strictly newer").ShouldBeVisibleAsync("Same-version manifest should be rejected.");
    }

    [Fact]
    public async Task RolePermissionPicker_ShouldAssignDynamicWildcardAndRequirePlatformWildcardAcknowledgement()
    {
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);

        string uniqueId = Guid.NewGuid().ToString("N")[..8].ToLowerInvariant();
        string applicationId = $"claims-api-{uniqueId}";
        string displayName = $"Claims API {uniqueId}";

        await RegisterApplicationAsync(applicationId, displayName);

        await page!.GotoAsync(new Uri(new Uri(fixture.AdminWebUrl!), "/roles/new").ToString());
        await page.Locator("[data-testid='permission-selector']")
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await FillRoleFieldsAsync($"claims-wildcard-{uniqueId}", $"Claims Wildcard {uniqueId}");

        await page.GetByRole(AriaRole.Tab, new() { Name = displayName }).ClickAsync();
        await page.GetByRole(AriaRole.Checkbox, new() { NameRegex = new Regex("Claims API .*Claim All", RegexOptions.IgnoreCase) }).ClickAsync();
        await page.GetByRole(AriaRole.Checkbox, new() { Name = "Acknowledge wildcard grant" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Create Role", Exact = true }).ClickAsync();

        await page.WaitForURLAsync(new Regex(@"/roles/[0-9a-fA-F-]{36}$"), new() { Timeout = 15000 });
        await page.GetByText($"{applicationId}:claim:*").ShouldBeVisibleAsync("Dynamic wildcard permission should be visible on the created role.");

        await page.GotoAsync(new Uri(new Uri(fixture.AdminWebUrl!), "/roles/new").ToString());
        await page.Locator("[data-testid='permission-selector']")
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await FillRoleFieldsAsync($"platform-wildcard-{uniqueId}", $"Platform Wildcard {uniqueId}");
        await page.GetByRole(AriaRole.Checkbox, new() { Name = "Users All", Exact = true }).ClickAsync();

        Task<IResponse> conflictResponse = page.WaitForResponseAsync(response =>
            response.Url.Contains("/api/admin/roles", StringComparison.OrdinalIgnoreCase)
            && response.Status == 409);
        await page.GetByRole(AriaRole.Button, new() { Name = "Create Role", Exact = true }).ClickAsync();
        await conflictResponse;
        page.Url.ShouldEndWith("/roles/new");

        await page.GetByRole(AriaRole.Checkbox, new() { Name = "Acknowledge wildcard grant" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Create Role", Exact = true }).ClickAsync();

        await page.WaitForURLAsync(new Regex(@"/roles/[0-9a-fA-F-]{36}$"), new() { Timeout = 15000 });
        await page.GetByText("users:*").ShouldBeVisibleAsync("Platform wildcard permission should be visible after acknowledgement.");
    }

    [Fact]
    public async Task DestructiveManifestFlow_ShouldPreviewConfirmAndApplyRemoval()
    {
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);

        string uniqueId = Guid.NewGuid().ToString("N")[..8].ToLowerInvariant();
        string applicationId = $"billing-api-{uniqueId}";
        string displayName = $"Billing API {uniqueId}";

        await RegisterApplicationAsync(applicationId, displayName);
        await TestHelpers.WaitForDetailPageAsync(page!, expectedHeadingText: displayName);
        await page!.GetByText($"{applicationId}:claim:write").ShouldBeVisibleAsync("Second permission should exist before destructive update.");

        string destructiveManifest = $$"""
            {
              "schemaVersion": "1.0.0",
              "application": {
                "id": "{{applicationId}}",
                "displayName": "{{displayName}}",
                "description": "Billing registry",
                "version": "2.0.0"
              },
              "permissions": [
                {
                  "key": "claim:read",
                  "displayName": "Read claims",
                  "description": "Allows reading claims",
                  "category": "Claims"
                }
              ]
            }
            """;

        await page.GetByLabel("Manifest JSON", new() { Exact = true }).FillAsync(destructiveManifest);
        await page.GetByRole(AriaRole.Button, new() { Name = "Preview Manifest", Exact = true }).ClickAsync();
        await page.GetByText("Destructive changes detected").ShouldBeVisibleAsync("Destructive preview should be called out.");
        await page.GetByRole(AriaRole.Listitem)
            .Filter(new() { HasText = $"{applicationId}:claim:write" })
            .ShouldBeVisibleAsync("Removed permission should be listed in preview.");

        await page.GetByRole(AriaRole.Checkbox, new() { Name = "Confirm destructive manifest changes", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Apply Manifest", Exact = true }).ClickAsync();

        await page.GetByText("Manifest applied with removals").ShouldBeVisibleAsync("Destructive apply should report removals.");
        await page.GetByText("Removed permissions: 1").ShouldBeVisibleAsync("Result should show one removed permission.");
        await page.Locator("section").Filter(new() { HasText = "Permissions" })
            .Locator("div.font-medium")
            .Filter(new() { HasText = $"{applicationId}:claim:write" })
            .ShouldNotBeVisibleAsync("Removed permission should no longer be visible after apply.");
    }

    [Fact]
    public async Task RemoteManifestFlow_ShouldPreviewConfirmAndApplyFromLocalFixture()
    {
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);

        string uniqueId = Guid.NewGuid().ToString("N")[..8].ToLowerInvariant();
        string applicationId = $"remote-api-{uniqueId}";
        string displayName = $"Remote API {uniqueId}";
        await using RemoteManifestFixtureServer server = await RemoteManifestFixtureServer.StartAsync($$"""
            {
              "schemaVersion": "1.0.0",
              "application": {
                "id": "{{applicationId}}",
                "displayName": "{{displayName}}",
                "description": "Remote registry",
                "version": "2.0.0"
              },
              "permissions": [
                {
                  "key": "claim:read",
                  "displayName": "Read claims",
                  "description": "Allows reading claims",
                  "category": "Claims"
                }
              ]
            }
            """);

        await RegisterApplicationAsync(applicationId, displayName, server.BaseUrl);
        await TestHelpers.WaitForDetailPageAsync(page!, expectedHeadingText: displayName);
        await page!.GetByText($"{applicationId}:claim:write").ShouldBeVisibleAsync("Second permission should exist before remote import.");

        await page.GetByRole(AriaRole.Button, new() { Name = "Preview Remote Manifest", Exact = true }).ClickAsync();
        await page.GetByText("Remote preview found destructive changes").ShouldBeVisibleAsync("Remote preview should report destructive changes.");
        await page.GetByRole(AriaRole.Listitem)
            .Filter(new() { HasText = $"{applicationId}:claim:write" })
            .ShouldBeVisibleAsync("Remote preview should list removed permission.");

        await page.GetByRole(AriaRole.Checkbox, new() { Name = "Confirm destructive manifest changes", Exact = true }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Apply Remote Manifest", Exact = true }).ClickAsync();

        await page.GetByText("Remote manifest applied with removals").ShouldBeVisibleAsync("Remote apply should report removals.");
        await page.GetByText("Removed permissions: 1").ShouldBeVisibleAsync("Remote apply result should show one removed permission.");
        await page.Locator("section").Filter(new() { HasText = "Manifest Version" })
            .GetByText("2.0.0", new() { Exact = true })
            .ShouldBeVisibleAsync("Remote apply should advance the manifest version.");
    }

    private async Task RegisterApplicationAsync(string applicationId, string displayName, string? manifestBaseUrl = null)
    {
        await page!.GotoAsync(new Uri(new Uri(fixture.AdminWebUrl!), "/application-permissions/new").ToString());
        await page.GetByLabel("Application ID", new() { Exact = true }).FillAsync(applicationId);
        await page.GetByLabel("Display Name", new() { Exact = true }).FillAsync(displayName);
        await page.GetByLabel("Description", new() { Exact = true }).FillAsync("Claims registry");
        await page.GetByLabel("Version", new() { Exact = true }).FillAsync("1.0.0");
        await page.GetByLabel("Owner ID", new() { Exact = true }).FillAsync("admin@test.com");
        await page.GetByLabel("Manifest Base URL", new() { Exact = true }).FillAsync(manifestBaseUrl ?? $"https://{applicationId}.example/api");
        await page.GetByLabel("Permission key 1", new() { Exact = true }).FillAsync("claim:read");
        await page.GetByLabel("Permission display name 1", new() { Exact = true }).FillAsync("Read claims");
        await page.GetByLabel("Permission description 1", new() { Exact = true }).FillAsync("Allows reading claims");
        await page.GetByLabel("Permission category 1", new() { Exact = true }).FillAsync("Claims");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add Permission", Exact = true }).ClickAsync();
        await page.GetByLabel("Permission key 2", new() { Exact = true }).FillAsync("claim:write");
        await page.GetByLabel("Permission display name 2", new() { Exact = true }).FillAsync("Write claims");
        await page.GetByLabel("Permission description 2", new() { Exact = true }).FillAsync("Allows writing claims");
        await page.GetByLabel("Permission category 2", new() { Exact = true }).FillAsync("Claims");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add Application", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/application-permissions/[0-9a-fA-F-]{36}$"), new() { Timeout = 15000 });
    }

    private async Task FillRoleFieldsAsync(string name, string displayName)
    {
        await page!.GetByLabel("Name", new() { Exact = true }).FillAsync(name);
        await page.GetByLabel("Display Name", new() { Exact = true }).FillAsync(displayName);
        await page.GetByLabel("Description (Optional)", new() { Exact = true }).FillAsync("Wildcard E2E role");
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
            HttpListener listener = new();
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
            System.Net.Sockets.TcpListener listener = new(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
