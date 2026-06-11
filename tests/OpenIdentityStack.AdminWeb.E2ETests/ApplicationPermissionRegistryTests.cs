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
    public async Task ManualApplicationFlow_ShouldCreateViewAndAddPermission()
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
        await page.GetByLabel("Permission key 1", new() { Exact = true }).FillAsync("patient:read");
        await page.GetByLabel("Permission display name 1", new() { Exact = true }).FillAsync("Read patients");
        await page.GetByLabel("Permission description 1", new() { Exact = true }).FillAsync("Allows reading patient records");
        await page.GetByLabel("Permission category 1", new() { Exact = true }).FillAsync("Patients");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add Application", Exact = true }).ClickAsync();

        await page.WaitForURLAsync(new Regex(@"/application-permissions/[0-9a-fA-F-]{36}$"), new() { Timeout = 15000 });
        await TestHelpers.WaitForDetailPageAsync(page, expectedHeadingText: displayName);
        await page.GetByText($"{applicationId}:patient:read").ShouldBeVisibleAsync("Created permission should be shown on the detail page.");

        await page.GetByLabel("Manifest JSON", new() { Exact = true }).ShouldNotBeVisibleAsync("Manual applications should not expose manifest update controls.");
        await page.GetByRole(AriaRole.Button, new() { Name = "Preview Manifest", Exact = true }).ShouldNotBeVisibleAsync("Manual applications should not expose manifest preview.");
        await page.GetByRole(AriaRole.Button, new() { Name = "Apply Manifest", Exact = true }).ShouldNotBeVisibleAsync("Manual applications should not expose manifest apply.");

        await page.GetByLabel("Permission name", new() { Exact = true }).FillAsync("patient:write");
        await page.GetByLabel("Permission category", new() { Exact = true }).FillAsync("Patients");
        await page.GetByLabel("Permission description", new() { Exact = true }).FillAsync("Allows writing patient records");
        await page.Locator("section").Filter(new() { HasText = "Permissions" })
            .GetByRole(AriaRole.Button, new() { Name = "Add", Exact = true })
            .ClickAsync();

        await page.GetByText($"{applicationId}:patient:write").ShouldBeVisibleAsync("Manual applications should allow manually added permissions.");
    }

    [Fact]
    public async Task RolePermissionPicker_ShouldAssignDynamicWildcardAndConfirmWildcardSave()
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
        await page.GetByRole(AriaRole.Button, new() { Name = "Create Role", Exact = true }).ClickAsync();
        await page.GetByText("The permissions include wildcard permissions. Are you sure you want to save this role?").ShouldBeVisibleAsync("Wildcard role creation should ask for confirmation.");
        await page.GetByRole(AriaRole.Button, new() { Name = "Save role", Exact = true }).ClickAsync();

        await page.WaitForURLAsync(new Regex(@"/roles/[0-9a-fA-F-]{36}$"), new() { Timeout = 15000 });
        await page.GetByText($"{applicationId}:claim:*").ShouldBeVisibleAsync("Dynamic wildcard permission should be visible on the created role.");

        await page.GotoAsync(new Uri(new Uri(fixture.AdminWebUrl!), "/roles/new").ToString());
        await page.Locator("[data-testid='permission-selector']")
            .WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await FillRoleFieldsAsync($"platform-wildcard-{uniqueId}", $"Platform Wildcard {uniqueId}");
        await page.GetByRole(AriaRole.Checkbox, new() { Name = "Users All", Exact = true }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Create Role", Exact = true }).ClickAsync();
        await page.GetByText("The permissions include wildcard permissions. Are you sure you want to save this role?").ShouldBeVisibleAsync("Platform wildcard role creation should ask for confirmation.");
        page.Url.ShouldEndWith("/roles/new");

        await page.GetByRole(AriaRole.Button, new() { Name = "Save role", Exact = true }).ClickAsync();

        await page.WaitForURLAsync(new Regex(@"/roles/[0-9a-fA-F-]{36}$"), new() { Timeout = 15000 });
        await page.GetByText("users:*").ShouldBeVisibleAsync("Platform wildcard permission should be visible after confirmation.");
    }

    [Fact]
    public async Task ImportedApplicationFlow_ShouldCreateReadOnlyApplicationFromEndpoint()
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
                "version": "1.0.0"
              },
              "permissions": [
                {
                  "key": "claim:read",
                  "displayName": "Read claims",
                  "description": "Allows reading claims",
                  "category": "Claims"
                },
                {
                  "key": "claim:write",
                  "displayName": "Write claims",
                  "description": "Allows writing claims",
                  "category": "Claims"
                }
              ]
            }
            """);

        await page!.GotoAsync(new Uri(new Uri(fixture.AdminWebUrl!), "/application-permissions/new").ToString());
        await page.GetByLabel("Well-known permissions endpoint", new() { Exact = true }).FillAsync($"{server.BaseUrl}/.well-known/permissions");
        await page.GetByRole(AriaRole.Button, new() { Name = "Import Endpoint", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/application-permissions/[0-9a-fA-F-]{36}$"), new() { Timeout = 15000 });
        await TestHelpers.WaitForDetailPageAsync(page!, expectedHeadingText: displayName);
        await page.GetByText($"{applicationId}:claim:read").ShouldBeVisibleAsync("Imported permission should be shown.");
        await page.GetByText($"{applicationId}:claim:write").ShouldBeVisibleAsync("Imported permission should be shown.");
        await page.Locator("section[aria-label='Application details']")
            .GetByText("1.0.0", new() { Exact = true })
            .First
            .ShouldBeVisibleAsync("Imported manifest version should be shown.");

        await page.GetByLabel("Permission name", new() { Exact = true }).ShouldNotBeVisibleAsync("Imported applications should not expose manual permission editing.");
        await page.GetByRole(AriaRole.Button, new() { Name = "Add", Exact = true }).ShouldNotBeVisibleAsync("Imported applications should not expose manual permission editing.");

        await page.GetByRole(AriaRole.Button, new() { Name = "Edit", Exact = true }).ClickAsync();
        await page.GetByLabel("Manifest Base URL", new() { Exact = true }).ShouldBeVisibleAsync("Imported applications should allow editing only the manifest URL.");
        await page.GetByLabel("New owner", new() { Exact = true }).ShouldNotBeVisibleAsync("Imported applications should not expose ownership editing.");
        await page.GetByLabel("New maintainer", new() { Exact = true }).ShouldNotBeVisibleAsync("Imported applications should not expose maintainer editing.");
    }

    private async Task RegisterApplicationAsync(string applicationId, string displayName)
    {
        await page!.GotoAsync(new Uri(new Uri(fixture.AdminWebUrl!), "/application-permissions/new").ToString());
        await page.GetByLabel("Application ID", new() { Exact = true }).FillAsync(applicationId);
        await page.GetByLabel("Display Name", new() { Exact = true }).FillAsync(displayName);
        await page.GetByLabel("Description", new() { Exact = true }).FillAsync("Claims registry");
        await page.GetByLabel("Version", new() { Exact = true }).FillAsync("1.0.0");
        await page.GetByLabel("Owner ID", new() { Exact = true }).FillAsync("admin@test.com");
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
