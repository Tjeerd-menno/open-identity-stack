using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>E2E coverage for the ManagementWeb Applications list and detail (real admin API).</summary>
public sealed class ApplicationManagementTests : ManagementWebPageTest
{
    private static readonly string[] WebGrantTypes = ["authorization_code", "refresh_token"];
    private static readonly string[] WebScopes = ["openid", "profile"];
    private static readonly string[] WebRedirectUris = ["https://app.northwind.io/callback"];

    private Guid _appId;
    private string _appName = "";

    public ApplicationManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    protected override async Task SeedAsync()
    {
        _appName = $"Northwind Web {Unique}";
        _appId = await SeedConfidentialWebAppAsync(_appName);

        // Add an explicit, listable client secret so the Credentials tab has one to revoke.
        await ApiPostAsync($"/api/admin/applications/{_appId}/credentials/client-secrets", new
        {
            description = "Seed secret",
            revokeExisting = false,
        });
    }

    private async Task<Guid> SeedConfidentialWebAppAsync(string displayName)
    {
        JsonNode app = await ApiPostAsync("/api/admin/applications", new
        {
            clientId = $"northwind-web-{Unique}-{Guid.NewGuid():N}",
            displayName,
            description = (string?)null,
            profile = "Web",
            clientType = "Confidential",
            allowedGrantTypes = WebGrantTypes,
            allowedScopes = WebScopes,
            redirectUris = WebRedirectUris,
            postLogoutRedirectUris = Array.Empty<string>(),
            requirePkce = true,
            requireConsent = false,
        });
        return Guid.Parse(app["id"]!.GetValue<string>());
    }

    [Fact]
    public async Task OperatorCanBrowseApplicationsAndOpenDetail()
    {
        await GotoAsync("/applications");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Applications", Exact = true }).WaitForAsync();
        await Page.GetByText(_appName).WaitForAsync();

        await GotoAsync($"/applications/{_appId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = _appName, Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Configuration", Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Credentials", RegexOptions.IgnoreCase) }).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanRegisterAnApplication()
    {
        string display = $"Northwind Service {Unique}";
        await GotoAsync("/applications");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Create application", Exact = true }).ClickAsync();

        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByLabel("Display name").FillAsync(display);
        await dialog.GetByLabel("Client ID").FillAsync($"northwind-svc-{Unique}");
        // Machine-to-machine needs no redirect URIs, keeping the form deterministic.
        await dialog.GetByLabel("Application profile").ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = "Machine-to-machine", Exact = true }).ClickAsync();
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Create application", Exact = true }).ClickAsync();

        // The modal closes on success and the new application appears in the list.
        await Page.GetByText(display).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanAddAClientSecret()
    {
        await GotoAsync($"/applications/{_appId}");
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Credentials", RegexOptions.IgnoreCase) }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add secret", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Dialog).GetByRole(AriaRole.Button, new() { Name = "Generate secret", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Client secret created", RegexOptions.IgnoreCase)).WaitForAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Copy", Exact = true }).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanEditOAuthConfiguration()
    {
        await GotoAsync($"/applications/{_appId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = _appName, Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Edit configuration", Exact = true }).ClickAsync();

        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByText("Edit OAuth configuration").WaitForAsync();
        await dialog.GetByPlaceholder("Add a scope").FillAsync("email");
        await dialog.GetByPlaceholder("Add a scope").PressAsync("Enter");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Save configuration", Exact = true }).ClickAsync();

        await dialog.WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    [Fact]
    public async Task OperatorCanRevokeAClientSecret()
    {
        await GotoAsync($"/applications/{_appId}");
        await Page.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Credentials", RegexOptions.IgnoreCase) }).ClickAsync();

        // The seeded confidential app already has its initial secret credential.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Revoke", Exact = true }).First.ClickAsync();
        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Revoke", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Credential revoked", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanDisableAndReEnableAnApplication()
    {
        await GotoAsync($"/applications/{_appId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = _appName, Exact = true }).WaitForAsync();

        // Disable from the header action, then the badge reflects the new status.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Disable", Exact = true }).ClickAsync();
        await Page.GetByText(new Regex("Application updated", RegexOptions.IgnoreCase)).WaitForAsync();
        await Page.GetByText("Disabled", new() { Exact = true }).First.WaitForAsync();

        // The same control now re-enables the application.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Enable", Exact = true }).ClickAsync();
        await Page.GetByText(new Regex("Application updated", RegexOptions.IgnoreCase)).WaitForAsync();
        await Page.GetByText("Active", new() { Exact = true }).First.WaitForAsync();
    }

    [Fact]
    public async Task SearchDrivesTheApplicationsQuery()
    {
        await GotoAsync("/applications");
        await Page.GetByLabel("Search applications").FillAsync(_appName);
        await Page.GetByText(_appName).WaitForAsync();

        // A term that matches nothing exercises the query round-trip: the row drops out.
        await Page.GetByLabel("Search applications").FillAsync($"no-such-app-{Unique}");
        await Page.GetByText(_appName).WaitForAsync(new() { State = WaitForSelectorState.Detached });
    }

    [Fact]
    public async Task OperatorCanDeleteAnApplication()
    {
        Guid disposableId = await SeedConfidentialWebAppAsync($"Disposable App {Unique}");

        await GotoAsync($"/applications/{disposableId}");
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Settings", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Delete application", Exact = true }).ClickAsync();

        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Delete application", Exact = true }).ClickAsync();

        await ExpectTextAsync("Application deleted");
        await Page.WaitForURLAsync(new Regex(@"/applications/?$"));
    }
}
