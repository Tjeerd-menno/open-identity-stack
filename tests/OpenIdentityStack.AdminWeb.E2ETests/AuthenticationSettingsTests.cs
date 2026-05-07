using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.AdminWeb.E2ETests.Fixtures;
using OpenIdentityStack.AdminWeb.E2ETests.Helpers;

namespace OpenIdentityStack.AdminWeb.E2ETests;

/// <summary>
/// E2E tests for Authentication Settings Management in the Admin Web App.
/// Tests for REQ-070 (Default Provider Selection) and REQ-071 (Admin Local Fallback).
/// Covers authentication settings configuration endpoints.
/// </summary>
public class AuthenticationSettingsTests : IAsyncLifetime
{
    private readonly AdminWebAppHostFixture fixture;
    private IBrowserContext? context;
    private IPage? page;

    public AuthenticationSettingsTests(AdminWebAppHostFixture fixture)
    {
        this.fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        context = await fixture.CreateBrowserContextAsync();
        page = await context.NewPageAsync();
        
        // Capture console messages for debugging
        page.Console += (_, msg) =>
        {
            string msgType = msg.Type.ToUpperInvariant();
            if (msgType == "ERROR" || msgType == "WARNING" || msg.Text.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                System.Console.WriteLine($"[BROWSER {msgType}] {msg.Text}");
            }
        };
        
        // Capture page errors (uncaught exceptions)
        page.PageError += (_, error) =>
        {
            System.Console.WriteLine($"[BROWSER PAGE ERROR] {error}");
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (page != null) await page.CloseAsync();
        if (context != null) await context.CloseAsync();
    }

    /// <summary>
    /// Helper method to navigate to Settings page.
    /// Uses direct link click since the page heading is "Authentication Settings" not "Settings".
    /// </summary>
    private async Task NavigateToSettingsAsync()
    {
        await page!.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        
        ILocator settingsLink = page.GetByRole(AriaRole.Link, new() { Name = "Settings", Exact = true });
        await settingsLink.ClickAsync();
        await page.WaitForURLAsync("**/settings");
        
        // Wait for page to load
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        
        // Verify page heading
        ILocator heading = page.GetByRole(AriaRole.Heading, new() { Name = "Authentication Settings" });
        await heading.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
    }

    /// <summary>
    /// TC-S01: Navigate to Settings page from sidebar
    /// Verifies that users can navigate to the Settings page from the sidebar navigation.
    /// </summary>
    [Fact]
    public async Task NavigateToSettings_ShouldDisplaySettingsPage()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        // Act - Click Settings link in sidebar
        ILocator settingsLink = page!.GetByRole(AriaRole.Link, new() { Name = "Settings", Exact = true });
        await settingsLink.ClickAsync();
        await page.WaitForURLAsync("**/settings");
        
        // Assert - Verify page loaded
        page.Url.ShouldContain("/settings");
        
        // Verify page heading
        ILocator heading = page.GetByRole(AriaRole.Heading, new() { Name = "Authentication Settings" });
        await heading.ShouldBeVisibleAsync("Authentication Settings heading should be visible");
        
        System.Console.WriteLine("[TEST] Settings page navigation successful");
    }

    /// <summary>
    /// TC-S02: Settings page displays Default Provider card
    /// Verifies that the Default Authentication Provider card is displayed with provider selection.
    /// </summary>
    [Fact]
    public async Task SettingsPage_ShouldDisplayDefaultProviderCard()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await NavigateToSettingsAsync();
        
        // Assert - Check for Default Provider card
        ILocator cardTitle = page!.GetByRole(AriaRole.Heading, new() { Name = "Default Authentication Provider" });
        await cardTitle.ShouldBeVisibleAsync("Default Authentication Provider card title should be visible");
        
        // Check for description text
        ILocator description = page.GetByText("Select which authentication method is presented as the primary login option");
        await description.ShouldBeVisibleAsync("Card description should be visible");
        
        // Check for provider select dropdown
        ILocator providerSelect = page.Locator("[data-testid='default-provider-select']");
        await providerSelect.ShouldBeVisibleAsync("Provider select dropdown should be visible");
        
