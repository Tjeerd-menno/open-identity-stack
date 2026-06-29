using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.ManagementWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.ManagementWeb.E2ETests;

/// <summary>E2E coverage for the ManagementWeb Authentication settings screen (real admin API).</summary>
public sealed class SettingsManagementTests : ManagementWebPageTest
{
    private static readonly string[] OpenidScope = ["openid"];

    private string _providerName = "";

    public SettingsManagementTests(ManagementWebAppHostFixture fixture) : base(fixture)
    {
    }

    protected override async Task SeedAsync()
    {
        _providerName = $"Okta {Unique}";
        await ApiPostAsync("/api/admin/providers", new
        {
            name = $"okta-{Unique}",
            displayName = _providerName,
            authority = "https://example.okta.com",
            clientId = "0oa-northwind-clientid",
            scopes = OpenidScope,
            jitProvisioningEnabled = true,
        });
    }

    [Fact]
    public async Task OperatorCanViewAuthenticationSettings()
    {
        await GotoAsync("/providers/settings");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Authentication settings", Exact = true }).WaitForAsync();
        await Page.GetByText("Default sign-in", new() { Exact = true }).WaitForAsync();
        await Page.GetByLabel(new Regex("Local password fallback", RegexOptions.IgnoreCase)).WaitForAsync();
        await Page.GetByText("Current configuration", new() { Exact = true }).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanToggleLocalFallback()
    {
        await GotoAsync("/providers/settings");
        await Page.GetByText("Default sign-in", new() { Exact = true }).WaitForAsync();
        // The Mantine Switch input is visually hidden; force the click.
        await Page.GetByLabel(new Regex("Local password fallback", RegexOptions.IgnoreCase))
            .ClickAsync(new LocatorClickOptions { Force = true });

        await Page.GetByText(new Regex("Local fallback updated", RegexOptions.IgnoreCase)).WaitForAsync();
    }

    [Fact]
    public async Task OperatorCanChangeTheDefaultProvider()
    {
        await GotoAsync("/providers/settings");
        await Page.GetByText("Default sign-in", new() { Exact = true }).WaitForAsync();

        // Pick the seeded upstream provider as the default sign-in method. Target the textbox
        // explicitly: the label is also referenced by the dropdown listbox.
        await Page.GetByRole(AriaRole.Textbox, new() { Name = "Default provider" }).ClickAsync();
        await Page.GetByRole(AriaRole.Option, new() { Name = _providerName, Exact = true }).ClickAsync();

        await Page.GetByText(new Regex("Default provider updated", RegexOptions.IgnoreCase)).WaitForAsync();
        // Selecting an upstream provider means local sign-in is no longer the default.
        await Page.GetByText("No", new() { Exact = true }).First.WaitForAsync();
    }
}
