using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.AdminWeb.E2ETests.Fixtures;

namespace OpenIdentityStack.AdminWeb.E2ETests;

/// <summary>
/// E2E tests for the Admin Web App navigation and layout.
/// Tests the MVP foundation: routing, sidebar navigation, and basic UI structure.
/// </summary>
public class AdminWebLayoutTests : IAsyncLifetime
{
    private readonly AdminWebAppHostFixture fixture;
    private IBrowserContext? context;
    private IPage? page;

    public AdminWebLayoutTests(AdminWebAppHostFixture fixture)
    {
        this.fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        context = await fixture.CreateBrowserContextAsync();
        page = await context.NewPageAsync();
        page.SetDefaultTimeout(60000);
        page.SetDefaultNavigationTimeout(60000);
    }

    public async ValueTask DisposeAsync()
    {
        if (page != null) await page.CloseAsync();
        if (context != null) await context.CloseAsync();
    }
    
    /// <summary>
    /// Helper method to log in as the test admin user.
    /// </summary>
    private async Task LoginAsTestAdminAsync()
    {
        string url = fixture.AdminWebUrl ?? throw new InvalidOperationException("AdminWeb URL is null");
        string urlRoot = Regex.Escape(url.TrimEnd('/'));
        
        // Navigate to admin web app - may already be authenticated
        await page!.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await WaitForAuthenticatedShellOrLoginAsync(urlRoot);
    }

    private async Task WaitForAuthenticatedShellOrLoginAsync(string urlRoot)
    {
        DateTime startedAt = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow - startedAt < timeout)
        {
            if (await IsAppShellVisibleAsync())
            {
                await WaitForAppShellAsync();
                return;
            }

            if (await IsLoginScreenVisibleAsync())
            {
                await CompleteLoginAsync(urlRoot);
                return;
            }

            if (page!.Url.Contains("/auth/callback", StringComparison.OrdinalIgnoreCase))
            {
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
            }

            await Task.Delay(250);
        }

