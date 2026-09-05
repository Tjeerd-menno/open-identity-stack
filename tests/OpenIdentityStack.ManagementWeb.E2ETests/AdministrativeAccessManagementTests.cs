using System.Text.Json.Nodes;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

public sealed class AdministrativeAccessManagementTests(ManagementWebAppHostFixture fixture) : ManagementWebPageTest(fixture)
{
    private static readonly string[] machineGrants = ["client_credentials"];
    private static readonly string[] administrativeScopes = ["ois.admin"];

    [Fact]
    public async Task OperatorSeparatesBusinessGrantsFromAcknowledgedAdministrativeApproval()
    {
        string permissionNamespace = $"boundary-{Unique}";
        JsonNode group = await ApiPostAsync("/api/admin/groups", new { name = $"Boundary owners {Unique}" });
        await ApiPostAsync("/api/admin/application-permissions/applications", new
        {
            manifest = new
            {
                schemaVersion = "1.0.0",
                application = new { id = permissionNamespace, displayName = "Boundary business API", version = "1.0.0" },
                permissions = new[] { new { key = "orders:read", displayName = "Read orders", category = "Orders" } },
            },
            ownerId = group["id"]!.GetValue<string>(), ownerType = "group",
        });
        JsonNode client = await ApiPostAsync("/api/admin/applications", new
        {
            clientId = $"boundary-client-{Unique}", displayName = $"Boundary client {Unique}", profile = "MachineToMachine",
            clientType = "Confidential", allowedGrantTypes = machineGrants, allowedScopes = administrativeScopes,
            redirectUris = Array.Empty<string>(), postLogoutRedirectUris = Array.Empty<string>(), requirePkce = false, requireConsent = false,
        });
        string clientId = client["id"]!.GetValue<string>();
        await GotoAsync($"/applications/{clientId}");
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Resource access", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add protected resource", Exact = true }).ClickAsync();
        ILocator editor = Page.GetByRole(AriaRole.Dialog);
        await editor.GetByLabel("Display name").FillAsync($"Business resource {Unique}");
        await editor.GetByLabel("Audience URI").FillAsync($"urn:boundary:{Unique}");
        await editor.GetByLabel("Resource scope").FillAsync($"boundary.{Unique}");
        await editor.GetByLabel("Permission namespaces").FillAsync(permissionNamespace);
        await editor.GetByRole(AriaRole.Button, new() { Name = "Save resource", Exact = true }).ClickAsync();
        await editor.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        await Page.GetByRole(AriaRole.Combobox, new() { Name = "Protected resource", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = $"Business resource {Unique}", Exact = true }).ClickAsync();
        await Page.GetByLabel("Application permissions", new() { Exact = true }).FillAsync($"{permissionNamespace}:orders:read");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save resource grant", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Resource grant saved." }).WaitForAsync();
        await ScreenshotAsync("resource-access");

        await Page.GetByRole(AriaRole.Tab, new() { Name = "Administrative access", Exact = true }).ClickAsync();
        await Page.GetByText("Not approved", new() { Exact = true }).WaitForAsync();
        await Page.GetByLabel("Machine permission ceiling", new() { Exact = true }).FillAsync("users:read");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save administrative access", Exact = true }).ClickAsync();
        ILocator approval = Page.GetByRole(AriaRole.Dialog, new() { Name = "Approve administrative access" });
        await approval.WaitForAsync();
        (await approval.GetByRole(AriaRole.Button, new() { Name = "Approve operation", Exact = true }).IsDisabledAsync()).ShouldBeTrue();
        await ScreenshotAsync("administrative-approval");
        await approval.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        JsonNode cancelled = await ApiGetAsync($"/api/admin/applications/{clientId}/administrative-access");
        cancelled["approved"]!.GetValue<bool>().ShouldBeFalse();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Save administrative access", Exact = true }).ClickAsync();
        await approval.GetByRole(AriaRole.Checkbox).CheckAsync();
        await approval.GetByRole(AriaRole.Button, new() { Name = "Approve operation", Exact = true }).ClickAsync();
        await Page.GetByText("Approved", new() { Exact = true }).WaitForAsync();
        JsonNode approved = await ApiGetAsync($"/api/admin/applications/{clientId}/administrative-access");
        approved["delegatedPermissions"]!.AsArray().ShouldBeEmpty();
        approved["applicationPermissions"]!.AsArray().Select(item => item!.GetValue<string>()).ShouldBe(["users:read"]);
        await ScreenshotAsync("administrative-approved");

        await Page.GetByLabel("Machine permission ceiling", new() { Exact = true }).FillAsync("");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Save administrative access", Exact = true }).ClickAsync();
        await Page.GetByText("Not approved", new() { Exact = true }).WaitForAsync();
        (await approval.CountAsync()).ShouldBe(0);
        JsonNode withdrawn = await ApiGetAsync($"/api/admin/applications/{clientId}/administrative-access");
        withdrawn["approved"]!.GetValue<bool>().ShouldBeFalse();
        withdrawn["revision"]!.GetValue<long>().ShouldBeGreaterThan(approved["revision"]!.GetValue<long>());
    }

    private async Task ScreenshotAsync(string name)
    {
        string directory = Path.Combine(Path.GetTempPath(), "ois-admin-boundary-e2e");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"{name}.png"), FullPage = true, Animations = ScreenshotAnimations.Disabled });
    }
}
