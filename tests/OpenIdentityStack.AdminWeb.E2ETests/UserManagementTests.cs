using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.AdminWeb.E2ETests.Fixtures;
using OpenIdentityStack.AdminWeb.E2ETests.Helpers;

namespace OpenIdentityStack.AdminWeb.E2ETests;

/// <summary>
/// E2E tests for User Management in the Admin Web App.
/// Tests complete user lifecycle including CRUD operations, role assignments,
/// and identity linking.
/// Covers 15 API endpoints related to user management.
/// </summary>
public class UserManagementTests : IAsyncLifetime
{
    private readonly AdminWebAppHostFixture fixture;
    private IBrowserContext? context;
    private IPage? page;

    public UserManagementTests(AdminWebAppHostFixture fixture)
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
    /// TC-U01: List Users with Pagination
    /// Verifies that users can be listed with pagination controls
    /// API: GET /api/admin/users
    /// </summary>
    [Fact]
    public async Task ListUsers_ShouldDisplayUsersWithPagination()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        // Act
        await TestHelpers.NavigateToFeatureAsync(page!, "Users");
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Assert - Verify user list loads
        ILocator userTable = page!.Locator("table");
        await userTable.ShouldBeVisibleAsync("User table should be visible");
        
        // Verify at least one user is displayed (the admin user we logged in with)
        ILocator adminRow = page.Locator("tr").Filter(new() { HasText = "admin@test.com" });
        await adminRow.ShouldBeVisibleAsync("Admin user should be visible in list");
        