        throw new TimeoutException("Timed out waiting for the authenticated shell or login screen.");
    }

    private async Task<bool> IsAppShellVisibleAsync()
    {
        ILocator usersLink = page!.GetByRole(AriaRole.Link, new() { Name = "Users", Exact = true });
        return await usersLink.IsVisibleAsync();
    }

    private async Task<bool> IsLoginScreenVisibleAsync()
    {
        if (page!.Url.Contains("/login", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        ILocator loginButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Sign in", RegexOptions.IgnoreCase) });
        return await loginButton.IsVisibleAsync();
    }

    private async Task CompleteLoginAsync(string urlRoot)
    {
        // Click Sign in button to start OIDC flow
        ILocator loginButton = page!.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Sign in", RegexOptions.IgnoreCase) });
        await loginButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        await loginButton.ClickAsync();

        // Wait for OIDC login page
        await page.WaitForURLAsync("**/Account/Login*", new() { Timeout = 15000 });

        // Enter credentials
        await page.GetByLabel("Email").FillAsync("admin@test.com");
        await page.GetByLabel("Password").FillAsync("Admin123!@456");

        // Submit login form
        ILocator submitButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Sign in", RegexOptions.IgnoreCase) });
        await submitButton.ClickAsync();

        // Wait for callback processing and redirect to home
        await page.WaitForURLAsync(new Regex($"(/auth/callback|{urlRoot}(/)?)"), new() { Timeout = 15000 });

        if (page.Url.Contains("/auth/callback", StringComparison.OrdinalIgnoreCase))
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
            await page.WaitForURLAsync(new Regex($"({urlRoot}(/)?|/login)"), new() { Timeout = 10000 });
        }

        // Wait for app to fully load
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        await WaitForAppShellAsync();
    }

    private async Task WaitForAppShellAsync()
    {
        ILocator heading = page!.GetByRole(AriaRole.Heading, new() { Name = "OpenIdentityStack Admin" });
        await heading.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        ILocator usersLink = page.GetByRole(AriaRole.Link, new() { Name = "Users", Exact = true });
        await usersLink.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
    }

    [Fact]
    public async Task AdminWeb_ShouldLoadHomePage()
    {
        // Arrange
        string url = fixture.AdminWebUrl ?? throw new InvalidOperationException("AdminWeb URL is null");

        // Act
        IResponse? response = await page!.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        
        // Assert
        response.ShouldNotBeNull();
        response!.Status.ShouldBe(200);

        ILocator loginButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Sign in", RegexOptions.IgnoreCase) });
        await loginButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        // Verify the page has the expected title after client-side redirects settle
        string title = await page.TitleAsync();
        title.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Sidebar_ShouldDisplayAllNavigationItems()
    {
        // Arrange - Login first since this requires authentication
        await LoginAsTestAdminAsync();

        // Act & Assert - Verify all navigation items are present
        // Use Exact = true to avoid matching multiple elements (e.g., "Sessions" vs "View active sessions")
        string[] expectedItems = new[] { "Users", "Roles", "Groups", "Applications", "Permissions", "Sessions", "Providers", "Settings" };
        
        foreach (string? item in expectedItems)
        {
            ILocator link = page!.GetByRole(AriaRole.Link, new() { Name = item, Exact = true });
            await link.ShouldBeVisibleAsync($"Navigation item '{item}' should be visible");
        }
    }

    [Fact]
    public async Task Navigation_ShouldNavigateToUsersPage()
    {
        // Arrange - Login first
        await LoginAsTestAdminAsync();
        
        // Act
        await page!.GetByRole(AriaRole.Link, new() { Name = "Users" }).ClickAsync();
        await page.WaitForURLAsync("**/users");
        
        // Assert
        page.Url.ShouldContain("/users");
    }

    [Fact]
    public async Task Navigation_ShouldNavigateToRolesPage()
    {
        // Arrange - Login first
        await LoginAsTestAdminAsync();
        
        // Act
        await page!.GetByRole(AriaRole.Link, new() { Name = "Roles" }).ClickAsync();
        await page.WaitForURLAsync("**/roles");
        
        // Assert
        page.Url.ShouldContain("/roles");
    }

    [Fact]
    public async Task Navigation_ShouldNavigateToGroupsPage()
    {
        // Arrange - Login first
        await LoginAsTestAdminAsync();
        
        // Act
        await page!.GetByRole(AriaRole.Link, new() { Name = "Groups" }).ClickAsync();
        await page.WaitForURLAsync("**/groups");
        
        // Assert
        page.Url.ShouldContain("/groups");
    }

    [Fact]
    public async Task Navigation_ShouldNavigateToApplicationsPage()
    {
        // Arrange - Login first
        await LoginAsTestAdminAsync();
        
        // Act
        await page!.GetByRole(AriaRole.Link, new() { Name = "Applications" }).ClickAsync();
        await page.WaitForURLAsync("**/applications");
        
        // Assert
        page.Url.ShouldContain("/applications");
    }

    [Fact]
    public async Task Navigation_ShouldNavigateToSessionsPage()
    {
        // Arrange - Login first
        await LoginAsTestAdminAsync();
        
        // Act - Use Exact = true to avoid matching "View active sessions" link on dashboard
        await page!.GetByRole(AriaRole.Link, new() { Name = "Sessions", Exact = true }).ClickAsync();
        await page.WaitForURLAsync("**/sessions");
        
        // Assert
        page.Url.ShouldContain("/sessions");
    }

    [Fact]
    public async Task Navigation_ShouldNavigateToProvidersPage()
    {
        // Arrange - Login first
        await LoginAsTestAdminAsync();
        
        // Act
        await page!.GetByRole(AriaRole.Link, new() { Name = "Providers" }).ClickAsync();
        await page.WaitForURLAsync("**/providers");
        
        // Assert
        page.Url.ShouldContain("/providers");
    }

    [Fact]
    public async Task Navigation_ShouldNavigateToSettingsPage()
    {
        // Arrange - Login first
        await LoginAsTestAdminAsync();
        
        // Act
        await page!.GetByRole(AriaRole.Link, new() { Name = "Settings" }).ClickAsync();
        await page.WaitForURLAsync("**/settings");
        
        // Assert
        page.Url.ShouldContain("/settings");
    }

    [Fact]
    public async Task Header_ShouldDisplayAppTitle()
    {
        // Arrange - Login first
        await LoginAsTestAdminAsync();

        // Act & Assert
        ILocator heading = page!.GetByRole(AriaRole.Heading, new() { Name = "OpenIdentityStack Admin" });
        await heading.ShouldBeVisibleAsync("App title should be visible in sidebar");
    }

    [Fact]
    public async Task Header_ShouldDisplayLogoutButton()
    {
        // Arrange - Login first
        await LoginAsTestAdminAsync();

        // Act - Click the user menu dropdown trigger (avatar/user area)
        ILocator userMenuTrigger = page!.Locator("[data-testid='user-menu-trigger']");
        if (!await userMenuTrigger.IsVisibleAsync())
        {
            userMenuTrigger = page.GetByRole(AriaRole.Button, new()
            {
                NameRegex = new Regex("(Test Admin|admin@test.com)", RegexOptions.IgnoreCase)
            });
            await userMenuTrigger.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        }

        await userMenuTrigger.ClickAsync();
        
        // Assert - Check that Sign Out menu item is visible
        ILocator signOutItem = page.GetByRole(AriaRole.Menuitem, new() { Name = "Sign Out" });
        await signOutItem.ShouldBeVisibleAsync("Sign Out menu item should be visible in user dropdown");
    }
}

/// <summary>
/// Extension methods for Playwright assertions with Shouldly-style syntax.
/// </summary>
internal static class PlaywrightAssertions
{
    public static async Task ShouldBeVisibleAsync(this ILocator locator, string? message = null)
    {
        await locator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        bool isVisible = await locator.IsVisibleAsync();
        isVisible.ShouldBeTrue(message ?? "Element should be visible");
    }
}
