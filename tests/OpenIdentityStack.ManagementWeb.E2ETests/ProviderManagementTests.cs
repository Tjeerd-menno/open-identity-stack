using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>E2E coverage for the ManagementWeb Identity providers list and detail (real admin API).</summary>
public sealed class ProviderManagementTests : ManagementWebPageTest
{
    private static readonly string[] ProviderScopes = ["openid", "email", "profile"];

    private Guid _providerId;
    private string _providerDisplay = "";

    public ProviderManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    protected override async Task SeedAsync()
    {
        _providerDisplay = $"Google {Unique}";
        _providerId = await SeedProviderAsync($"google-{Unique}", _providerDisplay);
    }

    private async Task<Guid> SeedProviderAsync(string name, string displayName)
    {
        JsonNode provider = await ApiPostAsync("/api/admin/providers", new
        {
            name,
            displayName,
            authority = "https://accounts.google.com",
            clientId = "northwind.apps.googleusercontent.com",
            scopes = ProviderScopes,
            jitProvisioningEnabled = true,
        });
        return Guid.Parse(provider["id"]!.GetValue<string>());
    }

    [Fact]
    public async Task OperatorCanBrowseProvidersAndOpenDetail()
    {
        await GotoAsync("/providers");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Identity providers", Exact = true }).WaitForAsync();
        await Page.GetByText(_providerDisplay).WaitForAsync();

        await GotoAsync($"/providers/{_providerId}");
        await Page.GetByRole(AriaRole.Heading, new() { Name = _providerDisplay, Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Connection", Exact = true }).WaitForAsync();
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Settings", Exact = true }).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanAddAProvider()
    {
        await GotoAsync("/providers");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Add provider", Exact = true }).ClickAsync();

        ILocator dialog = Page.GetByRole(AriaRole.Dialog);
        string display = $"Okta {Unique}";
        await dialog.GetByPlaceholder("google-workspace").FillAsync($"okta-{Unique}");
        await dialog.GetByLabel("Display name").FillAsync(display);
        await dialog.GetByLabel(new Regex("Issuer", RegexOptions.IgnoreCase)).FillAsync("https://northwind.okta.com");
        await dialog.GetByLabel("Client ID").FillAsync("0oa1northwind");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Add provider", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex($"Added {Regex.Escape(display)}", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanDisableAProviderFromTheRowMenu()
    {
        await GotoAsync("/providers");
        await Page.GetByText(_providerDisplay).WaitForAsync();

        ILocator row = Page.Locator("tbody tr", new() { Has = Page.GetByText(_providerDisplay) }).First;
        await row.GetByRole(AriaRole.Button, new() { Name = "Row actions" }).ClickAsync();
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Disable provider", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Provider updated", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanToggleJitProvisioning()
    {
        await GotoAsync($"/providers/{_providerId}");
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Settings", Exact = true }).ClickAsync();
        await Page.GetByLabel("Just-in-time provisioning").ClickAsync(new LocatorClickOptions { Force = true });

        await Page.GetByText(new Regex("Provider updated", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanDeleteAProviderFromTheDetailPage()
    {
        Guid disposableId = await SeedProviderAsync($"entra-{Unique}", $"Entra ID {Unique}");

        await GotoAsync($"/providers/{disposableId}");
        await Page.GetByRole(AriaRole.Tab, new() { Name = "Settings", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Delete provider", Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Provider deleted", RegexOptions.IgnoreCase)).WaitForAsync();
    }
}
