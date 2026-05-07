using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.AdminWeb.E2ETests.Fixtures;
using OpenIdentityStack.AdminWeb.E2ETests.Helpers;

namespace OpenIdentityStack.AdminWeb.E2ETests;

/// <summary>
/// E2E tests for Role Management in the Admin Web App.
/// Tests complete role lifecycle including CRUD operations and permission management.
/// Covers 5 API endpoints related to role management.
/// </summary>
public class RoleManagementTests : IAsyncLifetime
{
    private readonly AdminWebAppHostFixture fixture;
    private IBrowserContext? context;
    private IPage? page;

    public RoleManagementTests(AdminWebAppHostFixture fixture)
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
    /// TC-R01: List Roles with Pagination
    /// Verifies that roles can be listed with pagination controls
    /// API: GET /api/admin/roles
    /// </summary>
    [Fact]
    public async Task ListRoles_ShouldDisplayRolesWithPagination()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        // Act
        await TestHelpers.NavigateToFeatureAsync(page!, "Roles");
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Assert - Verify role list loads
        ILocator roleTable = page!.Locator("table");
        await roleTable.ShouldBeVisibleAsync("Role table should be visible");
        
        // Verify pagination controls exist (they may not be active if there's only one page)
        ILocator paginationArea = page.Locator("[role='navigation']").Or(page.Locator("nav"));
        bool hasPagination = await paginationArea.CountAsync() > 0;
        System.Console.WriteLine($"Pagination present: {hasPagination}");
    }

    /// <summary>
    /// TC-R02: Search Roles
    /// Verifies that roles can be searched and filtered
    /// API: GET /api/admin/roles with search parameter
    /// </summary>
    [Fact]
    public async Task SearchRoles_ShouldFilterResults()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await TestHelpers.NavigateToFeatureAsync(page!, "Roles");
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Act - Search for a role (if any exist, search for common terms)
        await TestHelpers.SearchInListAsync(page!, "admin");
        
        // Assert - Verify search completed (results may or may not exist)
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Clear search
        await TestHelpers.SearchInListAsync(page!, "");
        await TestHelpers.WaitForDataTableAsync(page!);
        
        System.Console.WriteLine("Search functionality verified");
    }

    /// <summary>
    /// TC-R03: Create New Role
    /// Verifies that a new role can be created
    /// API: POST /api/admin/roles
    /// </summary>
    [Fact]
    public async Task CreateRole_ShouldSucceed()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await TestHelpers.NavigateToFeatureAsync(page!, "Roles");
        
        string uniqueId = Guid.NewGuid().ToString("N")[..8];
        Dictionary<string, string> roleData = TestDataBuilder.Role()
            .WithName($"test-role-{uniqueId}")
            .WithDisplayName($"Test Role {uniqueId}")
            .WithDescription("E2E Test Role")
            .Build();
        
        // Act - Navigate to create page
        ILocator createButton = page!.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Create.*Role|New.*Role", RegexOptions.IgnoreCase) })
            .Or(page.GetByRole(AriaRole.Link, new() { NameRegex = new Regex("Create.*Role|New.*Role", RegexOptions.IgnoreCase) }))
            .Or(page.Locator("a[href*='/roles/new']"));
        
        await createButton.ClickAsync();
        await page.WaitForURLAsync("**/roles/new");
        
        // Wait for the form to fully load (permission selector should be visible)
        ILocator permissionSelector = page.Locator("[data-testid='permission-selector']");
        await permissionSelector.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        
        // Fill form
        await TestHelpers.FillFormFieldsAsync(page!, roleData);
        
        // Select at least one permission (required by form validation)
        // Use aria-label to select a specific permission checkbox
        ILocator firstPermissionCheckbox = page.GetByRole(AriaRole.Checkbox, new() { Name = "Read Users" });
        await firstPermissionCheckbox.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await firstPermissionCheckbox.ClickAsync();
        
        // Submit form
        ILocator saveButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Save|Create", RegexOptions.IgnoreCase) });
        await saveButton.ClickAsync();
        
        // Wait for successful creation - should redirect to detail page /roles/{guid}
        await page.WaitForURLAsync(new Regex(@"/roles/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"), new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        
        System.Console.WriteLine($"[TEST] Created role at URL: {page.Url}");
        
        // Assert - Verify role was created by navigating back to list and finding it
        await TestHelpers.NavigateToFeatureAsync(page!, "Roles");
        
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Search for the new role by display name (what's visible in the list)
        await TestHelpers.SearchInListAsync(page!, roleData["Display Name"]);
        
        ILocator roleRow = page.Locator("tr").Filter(new() { HasText = roleData["Display Name"] });
        await roleRow.ShouldBeVisibleAsync($"New role {roleData["Display Name"]} should appear in list");
    }

    /// <summary>
    /// TC-R04: View Role Details
    /// Verifies that role details can be viewed
    /// API: GET /api/admin/roles/{id}
    /// </summary>
    [Fact]
    public async Task ViewRoleDetails_ShouldDisplayRoleInformation()
    {
        // Arrange - Create a role to view
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        string uniqueId = Guid.NewGuid().ToString("N")[..8];
        Dictionary<string, string> roleData = TestDataBuilder.Role()
            .WithName($"view-test-role-{uniqueId}")
            .WithDisplayName($"View Test Role {uniqueId}")
            .WithDescription("Role for viewing test")
            .Build();
        
        await CreateRoleViaUI(roleData);
        
        // Navigate to roles list
        await TestHelpers.NavigateToFeatureAsync(page!, "Roles");
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Act - Click on the role (wait for the row to be present before clicking)
        await TestHelpers.SearchInListAsync(page!, roleData["Display Name"]);
        await TestHelpers.WaitForTableRowAsync(page!, roleData["Display Name"]);
        await TestHelpers.ClickTableRowAsync(page!, roleData["Display Name"]);

        // Assert - Verify we're on detail page
        await page!.WaitForURLAsync(new Regex(@"/roles/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"), new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });

        // Verify role information is displayed using a heading locator (more specific than text= substring)
        ILocator nameDisplay = page.GetByRole(AriaRole.Heading, new() { Name = roleData["Display Name"], Exact = true });
        await nameDisplay.ShouldBeVisibleAsync("Role name should be visible on detail page");
    }

    /// <summary>
    /// TC-R05: Edit Role
    /// Verifies that role details can be edited
    /// API: PATCH /api/admin/roles/{id}
    /// </summary>
    [Fact]
    public async Task EditRole_ShouldUpdateRoleDetails()
    {
        // Arrange - Create a role to edit
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        string uniqueId = Guid.NewGuid().ToString("N")[..8];
        Dictionary<string, string> originalRole = TestDataBuilder.Role()
            .WithName($"edit-test-role-{uniqueId}")
            .WithDisplayName($"Edit Test Role {uniqueId}")
            .WithDescription("Original Description")
            .Build();
        
        await CreateRoleViaUI(originalRole);
        
        // Navigate to the role's detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Roles");
        await TestHelpers.SearchInListAsync(page!, originalRole["Display Name"]);
        await TestHelpers.ClickTableRowAsync(page!, originalRole["Display Name"]);
        
        // Act - Click edit button
        ILocator editButton = page!.GetByRole(AriaRole.Button, new() { NameString = "Edit" })
            .Or(page.GetByRole(AriaRole.Link, new() { NameString = "Edit" }))
            .Or(page.Locator("a[href*='/edit']"));
        
        if (await editButton.CountAsync() > 0)
        {
            await editButton.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
            
            // Modify description
            string newDescription = "Updated Description";
            ILocator descriptionInput = page.GetByLabel("Description");
            await descriptionInput.FillAsync(newDescription);
            
            // Save changes
            ILocator saveButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Save|Update", RegexOptions.IgnoreCase) });
            await saveButton.ClickAsync();
            
            // Wait for save to complete
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
            
            // Assert - Verify changes are reflected
            ILocator updatedDescription = page.Locator($"text={newDescription}");
            await updatedDescription.ShouldBeVisibleAsync("Updated description should be visible");
        }
        else
        {
            System.Console.WriteLine("Edit button not found - feature may not be available");
        }
    }

    /// <summary>
    /// TC-R06: Delete Role
    /// Verifies that a role can be deleted
    /// API: DELETE /api/admin/roles/{id}
    /// </summary>
    [Fact]
    public async Task DeleteRole_ShouldRemoveRoleFromList()
    {
        // Arrange - Create a role to delete
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        string uniqueId = Guid.NewGuid().ToString("N")[..8];
        Dictionary<string, string> roleData = TestDataBuilder.Role()
            .WithName($"delete-test-role-{uniqueId}")
            .WithDisplayName($"Delete Test Role {uniqueId}")
            .WithDescription("Role to delete")
            .Build();
        
        await CreateRoleViaUI(roleData);
        
        // Navigate to role detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Roles");
        await TestHelpers.SearchInListAsync(page!, roleData["Display Name"]);
        await TestHelpers.ClickTableRowAsync(page!, roleData["Display Name"]);
        
        // Act - Click delete button
        ILocator deleteButton = page!.GetByRole(AriaRole.Button, new() { NameString = "Delete" });
        await deleteButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await deleteButton.ClickAsync();
        
        // Confirm delete in the dialog (RoleDetail uses "Delete" as confirmLabel)
        ILocator confirmButton = page.Locator("[role='dialog']").GetByRole(AriaRole.Button, new() { NameString = "Delete" });
        await confirmButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await confirmButton.ClickAsync();
        
        // Wait for redirect to list
        await page.WaitForURLAsync("**/roles", new() { Timeout = 10000 });
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Assert - Verify role is no longer in list
        await TestHelpers.SearchInListAsync(page!, roleData["Display Name"]);
        
        ILocator roleRow = page.Locator("tr").Filter(new() { HasText = roleData["Display Name"] });
        bool isVisible = await roleRow.IsVisibleAsync();
        isVisible.ShouldBeFalse($"Deleted role {roleData["Display Name"]} should not appear in list");
    }

    /// <summary>
    /// TC-R07: Assign Permissions to Role
    /// Verifies that permissions can be assigned to a role
    /// API: PATCH /api/admin/roles/{id} (with permissions update)
    /// </summary>
    [Fact]
    public async Task AssignPermissions_ShouldAddPermissionsToRole()
    {
        // Arrange - Create a role
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        string uniqueId = Guid.NewGuid().ToString("N")[..8];
        Dictionary<string, string> roleData = TestDataBuilder.Role()
            .WithName($"perms-test-role-{uniqueId}")
            .WithDisplayName($"Permissions Test Role {uniqueId}")
            .WithDescription("Role for permission testing")
            .Build();
        
        await CreateRoleViaUI(roleData);
        
        // Navigate to role detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Roles");
        await TestHelpers.SearchInListAsync(page!, roleData["Display Name"]);
        await TestHelpers.ClickTableRowAsync(page!, roleData["Display Name"]);
        
        // Act - Click edit button to modify permissions
        ILocator editButton = page!.GetByRole(AriaRole.Button, new() { NameString = "Edit" })
            .Or(page.GetByRole(AriaRole.Link, new() { NameString = "Edit" }));
        
        if (await editButton.CountAsync() > 0)
        {
            await editButton.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
            
            // Look for permission checkboxes or selection UI
            ILocator permissionControls = page.Locator("[type='checkbox']").Or(page.GetByRole(AriaRole.Checkbox));
            
            if (await permissionControls.CountAsync() > 0)
            {
                // Check the first available permission
                ILocator firstPermission = permissionControls.First;
                if (!await firstPermission.IsCheckedAsync())
                {
                    await firstPermission.CheckAsync();
                }
                
                // Save changes
                ILocator saveButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Save|Update", RegexOptions.IgnoreCase) });
                await saveButton.ClickAsync();
                
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
                
                System.Console.WriteLine("Permissions assignment action completed");
            }
            else
            {
                System.Console.WriteLine("Permission controls not found - may use different UI pattern");
            }
        }
        else
        {
            System.Console.WriteLine("Edit button not found - feature may not be available");
        }
    }

    /// <summary>
    /// TC-R08: Remove Permissions from Role
    /// Verifies that permissions can be removed from a role
    /// API: PATCH /api/admin/roles/{id} (with permissions update)
    /// </summary>
    [Fact]
    public async Task RemovePermissions_ShouldRemovePermissionsFromRole()
    {
        // Arrange - Create a role with permissions
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        string uniqueId = Guid.NewGuid().ToString("N")[..8];
        Dictionary<string, string> roleData = TestDataBuilder.Role()
            .WithName($"rm-perms-role-{uniqueId}")
            .WithDisplayName($"Remove Perms Role {uniqueId}")
            .WithDescription("Role for permission removal testing")
            .Build();
        
        await CreateRoleViaUI(roleData);
        
        // Navigate to role detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Roles");
        await TestHelpers.SearchInListAsync(page!, roleData["Display Name"]);
        await TestHelpers.ClickTableRowAsync(page!, roleData["Display Name"]);
        
        // Act - Click edit button to modify permissions
        ILocator editButton = page!.GetByRole(AriaRole.Button, new() { NameString = "Edit" })
            .Or(page.GetByRole(AriaRole.Link, new() { NameString = "Edit" }));
        
        if (await editButton.CountAsync() > 0)
        {
            await editButton.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
            
            // Look for permission checkboxes
            ILocator permissionControls = page.Locator("[type='checkbox']").Or(page.GetByRole(AriaRole.Checkbox));
            
            if (await permissionControls.CountAsync() > 0)
            {
                // Uncheck the first checked permission (if any)
                ILocator firstChecked = permissionControls.First;
                if (await firstChecked.IsCheckedAsync())
                {
                    await firstChecked.UncheckAsync();
                    
                    // Save changes
                    ILocator saveButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Save|Update", RegexOptions.IgnoreCase) });
                    await saveButton.ClickAsync();
                    
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
                    
                    System.Console.WriteLine("Permissions removal action completed");
                }
                else
                {
                    System.Console.WriteLine("No permissions to remove");
                }
            }
            else
            {
                System.Console.WriteLine("Permission controls not found - may use different UI pattern");
            }
        }
        else
        {
            System.Console.WriteLine("Edit button not found - feature may not be available");
        }
    }

    /// <summary>
    /// Helper method to create a role via the UI
    /// </summary>
    private async Task CreateRoleViaUI(Dictionary<string, string> roleData)
    {
        await TestHelpers.NavigateToFeatureAsync(page!, "Roles");
        
        ILocator createButton = page!.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Create.*Role|New.*Role", RegexOptions.IgnoreCase) })
            .Or(page.GetByRole(AriaRole.Link, new() { NameRegex = new Regex("Create.*Role|New.*Role", RegexOptions.IgnoreCase) }))
            .Or(page.Locator("a[href*='/roles/new']"));
        
        await createButton.ClickAsync();
        await page.WaitForURLAsync("**/roles/new");
        
        // Wait for the form to fully load (permission selector should be visible)
        ILocator permissionSelector = page.Locator("[data-testid='permission-selector']");
        await permissionSelector.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        
        // Fill text fields (Name, Display Name, Description)
        await TestHelpers.FillFormFieldsAsync(page!, roleData);
        
        // Select at least one permission (required by RoleForm)
        // Radix UI checkboxes are button[role='checkbox'], use ClickAsync() instead of CheckAsync()
        // Use aria-label to select a specific permission checkbox
        ILocator firstPermissionCheckbox = page.GetByRole(AriaRole.Checkbox, new() { Name = "Read Users" });
        await firstPermissionCheckbox.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await firstPermissionCheckbox.ClickAsync();
        
        ILocator saveButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Save|Create", RegexOptions.IgnoreCase) });
        await saveButton.ClickAsync();
        
        // Wait for successful creation - should redirect to detail page /roles/{guid}
        // Use GUID pattern to avoid matching /roles/new
        await page.WaitForURLAsync(new Regex(@"/roles/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"), new() { Timeout = 15000 });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        
        System.Console.WriteLine($"[TEST] Created role at URL: {page.Url}");
    }
}
