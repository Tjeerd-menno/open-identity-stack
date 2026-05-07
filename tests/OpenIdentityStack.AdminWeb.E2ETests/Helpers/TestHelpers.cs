using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace OpenIdentityStack.AdminWeb.E2ETests.Helpers;

/// <summary>
/// Shared helper methods for E2E tests to reduce code duplication.
/// Provides common operations like login, navigation, form filling, and waiting.
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Login as the test admin user.
    /// This is a common operation used in most E2E tests.
    /// </summary>
    public static async Task LoginAsTestAdminAsync(IPage page, string adminWebUrl)
    {
        string urlRoot = Regex.Escape(adminWebUrl.TrimEnd('/'));

        await page.GotoAsync(adminWebUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        await WaitForAuthenticatedShellOrLoginAsync(page, urlRoot);
    }

    private static async Task WaitForAuthenticatedShellOrLoginAsync(IPage page, string urlRoot)
    {
        DateTime startedAt = DateTime.UtcNow;
        var timeout = TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow - startedAt < timeout)
        {
            if (await IsAppShellVisibleAsync(page))
            {
                await WaitForAppShellAsync(page);
                return;
            }

            if (await IsLoginScreenVisibleAsync(page))
            {
                await CompleteLoginAsync(page, urlRoot);
                return;
            }

            if (page.Url.Contains("/auth/callback", StringComparison.OrdinalIgnoreCase))
            {
                await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new() { Timeout = 10000 });
            }

            await Task.Delay(250);
        }

        throw new TimeoutException("Timed out waiting for the authenticated shell or login screen.");
    }

    private static async Task<bool> IsAppShellVisibleAsync(IPage page)
    {
        ILocator usersLink = page.GetByRole(AriaRole.Link, new() { Name = "Users", Exact = true });
        return await usersLink.IsVisibleAsync();
    }

    private static async Task<bool> IsLoginScreenVisibleAsync(IPage page)
    {
        if (page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        ILocator loginButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Sign in", RegexOptions.IgnoreCase) });
        return await loginButton.IsVisibleAsync();
    }

    private static async Task CompleteLoginAsync(IPage page, string urlRoot)
    {
        ILocator loginButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Sign in", RegexOptions.IgnoreCase) });
        await loginButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        await loginButton.ClickAsync();

        await page.WaitForURLAsync("**/Account/Login*", new() { Timeout = 15000 });

        ILocator emailInput = page.GetByLabel("Email");
        await emailInput.WaitForAsync();
        await emailInput.FillAsync("admin@test.com");
        await page.GetByLabel("Password").FillAsync("Admin123!@456");

        ILocator submitButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Sign in", RegexOptions.IgnoreCase) });
        await submitButton.ClickAsync();

        await page.WaitForURLAsync(new Regex($"(/auth/callback|{urlRoot}(/)?)"), new() { Timeout = 15000 });

        if (page.Url.Contains("/auth/callback", StringComparison.OrdinalIgnoreCase))
        {
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new() { Timeout = 10000 });
            await page.WaitForURLAsync(new Regex($"({urlRoot}(/)?|/login)"), new() { Timeout = 10000 });
        }

        await WaitForAppShellAsync(page);
    }

    private static async Task WaitForAppShellAsync(IPage page)
    {
        ILocator heading = page.GetByRole(AriaRole.Heading, new() { Name = "OpenIdentityStack Admin" });
        await heading.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });

        ILocator usersLink = page.GetByRole(AriaRole.Link, new() { Name = "Users", Exact = true });
        await usersLink.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
    }

    /// <summary>
    /// Navigate to a specific feature page.
    /// </summary>
    /// <param name="page">Playwright page</param>
    /// <param name="featureName">Name of the feature (e.g., "Users", "Roles", "Groups")</param>
    public static async Task NavigateToFeatureAsync(IPage page, string featureName)
    {
        // Wait for any pending operations to complete
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        
        string slug = featureName.ToLowerInvariant().Replace(" ", "-");
        var currentUri = new System.Uri(page.Url);
        string currentPath = currentUri.AbsolutePath.TrimEnd('/');
        string targetPath = $"/{slug}";
        
        // Check if we're already on the exact target page
        if (currentPath.Equals(targetPath, System.StringComparison.OrdinalIgnoreCase))
        {
            System.Console.WriteLine($"[TestHelpers] Already on {targetPath}");
            return;
        }
        
        // If we're on a subpage of the target (e.g., /groups/123 when targeting /groups),
        // React Router link clicks may not navigate. Use direct navigation.
        bool isOnSubpage = currentPath.StartsWith(targetPath + "/", System.StringComparison.OrdinalIgnoreCase);
        
        if (isOnSubpage)
        {
            System.Console.WriteLine($"[TestHelpers] On subpage {currentPath}, using direct navigation to {targetPath}");
            string targetUrl = new System.Uri(currentUri, targetPath).ToString();
            await page.GotoAsync(targetUrl);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }
        else
        {
            // Normal navigation via sidebar link
            ILocator link = page.GetByRole(AriaRole.Link, new() { Name = featureName, Exact = true });
            
            // Ensure we click the one in navigation if multiple exist
            if (await link.CountAsync() > 1)
            {
                 ILocator navLink = page.Locator("nav").GetByRole(AriaRole.Link, new() { Name = featureName, Exact = true });
                 if (await navLink.CountAsync() > 0)
                 {
                     link = navLink;
                 }
            }
            
            await link.ClickAsync();
        }
        
        // Wait for landing on the page (check heading)
        // Use .First to avoid strict mode violations if duplicate elements exist temporarily
        try 
        {
             await page.GetByRole(AriaRole.Heading, new() { Name = featureName, Exact = true, Level = 1 })
                       .First
                       .WaitForAsync(new() { Timeout = 5000 });
        }
        catch 
        {
             // Fallback wait if heading is not found or slow
             await Task.Delay(1000);
        }
        
        // Verify we arrived at the list page by checking the H1 AND the URL
        bool headingVisible = false;
        
        // Check Heading
        try 
        {
             await page.GetByRole(AriaRole.Heading, new() { Name = featureName, Exact = true, Level = 1 })
                       .First
                       .WaitForAsync(new() { Timeout = 2000 });
             headingVisible = true;
        }
        catch { /* ignored */ }
        
        // Check URL
        bool urlCorrect = false;
        try 
        {
             urlCorrect = new System.Uri(page.Url).AbsolutePath.TrimEnd('/').EndsWith($"/{slug}", System.StringComparison.OrdinalIgnoreCase);
        }
        catch { /* ignored */ }
        
        System.Console.WriteLine($"[TestHelpers] Verify Nav: Feature='{featureName}', Slug='{slug}', Url='{page.Url}', HeadingVisible={headingVisible}, UrlCorrect={urlCorrect}");

        if (!headingVisible || !urlCorrect)
        {
             System.Console.WriteLine($"[TestHelpers] Warning: Navigation verification failed. HeadingVisible: {headingVisible}, UrlCorrect: {urlCorrect}, Current URL: {page.Url}. Attempting force navigation.");
             
             try 
             {
                 // Construct absolute URL
                 string targetUrl = new System.Uri(currentUri, targetPath).ToString();
                 
                 await page.GotoAsync(targetUrl);
                 await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                 await Task.Delay(500);
                 
                 // Verify again
                 await page.GetByRole(AriaRole.Heading, new() { Name = featureName, Exact = true, Level = 1 })
                       .First
                       .WaitForAsync(new() { Timeout = 5000 });
             }
             catch (System.Exception ex)
             {
                 System.Console.WriteLine($"[TestHelpers] Force navigation failed: {ex.Message}");
                 throw;
             }
        }

        // Additional wait for React to settle
        await Task.Delay(200);
    }

    /// <summary>
    /// Wait for a data table to finish loading.
    /// Data tables typically show a loading state, then render data.
    /// </summary>
    public static async Task WaitForDataTableAsync(IPage page)
    {
        // Wait for network to settle after data fetch
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        
        // Optional: Wait for table body to be visible (most tables have tbody)
        try
        {
            await page.Locator("tbody").WaitForAsync(new() { Timeout = 5000 });
        }
        catch
        {
            // If no tbody found, that's okay - might be an empty table
        }
    }

    /// <summary>
    /// Fill a form with multiple fields.
    /// Uses exact label matching to avoid strict mode violations when labels are similar.
    /// </summary>
    /// <param name="page">Playwright page</param>
    /// <param name="fields">Dictionary of field labels to values</param>
    public static async Task FillFormFieldsAsync(IPage page, Dictionary<string, string> fields)
    {
        foreach (KeyValuePair<string, string> field in fields)
        {
            // Use exact matching to avoid "Name" matching both "Name" and "Display Name"
            ILocator input = page.GetByLabel(field.Key, new() { Exact = true });
            await input.FillAsync(field.Value);
        }
    }

    /// <summary>
    /// Click a button and handle a confirmation dialog.
    /// </summary>
    /// <param name="page">Playwright page</param>
    /// <param name="buttonText">Text on the button to click</param>
    /// <param name="confirm">Whether to confirm (true) or cancel (false) the dialog</param>
    public static async Task ClickWithConfirmationAsync(IPage page, string buttonText, bool confirm = true)
    {
        // Set up dialog handler before clicking
        page.Dialog += async (_, dialog) =>
        {
            if (confirm)
                await dialog.AcceptAsync();
            else
                await dialog.DismissAsync();
        };

        // Click the button
        ILocator button = page.GetByRole(AriaRole.Button, new() { Name = buttonText });
        await button.ClickAsync();
        
        // Wait for any navigation or state changes
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
    }

    /// <summary>
    /// Search in a data table/list.
    /// Uses retry logic to handle React re-renders that may cause element detachment.
    /// </summary>
    /// <param name="page">Playwright page</param>
    /// <param name="searchTerm">Term to search for</param>
    public static async Task SearchInListAsync(IPage page, string searchTerm)
    {
        System.Console.WriteLine($"[TestHelpers] Searching for '{searchTerm}' on {page.Url}");

        // Wait for any pending renders to complete first
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        await Task.Delay(300); // Wait for React to settle
        
        // IMPROVED SELECTOR: Use CSS selector which is more robust than generic placeholder matching
        ILocator searchInput = page.Locator("input[placeholder*='Search']").First;
        
        try
        {
            // Wait for the input to be stable and visible
            await searchInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        }
        catch (System.Exception ex) when (ex is System.TimeoutException || ex.Message.Contains("Timeout"))
        {
            string url = page.Url;
            string content = await page.ContentAsync();
            string snippet = content.Length > 1000 ? string.Concat(content.AsSpan(0, 1000), "...") : content;
            
            System.Console.WriteLine($"[TestHelpers] Search input NOT FOUND on {url}. Content start: {snippet}");
            throw new System.TimeoutException($"Search input not found on {url}. Original error: {ex.Message}. Page content snippet: {snippet}", ex);
        }
        
        // Clear and fill with short delay to allow DOM to stabilize
        await searchInput.ClearAsync();
        await Task.Delay(100);
        await searchInput.FillAsync(searchTerm);
        
        // Wait for debounced search to complete (typically 300ms debounce + network)
        await Task.Delay(500);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
    }

    /// <summary>
    /// Click on a table row's view button to navigate to the detail page.
    /// Finds the row by matching text content, then clicks the View button within that row.
    /// </summary>
    /// <param name="page">Playwright page</param>
    /// <param name="rowText">Text to find in the row</param>
    public static async Task ClickTableRowAsync(IPage page, string rowText)
    {
        ILocator row = page.Locator("tr").Filter(new() { HasText = rowText });
        
        // Click the View button within the row (uses Eye icon with aria-label like "View user", "View role", etc.)
        ILocator viewButton = row.GetByRole(AriaRole.Button, new() { NameRegex = new System.Text.RegularExpressions.Regex("View", System.Text.RegularExpressions.RegexOptions.IgnoreCase) });
        
        if (await viewButton.CountAsync() > 0)
        {
            await viewButton.ClickAsync();
        }
        else
        {
            // Fallback: try clicking the row itself (some tables may have row-level click handling)
            await row.ClickAsync();
        }
        
        // Wait for navigation to detail page
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
    }

    /// <summary>
    /// Wait for a table row containing the provided text to become visible.
    /// Useful after creating or searching for records that may take a moment to appear.
    /// </summary>
    public static async Task WaitForTableRowAsync(IPage page, string rowText, int timeoutMs = 15000)
    {
        ILocator row = page.Locator("tr").Filter(new() { HasText = rowText }).First;

        try
        {
            await row.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
        }
        catch (TimeoutException)
        {
            System.Console.WriteLine($"[TestHelpers] Timed out waiting for row '{rowText}' on {page.Url}");
            throw;
        }
    }

    /// <summary>
    /// Generate a random email address for testing.
    /// </summary>
    public static string GenerateRandomEmail()
    {
        return $"test-{Guid.NewGuid():N}@test.com";
    }

    /// <summary>
    /// Generate a random display name for testing.
    /// </summary>
    public static string GenerateRandomName()
    {
        return $"Test User {Guid.NewGuid():N}";
    }

    /// <summary>
    /// Take a screenshot on test failure for debugging.
    /// </summary>
    /// <param name="page">Playwright page</param>
    /// <param name="testName">Name of the test for the screenshot file</param>
    public static async Task TakeScreenshotOnFailureAsync(IPage page, string testName)
    {
        string filename = $"failure-{testName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.png";
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = filename, FullPage = true });
        Console.WriteLine($"Screenshot saved: {filename}");
    }

    /// <summary>
    /// Wait for a toast notification to appear.
    /// </summary>
    /// <param name="page">Playwright page</param>
    /// <param name="expectedText">Expected text in the toast (optional)</param>
    public static async Task WaitForToastAsync(IPage page, string? expectedText = null)
    {
        // Toast notifications typically have role="status" or a specific class
        ILocator toast = expectedText != null
            ? page.Locator("[role='status']").Filter(new() { HasText = expectedText })
            : page.Locator("[role='status']");
        
        await toast.WaitForAsync(new() { Timeout = 5000 });
        
        // Give user time to see it (optional)
        await Task.Delay(500);
    }

    /// <summary>
    /// Click a specific action button in a table row.
    /// </summary>
    /// <param name="page">Playwright page</param>
    /// <param name="rowText">Text to identify the row</param>
    /// <param name="actionName">Name of the action button (e.g., "Edit", "Delete")</param>
    public static async Task ClickRowActionAsync(IPage page, string rowText, string actionName)
    {
        ILocator row = page.Locator("tr").Filter(new() { HasText = rowText });
        ILocator actionButton = row.GetByRole(AriaRole.Button, new() { Name = actionName });
        await actionButton.ClickAsync();
        
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
    }

    /// <summary>
    /// Navigate to the next page in pagination.
    /// </summary>
    public static async Task ClickNextPageAsync(IPage page)
    {
        ILocator nextButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Next|>") });
        await nextButton.ClickAsync();
        await WaitForDataTableAsync(page);
    }

    /// <summary>
    /// Navigate to the previous page in pagination.
    /// </summary>
    public static async Task ClickPreviousPageAsync(IPage page)
    {
        ILocator prevButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Previous|<") });
        await prevButton.ClickAsync();
        await WaitForDataTableAsync(page);
    }

    /// <summary>
    /// Select an option from a dropdown/combobox.
    /// </summary>
    /// <param name="page">Playwright page</param>
    /// <param name="label">Label of the dropdown</param>
    /// <param name="optionText">Text of the option to select</param>
    public static async Task SelectDropdownOptionAsync(IPage page, string label, string optionText)
    {
        // Click the dropdown trigger
        ILocator dropdown = page.GetByLabel(label);
        await dropdown.ClickAsync();
        
        // Wait for options to appear
        await Task.Delay(300);
        
        // Click the option
        ILocator option = page.GetByRole(AriaRole.Option, new() { Name = optionText });
        await option.ClickAsync();
    }

    /// <summary>
    /// Click a tab by name.
    /// </summary>
    /// <param name="page">Playwright page</param>
    /// <param name="tabName">Name of the tab</param>
    public static async Task ClickTabAsync(IPage page, string tabName)
    {
        ILocator tab = page.GetByRole(AriaRole.Tab, new() { Name = tabName });
        await tab.ClickAsync();
        
        // Wait for tab content to load
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
    }

    /// <summary>
    /// Wait for a detail page to finish loading its content.
    /// Detail pages typically show a LoadingSpinner while fetching data.
    /// This method waits for network to settle and for the loading spinner to disappear.
    /// </summary>
    public static async Task WaitForDetailPageAsync(IPage page, int timeoutMs = 10000, string? expectedHeadingText = null)
    {
        // Wait for network to settle (data fetch should complete)
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = timeoutMs });
        
        // Wait for any loading spinners to disappear (Loader2 with animate-spin class)
        try
        {
            ILocator spinner = page.Locator(".animate-spin");
            await spinner.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = timeoutMs });
        }
        catch
        {
            // Spinner might not exist or already hidden, that's fine
        }
        
        // Additional small delay to allow React to complete re-renders
        await Task.Delay(200);

        if (!string.IsNullOrWhiteSpace(expectedHeadingText))
        {
            ILocator heading = page.GetByRole(AriaRole.Heading, new() { Name = expectedHeadingText, Level = 1 });
            await heading.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeoutMs });
        }
    }

    /// <summary>
    /// Verify that an element is visible with a custom message.
    /// Waits for the element to become visible before asserting.
    /// </summary>
    public static async Task ShouldBeVisibleAsync(this ILocator locator, string? message = null, int timeout = 10000)
    {
        try
        {
            // Wait for the element to become visible before checking
            await locator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });
        }
        catch (System.TimeoutException)
        {
            // If timeout, continue to assertion which will produce a better error message
        }
        
        bool isVisible = await locator.IsVisibleAsync();
        isVisible.ShouldBeTrue(message ?? "Element should be visible");
    }

    /// <summary>
    /// Verify that an element is not visible.
    /// </summary>
    public static async Task ShouldNotBeVisibleAsync(this ILocator locator, string? message = null)
    {
        bool isVisible = await locator.IsVisibleAsync();
        isVisible.ShouldBeFalse(message ?? "Element should not be visible");
    }

    /// <summary>
    /// Verify that an element contains specific text.
    /// </summary>
    public static async Task ShouldContainTextAsync(this ILocator locator, string expectedText, string? message = null)
    {
        string? actualText = await locator.TextContentAsync();
        actualText.ShouldNotBeNull("Element should have text content");
        actualText.ShouldContain(expectedText);
    }
}
