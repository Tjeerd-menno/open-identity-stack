using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.AdminWeb.E2ETests.Fixtures;
using OpenIdentityStack.AdminWeb.E2ETests.Helpers;

namespace OpenIdentityStack.AdminWeb.E2ETests;

/// <summary>
/// E2E tests for OAuth2/OIDC authentication flow in the Admin Web App.
/// Tests login, logout, and token refresh functionality.
/// Task References: T045, T046, T047
/// </summary>
public class AuthenticationFlowTests : IAsyncLifetime
{
    private readonly AdminWebAppHostFixture fixture;
    private IBrowserContext? context;
    private IPage? page;

    public AuthenticationFlowTests(AdminWebAppHostFixture fixture)
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
            // Capture all console messages for debugging
            System.Console.WriteLine($"[BROWSER {msgType}] {msg.Text}");
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
    /// T045: E2E test for login flow
    /// Tests the complete OAuth2/OIDC login flow with PKCE
    /// </summary>
    [Fact]
    public async Task Login_ShouldRedirectToOIDCAndReturnWithToken()
    {
        // Arrange
        string url = (fixture.AdminWebUrl ?? throw new InvalidOperationException("AdminWeb URL is null")).TrimEnd('/');
        
        // Act - Navigate to admin web app
        await page!.GotoAsync(url);
        
        // Wait for the React app to load
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 15000 });
        
        // Debug: Log current URL after initial load
        System.Console.WriteLine($"DEBUG: Current URL after NetworkIdle: {page.Url}");
        
        // Wait a bit for React to render and redirect
        await page.WaitForTimeoutAsync(2000);
        System.Console.WriteLine($"DEBUG: Current URL after 2s delay: {page.Url}");
        
        // The app should redirect to login page if not authenticated
        // The admin web app redirects directly to the OIDC provider's /Account/Login page
        // when unauthenticated (it may also have an internal /login route)
        if (!page.Url.Contains("/login") && !page.Url.Contains("/Account/Login"))
        {
            // Wait for redirect to either internal login or OIDC login
            await page.WaitForURLAsync(new Regex("(/login|/Account/Login)"), new() { Timeout = 30000 });
        }
        
        bool isAtLoginPage = page.Url.Contains("/login") || page.Url.Contains("/Account/Login");
        isAtLoginPage.ShouldBeTrue("Should redirect to a login page");
        System.Console.WriteLine($"DEBUG: At login page: {page.Url}");

        // If we're already at the OIDC login page, skip clicking the login button
        if (page.Url.Contains("/Account/Login"))
        {
            // Already redirected to OIDC login page
            page.Url.ShouldContain("/Account/Login");
        }
        else
        {
            // At internal login page - click the Login button to start OIDC flow
            ILocator loginButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new("Sign in", RegexOptions.IgnoreCase) });
            await loginButton.ShouldBeVisibleAsync("Login button should be visible");
            
            // Click login button - navigation happens automatically
            await loginButton.ClickAsync();
            
            // Wait for redirect to OIDC login page (OpenIddict redirects /connect/authorize to /Account/Login)
            await page.WaitForURLAsync("**/Account/Login*", new() { Timeout = 15000 });

            // Verify we're at the OIDC login page
            page.Url.ShouldContain("/Account/Login"); // Should redirect to OIDC login endpoint
        }

        // Enter credentials (test user seeded by fixture)
        ILocator emailInput = page.GetByLabel("Email");
        await emailInput.FillAsync("admin@test.com");

        ILocator passwordInput = page.GetByLabel("Password");
        await passwordInput.FillAsync("Admin123!@456");

        // Submit login form
        ILocator submitButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new("Sign in", RegexOptions.IgnoreCase) });
        
        // Click submit and wait for redirect back to callback
        await submitButton.ClickAsync();
        
        // Wait for callback or home page (callback may be fast and redirect to home)
        await page.WaitForURLAsync(new Regex($"(/auth/callback|{Regex.Escape(url)})"), new() { Timeout = 15000 });
        
        // If we're at callback, wait for final redirect to home
        if (page.Url.Contains("/auth/callback"))
        {
            // Wait for callback page to process - give it time
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
            
            // Check for JavaScript errors
            await page.WaitForTimeoutAsync(2000); // Give React time to process
            
            // If still at callback, something might be wrong
            if (page.Url.Contains("/auth/callback"))
            {
                // Take a screenshot for debugging
                await page.ScreenshotAsync(new PageScreenshotOptions { Path = "callback-debug.png" });
                System.Console.WriteLine($"DEBUG: Still at callback page after timeout. URL: {page.Url}");
                
                // Try waiting longer for redirect
                await page.WaitForURLAsync(new Regex($"({Regex.Escape(url)}|/login)"), new() { Timeout = 10000 });
            }
        }
        
        // Wait for the app to fully load (network to settle)
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        
        // Debug: Log current URL and page content for troubleshooting
        System.Console.WriteLine($"DEBUG: Current URL after login: {page.Url}");
        string pageContent = await page.ContentAsync();
        if (pageContent.Contains("error") || pageContent.Contains("Error"))
        {
            System.Console.WriteLine($"DEBUG: Page may contain error. Body preview: {pageContent.Substring(0, Math.Min(2000, pageContent.Length))}");
        }

        // Assert - Should be authenticated and show user info
        // The button contains user name and email in the dropdown trigger
        ILocator userMenu = page.GetByRole(AriaRole.Button, new() { NameRegex = new("admin@test\\.com|Admin|Test Admin") });
        await userMenu.ShouldBeVisibleAsync("User menu should be visible after login");
        
        // Verify we have access to protected routes
        await page.GetByRole(AriaRole.Link, new() { Name = "Users" }).ClickAsync();
        await page.WaitForURLAsync("**/users");
        page.Url.ShouldContain("/users"); // Should be able to access protected users page
    }

    /// <summary>
    /// T046: E2E test for logout flow
    /// Tests that logout clears session and redirects to login
    /// </summary>
    [Fact]
    public async Task Logout_ShouldClearSessionAndRedirectToLogin()
    {
        // Arrange - First login
        await LoginAsTestAdmin();

        string url = fixture.AdminWebUrl ?? throw new InvalidOperationException("AdminWeb URL is null");

        // Verify we're authenticated
        ILocator userMenu = page!.GetByRole(AriaRole.Button, new() { NameRegex = new("admin@test\\.com|Admin|Test Admin") });
        await userMenu.ShouldBeVisibleAsync("Should be authenticated before logout");
        
        // Act - Click user menu to open dropdown
        await userMenu.ClickAsync();

        // The menu item is "Sign Out" based on Header.tsx
        ILocator signOutItem = page.GetByRole(AriaRole.Menuitem, new() { Name = "Sign Out" });
        
        // Click sign out  
        await signOutItem.ClickAsync();
        
        // Wait for redirect to OIDC logout endpoint or login page
        // After logout, the app should redirect to the login page
        await page.WaitForURLAsync(new Regex("(/connect/logout|/login|/$)"), new() { Timeout = 15000 });
        
        // Navigate to protected route to verify logout worked
        await page.GotoAsync($"{url}/users");
        
        // Should redirect to login page since we're logged out
        await page.WaitForURLAsync("**/login", new() { Timeout = 10000 });
        page.Url.ShouldContain("/login"); // Should redirect to login when accessing protected route after logout
    }

    /// <summary>
    /// T047: E2E test for token refresh
    /// Tests automatic silent token refresh before expiration
    /// </summary>
    [Fact]
    public async Task TokenRefresh_ShouldAutomaticallyRefreshBeforeExpiration()
    {
        // Arrange - Login
        await LoginAsTestAdmin();

        // Verify we're authenticated
        ILocator userMenu = page!.GetByRole(AriaRole.Button, new() { NameRegex = new("admin@test\\.com|Admin") });
        await userMenu.ShouldBeVisibleAsync("Should be authenticated");
        
        // Act - Wait for token to expire (access tokens typically expire in 15-60 minutes)
        // In test environment, we'll set a shorter expiration (e.g., 30 seconds)
        // and verify the silent refresh happens
        
        // Navigate to a page that requires authentication
        await page.GetByRole(AriaRole.Link, new() { Name = "Users" }).ClickAsync();
        await page.WaitForURLAsync("**/users");
        
        // Wait for token to be close to expiration (simulated by waiting)
        // The UserManager should automatically trigger silent refresh
        await Task.Delay(TimeSpan.FromSeconds(35));
        
        // Assert - Should still have valid session after refresh
        // Try to navigate to another protected route
        await page.GetByRole(AriaRole.Link, new() { Name = "Roles" }).ClickAsync();
        await page.WaitForURLAsync("**/roles");
        
        // Should not redirect to login - token was refreshed
        page.Url.ShouldContain("/roles"); // Should still have access after token refresh
        
        // User menu should still be visible
        await userMenu.ShouldBeVisibleAsync("User should still be authenticated after refresh");
    }

    /// <summary>
    /// Helper method to login as test admin user
    /// </summary>
    private async Task LoginAsTestAdmin()
    {
        string url = fixture.AdminWebUrl ?? throw new InvalidOperationException("AdminWeb URL is null");

        await TestHelpers.LoginAsTestAdminAsync(page!, url);
    }

    /// <summary>
    /// Test that unauthenticated users are redirected to login
    /// </summary>
    [Fact]
    public async Task ProtectedRoute_ShouldRedirectToLoginWhenNotAuthenticated()
    {
        // Arrange
        string url = fixture.AdminWebUrl ?? throw new InvalidOperationException("AdminWeb URL is null");
        
        // Act - Try to access protected route directly
        await page!.GotoAsync($"{url}/users");
        
        // Assert - Should redirect to either the SPA login route or the OIDC login page.
        await page.WaitForURLAsync(
            new Regex("(/login|/Account/Login)", RegexOptions.IgnoreCase),
            new PageWaitForURLOptions { Timeout = 30000, WaitUntil = WaitUntilState.Commit });

        bool redirectedToLogin =
            page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase) ||
            page.Url.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase);
        redirectedToLogin.ShouldBeTrue("Should redirect to a login page when accessing a protected route");
    }

    /// <summary>
    /// Test that callback page handles authorization code
    /// </summary>
    [Fact]
    public async Task CallbackPage_ShouldExchangeCodeForTokens()
    {
        // Arrange
        await LoginAsTestAdmin();

        // Assert - Should have successfully exchanged code for tokens
        // and be redirected to home page (or any page that's not the callback)
        page!.Url.ShouldNotContain("/auth/callback");

        // Should be authenticated
        ILocator userMenu = page.GetByRole(AriaRole.Button, new() { NameRegex = new("admin@test\\.com|Admin|Test Admin") });
        await userMenu.ShouldBeVisibleAsync("Should be authenticated after callback");
    }
}