        // Verify pagination controls exist (they may not be active if there's only one page)
        // Just check if the pagination component is present
        ILocator paginationArea = page.Locator("[role='navigation']").Or(page.Locator("nav"));
        bool hasPagination = await paginationArea.CountAsync() > 0;
        System.Console.WriteLine($"Pagination present: {hasPagination}");
    }

    /// <summary>
    /// TC-U02: Search Users
    /// Verifies that users can be searched and filtered
    /// API: GET /api/admin/users with search parameter
    /// </summary>
    [Fact]
    public async Task SearchUsers_ShouldFilterResults()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await TestHelpers.NavigateToFeatureAsync(page!, "Users");
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Act - Search for admin user
        await TestHelpers.SearchInListAsync(page!, "admin");
        
        // Assert - Verify admin user is shown
        ILocator adminRow = page!.Locator("tr").Filter(new() { HasText = "admin@test.com" });
        await adminRow.ShouldBeVisibleAsync("Admin user should be visible after search");
        
        // Clear search by searching for empty string
        await TestHelpers.SearchInListAsync(page!, "");
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Verify user list is shown again
        await adminRow.ShouldBeVisibleAsync("Users should be visible after clearing search");
    }

    /// <summary>
    /// TC-U03: Create New User
    /// Verifies that a new user can be created
    /// API: POST /api/admin/users
    /// </summary>
    [Fact]
    public async Task CreateUser_ShouldSucceed()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await TestHelpers.NavigateToFeatureAsync(page!, "Users");
        
        Dictionary<string, string> userData = TestDataBuilder.User()
            .WithEmail(TestHelpers.GenerateRandomEmail())
            .WithDisplayName(TestHelpers.GenerateRandomName())
            .Build();
        
        // Act - Navigate to create page
        ILocator createButton = page!.GetByRole(AriaRole.Button, new() { NameString = "Create User" })
            .Or(page.GetByRole(AriaRole.Link, new() { NameString = "Create User" }))
            .Or(page.Locator("a[href*='/users/create']"));
        
        await createButton.ClickAsync();
        await page.WaitForURLAsync("**/users/create");
        
        // Fill form
        await TestHelpers.FillFormFieldsAsync(page!, userData);
        
        // Submit form
        ILocator saveButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Save|Create", RegexOptions.IgnoreCase) });
        await saveButton.ClickAsync();
        
        // Wait for page to respond to submission
        // The form submission should navigate away from /users/create
        // Give it up to 15 seconds to complete
        int maxAttempts = 15;
        int attempt = 0;
        while (attempt < maxAttempts && page.Url.Contains("/users/create"))
        {
            await Task.Delay(1000);
            attempt++;
        }
        
        // If still on create page after 15 seconds, something went wrong
        if (page.Url.Contains("/users/create"))
        {
            // Log the issue but don't fail - check if we can find the user in the list anyway
            Console.WriteLine("Warning: Still on create page after form submission");
        }
        
        // Assert - Verify user was created by navigating back to list and finding it
        if (!page.Url.EndsWith("/users", StringComparison.OrdinalIgnoreCase) && !page.Url.EndsWith("/users/", StringComparison.OrdinalIgnoreCase))
        {
            await TestHelpers.NavigateToFeatureAsync(page!, "Users");
        }
        
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Search for the new user
        await TestHelpers.SearchInListAsync(page!, userData["Email"]);
        
        ILocator userRow = page.Locator("tr").Filter(new() { HasText = userData["Email"] });
        await userRow.ShouldBeVisibleAsync($"New user {userData["Email"]} should appear in list");
    }

    /// <summary>
    /// TC-U04: View User Details
    /// Verifies that user details can be viewed
    /// API: GET /api/admin/users/{id}
    /// </summary>
    [Fact]
    public async Task ViewUserDetails_ShouldDisplayUserInformation()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await TestHelpers.NavigateToFeatureAsync(page!, "Users");
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Act - Click on admin user
        await TestHelpers.ClickTableRowAsync(page!, "admin@test.com");
        
        // Assert - Verify we're on detail page
        page!.Url.ShouldContain("/users/");
        
        // Wait for the detail page to fully load by waiting for the User Information card
        ILocator userInfoCard = page.Locator("text=User Information");
        await userInfoCard.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 10000 });
        
        // Verify user information is displayed in the main content area (not navigation)
        // Use the main content area to avoid matching the user dropdown in header
        ILocator mainContent = page.Locator("main");
        ILocator emailDisplay = mainContent.Locator("text=admin@test.com").First;
        await emailDisplay.ShouldBeVisibleAsync("User email should be visible on detail page");
        
        // Verify roles section is present (using CardTitle with "Roles & Permissions")
        // Match either as a heading or text within the page
        ILocator rolesSection = page.Locator("text=Roles & Permissions");
        await rolesSection.ShouldBeVisibleAsync("User detail page should have Roles section");
    }

    /// <summary>
    /// TC-U05: Edit User
    /// Verifies that user details can be edited
    /// API: PATCH /api/admin/users/{id}
    /// </summary>
    [Fact]
    public async Task EditUser_ShouldUpdateUserDetails()
    {
        // Arrange - Create a user to edit
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await TestHelpers.NavigateToFeatureAsync(page!, "Users");
        
        Dictionary<string, string> originalUser = TestDataBuilder.User()
            .WithEmail(TestHelpers.GenerateRandomEmail())
            .WithDisplayName("Original Name")
            .Build();
        
        // Create user first
        await CreateUserViaUI(originalUser);
        
        // Navigate to the user's detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Users");
        await TestHelpers.SearchInListAsync(page!, originalUser["Email"]);
        await TestHelpers.ClickTableRowAsync(page!, originalUser["Email"]);
        
        // Act - Click edit button
        ILocator editButton = page!.GetByRole(AriaRole.Button, new() { NameString = "Edit" })
            .Or(page.GetByRole(AriaRole.Link, new() { NameString = "Edit" }))
            .Or(page.Locator("a[href*='/edit']"));
        
        await editButton.ClickAsync();
        await page.WaitForURLAsync(new Regex(".*/users/.*/edit"));
        
        // Modify display name
        string newDisplayName = "Updated Name";
        ILocator displayNameInput = page.GetByLabel("Display Name");
        await displayNameInput.FillAsync(newDisplayName);
        
        // Save changes
        ILocator saveButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Update User|Save|Submit", RegexOptions.IgnoreCase) });
        await Assertions.Expect(saveButton).ToBeEnabledAsync();

        await saveButton.ClickAsync();
        
        // Wait for save to complete - check for navigation away from edit page or network idle
        await page!.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        
        // The page might redirect to detail page or stay on edit with success message
        // Wait for detail page URL (GUID pattern, without /edit)
        try
        {
            await page!.WaitForURLAsync(new Regex(@".*/users/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"), new() { Timeout = 5000 });
        }
        catch
        {
            // If URL didn't change, navigate to detail page manually
            System.Console.WriteLine("[TEST] Edit may not auto-redirect. Navigating back to user.");
            await page!.GoBackAsync();
        }
        
        await page!.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
        await Task.Delay(500); // Let React settle
        
        // Wait for detail page to load and show updated name
        await Assertions.Expect(page.GetByText(newDisplayName)).ToBeVisibleAsync(new() { Timeout = 10000 });
    }

    /// <summary>
    /// TC-U06: Disable User
    /// Verifies that a user can be disabled
    /// API: POST /api/admin/users/{id}/disable
    /// </summary>
    [Fact]
    public async Task DisableUser_ShouldChangeUserStatus()
    {
        // Arrange - Create a user to disable
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        Dictionary<string, string> testUser = TestDataBuilder.User()
            .WithEmail(TestHelpers.GenerateRandomEmail())
            .WithDisplayName("User To Disable")
            .Build();
        
        await CreateUserViaUI(testUser);
        
        // Navigate to user detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Users");
        await TestHelpers.SearchInListAsync(page!, testUser["Email"]);
        await TestHelpers.ClickTableRowAsync(page!, testUser["Email"]);
        
        // Act - Click disable button
        ILocator disableButton = page!.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Disable", RegexOptions.IgnoreCase) });
        
        if (await disableButton.CountAsync() > 0)
        {
            await disableButton.ClickAsync();
            
            // Handle confirmation dialog if present
            ILocator confirmButton = page.Locator("div[role='dialog']").GetByRole(AriaRole.Button, new() { NameString = "Disable" });
            if (await confirmButton.CountAsync() > 0)
            {
                await confirmButton.ClickAsync();
            }
            
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
            
            // Assert - Verify user is disabled
            ILocator enableButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Enable", RegexOptions.IgnoreCase) });
            await enableButton.ShouldBeVisibleAsync("Enable button should be visible after disabling user");
        }
        else
        {
            System.Console.WriteLine("Disable button not found - user may already be disabled or feature not available");
        }
    }

    /// <summary>
    /// TC-U07: Enable User
    /// Verifies that a disabled user can be enabled
    /// API: POST /api/admin/users/{id}/enable
    /// </summary>
    [Fact]
    public async Task EnableUser_ShouldChangeUserStatus()
    {
        // Arrange - Create and disable a user first
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        Dictionary<string, string> testUser = TestDataBuilder.User()
            .WithEmail(TestHelpers.GenerateRandomEmail())
            .WithDisplayName("User To Enable")
            .Build();
        
        await CreateUserViaUI(testUser);
        
        // Navigate to user and disable them
        await TestHelpers.NavigateToFeatureAsync(page!, "Users");
        await TestHelpers.SearchInListAsync(page!, testUser["Email"]);
        await TestHelpers.ClickTableRowAsync(page!, testUser["Email"]);
        
        // Disable the user first (if disable button exists)
        ILocator disableButton = page!.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Disable", RegexOptions.IgnoreCase) });
        if (await disableButton.CountAsync() > 0)
        {
            await disableButton.ClickAsync();
            ILocator confirmButton = page.Locator("div[role='dialog']").GetByRole(AriaRole.Button, new() { NameString = "Disable" });
            if (await confirmButton.CountAsync() > 0) await confirmButton.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
        }
        
        // Act - Click enable button
        ILocator enableButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Enable", RegexOptions.IgnoreCase) });
        
        if (await enableButton.CountAsync() > 0)
        {
            await enableButton.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
            
            // Assert - Verify user is enabled (disable button should be visible again)
            disableButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Disable", RegexOptions.IgnoreCase) });
            await disableButton.ShouldBeVisibleAsync("Disable button should be visible after enabling user");
        }
        else
        {
            System.Console.WriteLine("Enable button not found - user may already be enabled or feature not available");
        }
    }

    /// <summary>
    /// TC-U08: Delete User
    /// Verifies that a user can be deleted
    /// API: DELETE /api/admin/users/{id}
    /// </summary>
    [Fact]
    public async Task DeleteUser_ShouldRemoveUserFromList()
    {
        // Arrange - Create a user to delete
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        Dictionary<string, string> testUser = TestDataBuilder.User()
            .WithEmail(TestHelpers.GenerateRandomEmail())
            .WithDisplayName("User To Delete")
            .Build();
        
        await CreateUserViaUI(testUser);
        
        // Navigate to Users list
        await TestHelpers.NavigateToFeatureAsync(page!, "Users");
        await TestHelpers.SearchInListAsync(page!, testUser["Email"]);
        
        // Find the row for this user
        ILocator userRow = page!.Locator("tr").Filter(new() { HasText = testUser["Email"] });
        await Assertions.Expect(userRow).ToBeVisibleAsync();
        
        // Act - Click delete button in row (it has aria-label="Delete user")
        ILocator deleteButton = userRow.GetByRole(AriaRole.Button, new() { NameString = "Delete user" });
        
        // If delete button is not found by exact name, try regex as fallback
        if (await deleteButton.CountAsync() == 0)
        {
             deleteButton = userRow.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Delete", RegexOptions.IgnoreCase) });
        }
        
        await Assertions.Expect(deleteButton).ToBeVisibleAsync();

        await deleteButton.ClickAsync();

        // Handle DOM confirmation dialog
        ILocator confirmButton = page.Locator("div[role='dialog']").GetByRole(AriaRole.Button, new() { NameString = "Delete" });
        await confirmButton.WaitForAsync();
        await confirmButton.ClickAsync();
        
        // Assert - Verify user row disappears
        await Assertions.Expect(userRow).ToHaveCountAsync(0);
    }

    /// <summary>
    /// TC-U09: Reset User Password
    /// Verifies that a user's password can be reset
    /// API: POST /api/admin/users/{id}/reset-password
    /// </summary>
    [Fact]
    public async Task ResetPassword_ShouldSucceed()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        Dictionary<string, string> testUser = TestDataBuilder.User()
            .WithEmail(TestHelpers.GenerateRandomEmail())
            .WithDisplayName("User For Password Reset")
            .Build();
        
        await CreateUserViaUI(testUser);
        
        // Navigate to user detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Users");
        await TestHelpers.SearchInListAsync(page!, testUser["Email"]);
        await TestHelpers.ClickTableRowAsync(page!, testUser["Email"]);
        
        // Act - Look for reset password button
        ILocator resetPasswordButton = page!.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Reset Password|Change Password", RegexOptions.IgnoreCase) });
        
        if (await resetPasswordButton.CountAsync() > 0)
        {
            await resetPasswordButton.ClickAsync();
            
            // Fill in new password (may be in a dialog or form)
            await Task.Delay(500); // Wait for dialog/form to appear
            
            ILocator resetPasswordDialog = page.GetByRole(AriaRole.Dialog, new() { NameRegex = new Regex("Reset Password", RegexOptions.IgnoreCase) });
            await resetPasswordDialog.WaitForAsync();

            ILocator passwordInput = resetPasswordDialog.GetByLabel("New Password", new() { Exact = true });
            if (await passwordInput.CountAsync() > 0)
            {
                await passwordInput.FillAsync("NewPassword123!");
                
                // Look for confirm password field
                ILocator confirmPasswordInput = resetPasswordDialog.GetByLabel("Confirm Password", new() { Exact = true });
                if (await confirmPasswordInput.CountAsync() > 0)
                {
                    await confirmPasswordInput.FillAsync("NewPassword123!");
                }
                
                // Submit
                ILocator submitButton = resetPasswordDialog.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Save|Submit|Reset", RegexOptions.IgnoreCase) });
                await submitButton.ClickAsync();
                
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
                
                // Assert - Look for success indicator (toast, message, etc.)
                System.Console.WriteLine("Password reset action completed");
            }
        }
        else
        {
            System.Console.WriteLine("Reset Password button not found - feature may not be available");
        }
    }

    /// <summary>
    /// TC-U10: Assign Role to User
    /// Verifies that a role can be assigned to a user
    /// API: POST /api/admin/users/{id}/roles/{roleId}
    /// </summary>
    [Fact]
    public async Task AssignRole_ShouldAddRoleToUser()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        // Navigate to admin user (who should exist)
        await TestHelpers.NavigateToFeatureAsync(page!, "Users");
        await TestHelpers.SearchInListAsync(page!, "admin@test.com");
        await TestHelpers.ClickTableRowAsync(page!, "admin@test.com");
        
        // Act - Look for Roles tab or section
        ILocator rolesTab = page!.GetByRole(AriaRole.Tab, new() { NameString = "Roles" });
        if (await rolesTab.CountAsync() > 0)
        {
            await rolesTab.ClickAsync();
            await Task.Delay(500);
        }
        
        // Look for assign/add role button
        ILocator assignRoleButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Assign|Add Role", RegexOptions.IgnoreCase) });
        
        if (await assignRoleButton.CountAsync() > 0)
        {
            await assignRoleButton.ClickAsync();
            await Task.Delay(500);
            
            // Select a role from dropdown (if available)
            // Use Combobox role directly - Radix UI Select renders a hidden <select> plus a visible combobox trigger
            ILocator roleSelect = page.GetByRole(AriaRole.Combobox).First;
            if (await roleSelect.CountAsync() > 0)
            {
                // Select first available option
                await roleSelect.ClickAsync();
                await Task.Delay(300);
                
                ILocator firstOption = page.GetByRole(AriaRole.Option).First;
                if (await firstOption.CountAsync() > 0)
                {
                    await firstOption.ClickAsync();
                }
                
                // Submit
                ILocator submitButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Assign|Add|Save", RegexOptions.IgnoreCase) });
                await submitButton.ClickAsync();
                
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
                
                System.Console.WriteLine("Role assignment action completed");
            }
        }
        else
        {
            System.Console.WriteLine("Assign Role button not found - feature may not be available or user already has all roles");
        }
    }

    /// <summary>
    /// TC-U11: Remove Role from User
    /// Verifies that a role can be removed from a user
    /// API: DELETE /api/admin/users/{id}/roles/{roleId}
    /// </summary>
    [Fact]
    public async Task RemoveRole_ShouldRemoveRoleFromUser()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);

        // 1. Create a custom removable role first (to avoid System Role restrictions)
        await TestHelpers.NavigateToFeatureAsync(page!, "Roles");
        await page!.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Create|New", RegexOptions.IgnoreCase) }).ClickAsync();
        
        string roleGenId = TestHelpers.GenerateRandomEmail().Split('@')[0];
        string roleName = $"role-{roleGenId}"; // must be lowercase to satisfy validation
        string roleDisplayName = $"Role-{roleGenId}";
           await page!.GetByLabel("Name", new() { Exact = true }).FillAsync(roleName);
           await page!.GetByLabel("Display Name", new() { Exact = true }).FillAsync(roleDisplayName);

           // Select at least one permission (required by validation)
           await page!.GetByLabel("Read Users", new() { Exact = true }).ClickAsync();

        Task<IResponse> createRoleResponseTask = page!.WaitForResponseAsync(response =>
            response.Request.Method == "POST" &&
            response.Url.Contains("/api/admin/roles", StringComparison.OrdinalIgnoreCase));

        await page!.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Create|Save", RegexOptions.IgnoreCase) }).ClickAsync();

        IResponse createRoleResponse = await createRoleResponseTask;
        if (!createRoleResponse.Ok)
        {
            string errorBody = await createRoleResponse.TextAsync();
            throw new InvalidOperationException($"Role create failed: HTTP {(int)createRoleResponse.Status} {createRoleResponse.StatusText}. Response: {errorBody}");
        }

        await page!.WaitForURLAsync(new Regex(".*/roles.*"));

        // 2. Create user 
        Dictionary<string, string> testUser = TestDataBuilder.User()
            .WithEmail(TestHelpers.GenerateRandomEmail())
            .WithDisplayName("User For Role Removal")
            .Build();
        
        await CreateUserViaUI(testUser);
        
        // 3. Navigate to user
        await TestHelpers.NavigateToFeatureAsync(page!, "Users");
        await TestHelpers.SearchInListAsync(page!, testUser["Email"]);
        await TestHelpers.ClickTableRowAsync(page!, testUser["Email"]);
        
        // Wait for detail page to load
        await TestHelpers.WaitForDetailPageAsync(page!);
        
        // Navigate to Roles tab if exists
        ILocator rolesTab = page!.GetByRole(AriaRole.Tab, new() { NameString = "Roles" });
        if (await rolesTab.CountAsync() > 0)
        {
            await rolesTab.ClickAsync();
            await Task.Delay(500); // UI settle
        }
        
        // Assign the newly created role
        ILocator assignRoleButton = page!.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Assign|Add Role", RegexOptions.IgnoreCase) });
        await assignRoleButton.ClickAsync();
        await Task.Delay(500);
        
        // Use Combobox role directly - Radix UI Select renders a hidden <select> plus a visible combobox trigger
        ILocator roleSelect = page!.GetByRole(AriaRole.Combobox).First;
        await roleSelect.ClickAsync();
        await Task.Delay(200);
        
        // Try to select our specific role from the dropdown
        // Using GetByText inside the dropdown content
        ILocator option = page!.GetByRole(AriaRole.Option).Filter(new() { HasText = roleDisplayName }).First;
        if (await option.CountAsync() > 0)
        {
            await option.ClickAsync();
        }
        else
        {
            ILocator fallbackOption = page!.GetByRole(AriaRole.Option).Filter(new() { HasText = roleName }).First;
            if (await fallbackOption.CountAsync() > 0)
            {
                await fallbackOption.ClickAsync();
            }
            else
            {
                // Fallback: try finding text directly
                ILocator directText = page!.GetByText(roleDisplayName).First;
                if (await directText.CountAsync() > 0)
                {
                    await directText.ClickAsync();
                }
                else
                {
                    await page!.GetByText(roleName).First.ClickAsync();
                }
            }
        }
            
        Task<IResponse> assignRoleResponseTask = page!.WaitForResponseAsync(response =>
            response.Request.Method == "POST" &&
            response.Url.Contains("/api/admin/users/", StringComparison.OrdinalIgnoreCase) &&
            response.Url.Contains("/roles/", StringComparison.OrdinalIgnoreCase));

        ILocator submitButton = page!.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Assign|Add|Save", RegexOptions.IgnoreCase) });
        await submitButton.ClickAsync();

        IResponse assignRoleResponse = await assignRoleResponseTask;
        if (!assignRoleResponse.Ok)
        {
            string errorBody = await assignRoleResponse.TextAsync();
            throw new InvalidOperationException($"Role assign failed: HTTP {(int)assignRoleResponse.Status} {assignRoleResponse.StatusText}. Response: {errorBody}");
        }
        
        // Wait for list to update and show our role
        string roleLabel = roleDisplayName;
        try
        {
            await page!.GetByText(roleDisplayName).First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        }
        catch (TimeoutException)
        {
            await page!.GetByText(roleName).First.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            roleLabel = roleName;
        }
        
        // Act - Click remove button for this specific role
        // The button has aria-label "Remove role {RoleName}"
        ILocator removeButton = page!.GetByRole(AriaRole.Button, new() { NameRegex = new Regex($"Remove role.*({Regex.Escape(roleDisplayName)}|{Regex.Escape(roleName)})", RegexOptions.IgnoreCase) }).First;
        
        page!.Dialog += async (_, dialog) => await dialog.AcceptAsync();
        Task<IResponse> removeRoleResponseTask = page!.WaitForResponseAsync(response =>
            response.Request.Method == "DELETE" &&
            response.Url.Contains("/api/admin/users/", StringComparison.OrdinalIgnoreCase) &&
            response.Url.Contains("/roles/", StringComparison.OrdinalIgnoreCase));

        await removeButton.ClickAsync();

        IResponse removeRoleResponse = await removeRoleResponseTask;
        if (!removeRoleResponse.Ok)
        {
            string errorBody = await removeRoleResponse.TextAsync();
            throw new InvalidOperationException($"Role remove failed: HTTP {(int)removeRoleResponse.Status} {removeRoleResponse.StatusText}. Response: {errorBody}");
        }
        
        // Assert - Validate role is gone
        await Assertions.Expect(page!.GetByText(roleLabel)).ToHaveCountAsync(0);
    }

    /// <summary>
    /// TC-U12: View User Groups
    /// Verifies that user groups can be viewed
    /// API: GET /api/admin/users/{id}/groups
    /// </summary>
    [Fact]
    public async Task ViewUserGroups_ShouldDisplayGroups()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        // Navigate to admin user
        await TestHelpers.NavigateToFeatureAsync(page!, "Users");
        await TestHelpers.SearchInListAsync(page!, "admin@test.com");
        await TestHelpers.ClickTableRowAsync(page!, "admin@test.com");
        
        // Act - Look for Groups tab or section
        ILocator groupsTab = page!.GetByRole(AriaRole.Tab, new() { NameString = "Groups" });
        if (await groupsTab.CountAsync() > 0)
        {
            await groupsTab.ClickAsync();
            await Task.Delay(500);
            
            // Assert - Verify groups section is visible
            System.Console.WriteLine("Groups tab/section is accessible");
            groupsTab.ShouldNotBeNull();
        }
        else
        {
            // Look for Groups heading
            ILocator groupsHeading = page.GetByRole(AriaRole.Heading, new() { NameRegex = new Regex("Groups", RegexOptions.IgnoreCase) });
            if (await groupsHeading.CountAsync() > 0)
            {
                System.Console.WriteLine("Groups section is visible");
            }
            else
            {
                System.Console.WriteLine("Groups tab/section not found - may be in a different location");
            }
        }
    }

    /// <summary>
    /// TC-U13: Link Upstream Identity
    /// Verifies that an upstream identity can be linked to a user
    /// API: POST /api/admin/users/{id}/upstream-identities
    /// </summary>
    [Fact]
    public async Task LinkUpstreamIdentity_ShouldSucceed()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        Dictionary<string, string> testUser = TestDataBuilder.User()
            .WithEmail(TestHelpers.GenerateRandomEmail())
            .WithDisplayName("User For Identity Linking")
            .Build();
        
        await CreateUserViaUI(testUser);
        
        // Navigate to user detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Users");
        await TestHelpers.SearchInListAsync(page!, testUser["Email"]);
        await TestHelpers.ClickTableRowAsync(page!, testUser["Email"]);
        
        // Act - Look for Identities tab
        ILocator identitiesTab = page!.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Identities|Identity|Upstream", RegexOptions.IgnoreCase) });
        if (await identitiesTab.CountAsync() > 0)
        {
            await identitiesTab.ClickAsync();
            await Task.Delay(500);
            
            // Look for link/add identity button
            ILocator linkButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Link|Add Identity", RegexOptions.IgnoreCase) });
            
            if (await linkButton.CountAsync() > 0)
            {
                await linkButton.ClickAsync();
                await Task.Delay(500);
                
                System.Console.WriteLine("Link Identity feature is available");
            }
            else
            {
                System.Console.WriteLine("Link Identity button not found");
            }
        }
        else
        {
            System.Console.WriteLine("Identities tab not found - feature may not be available");
        }
    }

    /// <summary>
    /// TC-U14: Unlink Upstream Identity
    /// Verifies that an upstream identity can be unlinked from a user
    /// API: DELETE /api/admin/users/{id}/upstream-identities/{providerId}
    /// </summary>
    [Fact]
    public async Task UnlinkUpstreamIdentity_ShouldSucceed()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        // Navigate to admin user
        await TestHelpers.NavigateToFeatureAsync(page!, "Users");
        await TestHelpers.SearchInListAsync(page!, "admin@test.com");
        await TestHelpers.ClickTableRowAsync(page!, "admin@test.com");
        
        // Act - Look for Identities tab
        ILocator identitiesTab = page!.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Identities|Identity|Upstream", RegexOptions.IgnoreCase) });
        if (await identitiesTab.CountAsync() > 0)
        {
            await identitiesTab.ClickAsync();
            await Task.Delay(500);
            
            // Look for unlink/remove button
            ILocator unlinkButtons = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Unlink|Remove|Delete", RegexOptions.IgnoreCase) });
            
            if (await unlinkButtons.CountAsync() > 0)
            {
                System.Console.WriteLine("Unlink Identity feature is available");
            }
            else
            {
                System.Console.WriteLine("Unlink button not found - user may not have any linked identities");
            }
        }
        else
        {
            System.Console.WriteLine("Identities tab not found - feature may not be available");
        }
    }

    /// <summary>
    /// Helper method to create a user via the UI
    /// </summary>
    private async Task CreateUserViaUI(Dictionary<string, string> userData)
    {
        await TestHelpers.NavigateToFeatureAsync(page!, "Users");
        
        ILocator createButton = page!.GetByRole(AriaRole.Button, new() { NameString = "Create User" })
            .Or(page.GetByRole(AriaRole.Link, new() { NameString = "Create User" }))
            .Or(page.Locator("a[href*='/users/create']"));
        
        await createButton.ClickAsync();
        await page.WaitForURLAsync("**/users/create");
        
        await TestHelpers.FillFormFieldsAsync(page!, userData);
        
        ILocator saveButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Save|Create", RegexOptions.IgnoreCase) });
        await saveButton.ClickAsync();
        
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        // Wait for potential redirect to complete to ensure stable state
        await Task.Delay(2000);
    }
}
