using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>
/// E2E coverage for the ManagementWeb Permissions (registered applications) list and detail,
/// seeded and asserted against the real admin API.
/// </summary>
public sealed class ApplicationPermissionsManagementTests : ManagementWebPageTest
{
    private Guid _registrationId;
    private string _ownerId = "";
    private string _appDisplay = "";

    public ApplicationPermissionsManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    protected override async Task SeedAsync()
    {
        _appDisplay = $"Orders API {Unique}";

        // Use a real group as the owner so any owner validation is satisfied.
        JsonNode group = await ApiPostAsync("/api/admin/groups", new
        {
            name = $"Platform {Unique}",
            description = "Owner group",
        });
        _ownerId = group["id"]!.GetValue<string>();

        JsonNode registration = await ApiPostAsync("/api/admin/application-permissions/applications", new
        {
            manifest = new
            {
                schemaVersion = "1.0.0",
                application = new { id = $"orders-api-{Unique}", displayName = _appDisplay, version = "1.0.0" },
                permissions = new[]
                {
                    new { key = "orders:read", displayName = "Read orders", category = "Orders" },
                },
            },
            ownerId = _ownerId,
            ownerType = "group",
        });
        _registrationId = Guid.Parse((registration["id"] ?? registration["applicationId"])!.GetValue<string>());
    }

    [Fact]
    public async Task OperatorCanBrowseRegisteredApplicationsAndOpenDetail()
    {
        await GotoAsync("/application-permissions");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Permissions", Exact = true }).WaitForAsync();
        await Page.GetByText(_appDisplay).WaitForAsync();

        await GotoAsync($"/application-permissions/{_registrationId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = _appDisplay, Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Permissions", RegexOptions.IgnoreCase) }).WaitForAsync();
        ILocator permissionKey = Page.GetByText("orders:read");
        await permissionKey.WaitForAsync();
        (await permissionKey.IsVisibleAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task OperatorCanRegisterAManifestManually()
    {
        string display = $"Billing API {Unique}";
        await GotoAsync("/application-permissions");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Register application", Exact = true }).ClickAsync();

        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByText("Register manually").ClickAsync();
        await dialog.GetByLabel("Application ID").FillAsync($"billing-api-{Unique}");
        await dialog.GetByLabel("Display name").FillAsync(display);
        await dialog.GetByLabel("Owner ID").FillAsync(_ownerId);
        await dialog.GetByLabel(new Regex("Permission key 1", RegexOptions.IgnoreCase)).FillAsync("billing:read");
        await dialog.GetByLabel(new Regex("Permission name 1", RegexOptions.IgnoreCase)).FillAsync("Read billing");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Register application", Exact = true }).ClickAsync();

        // Registration navigates to the new application's permission detail page.
        await Page.WaitForURLAsync(new Regex(@"/application-permissions/[^/]+$"));
        ILocator heading = Page.GetByRole(AriaRole.Heading, new() { Name = display, Exact = true });
        await heading.WaitForAsync();
        (await heading.IsVisibleAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task OperatorCanDisableARegisteredApplication()
    {
        await GotoAsync($"/application-permissions/{_registrationId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = _appDisplay, Exact = true }).WaitForAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Disable", Exact = true }).ClickAsync();
        ILocator toast = Page.GetByText(new Regex("Application updated", RegexOptions.IgnoreCase));
        await toast.WaitForAsync();
        (await toast.IsVisibleAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task OperatorCanDisableAndReEnableARegisteredApplication()
    {
        await GotoAsync($"/application-permissions/{_registrationId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = _appDisplay, Exact = true }).WaitForAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Disable", Exact = true }).ClickAsync();
        await Page.GetByText(new Regex("Application updated", RegexOptions.IgnoreCase)).WaitForAsync();
        await Page.GetByText("Disabled", new() { Exact = true }).First.WaitForAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Enable", Exact = true }).ClickAsync();
        await Page.GetByText(new Regex("Application updated", RegexOptions.IgnoreCase)).WaitForAsync();
        ILocator activeBadge = Page.GetByText("Active", new() { Exact = true }).First;
        await activeBadge.WaitForAsync();
        (await activeBadge.IsVisibleAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task OperatorSeesDeclaredMaintainerFallback()
    {
        await GotoAsync($"/application-permissions/{_registrationId}");
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Maintainers", RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.GetByText(new Regex("No delegated maintainers", RegexOptions.IgnoreCase)).WaitForAsync();
        ILocator ownerFallback = Page.GetByText(_ownerId);
        await ownerFallback.WaitForAsync();
        (await ownerFallback.IsVisibleAsync()).ShouldBeTrue();
    }
}