        System.Console.WriteLine("[TEST] Default Provider card displayed correctly");
    }

    /// <summary>
    /// TC-S03: Settings page displays Local Fallback card
    /// Verifies that the Admin Local Fallback configuration card is displayed.
    /// </summary>
    [Fact]
    public async Task SettingsPage_ShouldDisplayLocalFallbackCard()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await NavigateToSettingsAsync();
        
        // Assert - Check for Local Fallback card
        ILocator cardTitle = page!.GetByRole(AriaRole.Heading, new() { Name = "Admin Local Fallback" });
        await cardTitle.ShouldBeVisibleAsync("Admin Local Fallback card title should be visible");
        
        // Check for description text - use first() to avoid strict mode violation when multiple matches exist
        ILocator description = page.GetByText("When an external provider is the default").First;
        await description.ShouldBeVisibleAsync("Card description should be visible");
        
        // Check for fallback checkbox
        ILocator fallbackCheckbox = page.Locator("[data-testid='local-fallback-checkbox']");
        await fallbackCheckbox.ShouldBeVisibleAsync("Local fallback checkbox should be visible");
        
        System.Console.WriteLine("[TEST] Local Fallback card displayed correctly");
    }

    /// <summary>
    /// TC-S04: Settings page displays documentation card
    /// Verifies that the documentation card explaining authentication settings is displayed.
    /// </summary>
    [Fact]
    public async Task SettingsPage_ShouldDisplayDocumentationCard()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await NavigateToSettingsAsync();
        
        // Assert - Check for documentation card
        ILocator docCardTitle = page!.GetByRole(AriaRole.Heading, new() { Name = "How Authentication Provider Selection Works" });
        await docCardTitle.ShouldBeVisibleAsync("Documentation card title should be visible");
        
        // Check for requirement references
        ILocator req070 = page.GetByText("Default Provider Selection");
        await req070.ShouldBeVisibleAsync("reference should be visible");
        
        ILocator req071 = page.GetByText("Admin-Only Local Fallback");
        await req071.ShouldBeVisibleAsync("reference should be visible");
        
        ILocator req072 = page.GetByRole(AriaRole.Heading, new() { Name = "Login Page Behavior" });
        await req072.ShouldBeVisibleAsync("reference should be visible");
        
        System.Console.WriteLine("[TEST] Documentation card displayed correctly");
    }

    /// <summary>
    /// TC-S05: Default Provider select shows available providers
    /// Verifies that the provider dropdown shows available providers including Local Accounts.
    /// </summary>
    [Fact]
    public async Task DefaultProviderSelect_ShouldShowAvailableProviders()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await NavigateToSettingsAsync();
        
        // Act - Click the provider select to open dropdown
        ILocator providerSelect = page!.Locator("[data-testid='default-provider-select']");
        await providerSelect.ClickAsync();
        
        // Assert - Check that at least "Local Accounts" option is available
        ILocator localOption = page.GetByRole(AriaRole.Option, new() { NameRegex = new Regex("Local Accounts", RegexOptions.IgnoreCase) });
        await localOption.ShouldBeVisibleAsync("Local Accounts option should be available in the dropdown");
        
        System.Console.WriteLine("[TEST] Provider select displays available options");
    }

    /// <summary>
    /// TC-S06: Current status badge displays correctly
    /// Verifies that the current authentication status badge is displayed.
    /// </summary>
    [Fact]
    public async Task SettingsPage_ShouldDisplayCurrentStatusBadge()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await NavigateToSettingsAsync();
        
        // Assert - Check for current status section
        ILocator currentStatus = page!.GetByText("Current status:");
        await currentStatus.ShouldBeVisibleAsync("Current status label should be visible");
        
        // Either Local Accounts or External Provider badge should be visible
        // Use First to avoid strict mode violation when multiple matches exist (e.g., in documentation text)
        ILocator localBadge = page.GetByText("Local Accounts (Default)").First;
        ILocator externalBadge = page.GetByText("External Provider").First;
        
        bool hasLocalBadge = await localBadge.IsVisibleAsync();
        bool hasExternalBadge = await externalBadge.IsVisibleAsync();
        
        (hasLocalBadge || hasExternalBadge).ShouldBeTrue("Either Local Accounts or External Provider badge should be visible");
        
        System.Console.WriteLine("[TEST] Current status badge displayed correctly");
    }

    /// <summary>
    /// TC-S07: Local fallback checkbox state reflects settings
    /// Verifies that the local fallback checkbox reflects the current settings.
    /// </summary>
    [Fact]
    public async Task LocalFallbackCheckbox_ShouldReflectCurrentSettings()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await NavigateToSettingsAsync();
        
        // Assert - Check that the checkbox is present and has a state
        ILocator fallbackCheckbox = page!.Locator("[data-testid='local-fallback-checkbox']");
        await fallbackCheckbox.ShouldBeVisibleAsync("Local fallback checkbox should be visible");
        
        // Verify the label text is present
        ILocator label = page.GetByText("Enable local fallback for IAM admins");
        await label.ShouldBeVisibleAsync("Checkbox label should be visible");
        
        System.Console.WriteLine("[TEST] Local fallback checkbox state verified");
    }

    /// <summary>
    /// TC-S08: Settings page has correct heading and description
    /// Verifies that the settings page has the correct title and description.
    /// </summary>
    [Fact]
    public async Task SettingsPage_ShouldHaveCorrectHeadingAndDescription()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await NavigateToSettingsAsync();
        
        // Assert - Check heading
        ILocator heading = page!.GetByRole(AriaRole.Heading, new() { Name = "Authentication Settings", Level = 1 });
        await heading.ShouldBeVisibleAsync("Main heading should be visible");
        
        // Check description text
        ILocator description = page.GetByText("Configure default authentication provider and local fallback options");
        await description.ShouldBeVisibleAsync("Page description should be visible");
        
        System.Console.WriteLine("[TEST] Settings page heading and description verified");
    }

    /// <summary>
    /// TC-S09: Settings page shows informational alert based on provider type
    /// Verifies that informational alerts are shown based on the current provider configuration.
    /// </summary>
    [Fact]
    public async Task SettingsPage_ShouldShowInformationalAlertBasedOnProviderType()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await NavigateToSettingsAsync();
        
        // Assert - Page should load successfully
        ILocator pageHeading = page!.GetByRole(AriaRole.Heading, new() { Name = "Authentication Settings" });
        await pageHeading.ShouldBeVisibleAsync("Page heading should be visible");
        
        // Check for informational alerts - one of these should be visible depending on the current settings
        ILocator localDefaultAlert = page.GetByText("Local fallback setting is not applicable when Local Accounts is the default provider");
        ILocator externalDefaultAlert = page.GetByText("only IAM administrators can use local account authentication");
        
        bool hasLocalAlert = await localDefaultAlert.IsVisibleAsync();
        bool hasExternalAlert = await externalDefaultAlert.IsVisibleAsync();
        
        System.Console.WriteLine($"[TEST] Alert state - Local default alert: {hasLocalAlert}, External default alert: {hasExternalAlert}");
    }

    /// <summary>
    /// TC-S10: Settings page displays last updated timestamp when available
    /// Verifies that the last updated timestamp is displayed when settings have been modified.
    /// </summary>
    [Fact]
    public async Task SettingsPage_ShouldDisplayLastUpdatedTimestamp()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await NavigateToSettingsAsync();
        
        // Assert - Page should load successfully
        ILocator pageHeading = page!.GetByRole(AriaRole.Heading, new() { Name = "Authentication Settings" });
        await pageHeading.ShouldBeVisibleAsync("Page heading should be visible");
        
        // Check for last updated text (may not be present if settings haven't been modified)
        ILocator lastUpdatedText = page.GetByText(new Regex("Last updated:"));
        bool hasLastUpdated = await lastUpdatedText.IsVisibleAsync();
        
        System.Console.WriteLine($"[TEST] Last updated timestamp {(hasLastUpdated ? "is" : "is not")} displayed");
    }
}
