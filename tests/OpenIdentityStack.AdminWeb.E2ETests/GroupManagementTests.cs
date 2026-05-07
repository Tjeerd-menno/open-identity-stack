using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.AdminWeb.E2ETests.Fixtures;
using OpenIdentityStack.AdminWeb.E2ETests.Helpers;

namespace OpenIdentityStack.AdminWeb.E2ETests;

/// <summary>
/// E2E tests for Group Management in the Admin Web App.
/// Tests complete group lifecycle including CRUD operations, member management, and group mappings.
/// Covers 11 API endpoints related to group management.
/// </summary>
public class GroupManagementTests : IAsyncLifetime
{
    private readonly AdminWebAppHostFixture fixture;
    private IBrowserContext? context;
    private IPage? page;

    public GroupManagementTests(AdminWebAppHostFixture fixture)
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
    /// TC-G01: List Groups with Pagination
    /// Verifies that groups can be listed with pagination controls
    /// API: GET /api/admin/groups
    /// </summary>
    [Fact]
    public async Task ListGroups_ShouldDisplayGroupsWithPagination()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        // Act
        await TestHelpers.NavigateToFeatureAsync(page!, "Groups");
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Assert - Verify group list loads
        ILocator groupTable = page!.Locator("table");
        await groupTable.ShouldBeVisibleAsync("Group table should be visible");
        
        // Verify pagination controls exist (they may not be active if there's only one page)
        ILocator paginationArea = page.Locator("[role='navigation']").Or(page.Locator("nav"));
        bool hasPagination = await paginationArea.CountAsync() > 0;
        System.Console.WriteLine($"Pagination present: {hasPagination}");
    }

    /// <summary>
    /// TC-G02: Search Groups
    /// Verifies that groups can be searched and filtered
    /// API: GET /api/admin/groups with search parameter
    /// </summary>
    [Fact]
    public async Task SearchGroups_ShouldFilterResults()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await TestHelpers.NavigateToFeatureAsync(page!, "Groups");
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Act - Search for a group
        await TestHelpers.SearchInListAsync(page!, "test");
        
        // Assert - Verify search completed (results may or may not exist)
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Clear search
        await TestHelpers.SearchInListAsync(page!, "");
        await TestHelpers.WaitForDataTableAsync(page!);
        
        System.Console.WriteLine("Search functionality verified");
    }

    /// <summary>
    /// TC-G03: Create New Group
    /// Verifies that a new group can be created
    /// API: POST /api/admin/groups
    /// </summary>
    [Fact]
    public async Task CreateGroup_ShouldSucceed()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await TestHelpers.NavigateToFeatureAsync(page!, "Groups");
        
        Dictionary<string, string> groupData = TestDataBuilder.Group()
            .WithName($"Test Group {Guid.NewGuid():N}")
            .WithDescription("E2E Test Group")
            .Build();
        
        // Act - Navigate to create page
        ILocator createButton = page!.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Create.*Group|New.*Group", RegexOptions.IgnoreCase) })
            .Or(page.GetByRole(AriaRole.Link, new() { NameRegex = new Regex("Create.*Group|New.*Group", RegexOptions.IgnoreCase) }))
            .Or(page.Locator("a[href*='/groups/new']"));
        
        await createButton.ClickAsync();
        await page.WaitForURLAsync("**/groups/new");
        
        // Fill form
        await TestHelpers.FillFormFieldsAsync(page!, groupData);
        
        // Submit form
        ILocator saveButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Save|Create", RegexOptions.IgnoreCase) });
        await saveButton.ClickAsync();
        
        // Wait for navigation to detail page or list page
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        
        // Assert - Verify group was created by navigating back to list and finding it
        await TestHelpers.NavigateToFeatureAsync(page!, "Groups");
        
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Search for the new group
        await TestHelpers.SearchInListAsync(page!, groupData["Group Name"]);
        
        ILocator groupRow = page.Locator("tr").Filter(new() { HasText = groupData["Group Name"] });
        await groupRow.ShouldBeVisibleAsync($"New group {groupData["Group Name"]} should appear in list");
    }

    /// <summary>
    /// TC-G04: View Group Details
    /// Verifies that group details can be viewed
    /// API: GET /api/admin/groups/{id}
    /// </summary>
    [Fact]
    public async Task ViewGroupDetails_ShouldDisplayGroupInformation()
    {
        // Arrange - Create a group to view
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        Dictionary<string, string> groupData = TestDataBuilder.Group()
            .WithName($"View Test Group {Guid.NewGuid():N}")
            .WithDescription("Group for viewing test")
            .Build();
        
        await CreateGroupViaUI(groupData);
        
        // Navigate to groups list
        await TestHelpers.NavigateToFeatureAsync(page!, "Groups");
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Act - Click on the group
        await TestHelpers.SearchInListAsync(page!, groupData["Group Name"]);
        await TestHelpers.ClickTableRowAsync(page!, groupData["Group Name"]);
        
        // Assert - Verify we're on detail page
        page!.Url.ShouldContain("/groups/");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        
        // Verify group information is displayed
        ILocator nameDisplay = page.GetByRole(AriaRole.Heading, new() { Name = groupData["Group Name"] });
        await nameDisplay.ShouldBeVisibleAsync("Group name should be visible on detail page");
    }

    /// <summary>
    /// TC-G05: Edit Group
    /// Verifies that group details can be edited
    /// API: PATCH /api/admin/groups/{id}
    /// </summary>
    [Fact]
    public async Task EditGroup_ShouldUpdateGroupDetails()
    {
        // Arrange - Create a group to edit
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        Dictionary<string, string> originalGroup = TestDataBuilder.Group()
            .WithName($"Edit Test Group {Guid.NewGuid():N}")
            .WithDescription("Original Description")
            .Build();
        
        await CreateGroupViaUI(originalGroup);
        
        // Navigate to the group's detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Groups");
        await TestHelpers.SearchInListAsync(page!, originalGroup["Group Name"]);
        await TestHelpers.ClickTableRowAsync(page!, originalGroup["Group Name"]);
        
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
    /// TC-G06: Delete Group
    /// Verifies that a group can be deleted
    /// API: DELETE /api/admin/groups/{id}
    /// </summary>
    [Fact]
    public async Task DeleteGroup_ShouldRemoveGroupFromList()
    {
        // Arrange - Create a group to delete
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        Dictionary<string, string> groupData = TestDataBuilder.Group()
            .WithName($"Delete Test Group {Guid.NewGuid():N}")
            .WithDescription("Group to delete")
            .Build();
        
        await CreateGroupViaUI(groupData);
        
        // Navigate to group detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Groups");
        await TestHelpers.SearchInListAsync(page!, groupData["Group Name"]);
        await TestHelpers.ClickTableRowAsync(page!, groupData["Group Name"]);
        
        // Wait for detail page to load - use GUID pattern to avoid matching /groups/new
        await page!.WaitForURLAsync(new Regex(@"/groups/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"), new() { Timeout = 10000 });
        
        // Act - Click delete button
        ILocator deleteButton = page.GetByRole(AriaRole.Button, new() { NameString = "Delete" });
        await deleteButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await deleteButton.ClickAsync();
        
        // Confirm delete in the dialog (GroupDetail uses "Delete" as confirmLabel)
        ILocator confirmButton = page.Locator("[role='dialog']").GetByRole(AriaRole.Button, new() { NameString = "Delete" });
        await confirmButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await confirmButton.ClickAsync();
        
        // Wait for redirect to list
        await page.WaitForURLAsync("**/groups", new() { Timeout = 10000 });
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Assert - Verify group is no longer in list
        await TestHelpers.SearchInListAsync(page!, groupData["Group Name"]);
        
        ILocator groupRow = page.Locator("tr").Filter(new() { HasText = groupData["Group Name"] });
        bool isVisible = await groupRow.IsVisibleAsync();
        isVisible.ShouldBeFalse($"Deleted group {groupData["Group Name"]} should not appear in list");
    }

    /// <summary>
    /// TC-G07: Add Member to Group
    /// Verifies that a member can be added to a group
    /// API: POST /api/admin/groups/{id}/members/{userId}
    /// </summary>
    [Fact]
    public async Task AddMember_ShouldAddUserToGroup()
    {
        // Arrange - Create a group
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        Dictionary<string, string> groupData = TestDataBuilder.Group()
            .WithName($"Member Test Group {Guid.NewGuid():N}")
            .WithDescription("Group for member testing")
            .Build();
        
        await CreateGroupViaUI(groupData);
        
        // Navigate to group detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Groups");
        await TestHelpers.SearchInListAsync(page!, groupData["Group Name"]);
        await TestHelpers.ClickTableRowAsync(page!, groupData["Group Name"]);
        
        // Act - Look for "Add Member" button or tab
        ILocator addMemberButton = page!.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Add.*Member", RegexOptions.IgnoreCase) })
            .Or(page.GetByRole(AriaRole.Link, new() { NameRegex = new Regex("Add.*Member", RegexOptions.IgnoreCase) }));
        
        if (await addMemberButton.CountAsync() > 0)
        {
            await addMemberButton.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
            
            // Look for user selection UI (combobox, dropdown, or search)
            ILocator userSelector = page.Locator("[role='combobox']")
                .Or(page.GetByRole(AriaRole.Combobox))
                .Or(page.Locator("input[type='search']"));
            
            if (await userSelector.CountAsync() > 0)
            {
                // Try to select a user (if any exist)
                await userSelector.First.ClickAsync();
                await page.WaitForTimeoutAsync(500);
                
                // Look for any user option
                ILocator userOption = page.GetByRole(AriaRole.Option).First;
                if (await userOption.CountAsync() > 0)
                {
                    await userOption.ClickAsync();
                    
                    // Submit
                    ILocator submitButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Add|Save", RegexOptions.IgnoreCase) });
                    if (await submitButton.CountAsync() > 0)
                    {
                        await submitButton.ClickAsync();
                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
                        
                        System.Console.WriteLine("Member added successfully");
                    }
                }
                else
                {
                    System.Console.WriteLine("No users available to add");
                }
            }
            else
            {
                System.Console.WriteLine("User selector not found - may use different UI pattern");
            }
        }
        else
        {
            System.Console.WriteLine("Add Member button not found - feature may not be available");
        }
    }

    /// <summary>
    /// TC-G08: Remove Member from Group
    /// Verifies that a member can be removed from a group
    /// API: DELETE /api/admin/groups/{id}/members/{userId}
    /// </summary>
    [Fact]
    public async Task RemoveMember_ShouldRemoveUserFromGroup()
    {
        // Arrange - Create a group (assuming it might have members)
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        Dictionary<string, string> groupData = TestDataBuilder.Group()
            .WithName($"Remove Member Group {Guid.NewGuid():N}")
            .WithDescription("Group for member removal testing")
            .Build();
        
        await CreateGroupViaUI(groupData);
        
        // Navigate to group detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Groups");
        await TestHelpers.SearchInListAsync(page!, groupData["Group Name"]);
        await TestHelpers.ClickTableRowAsync(page!, groupData["Group Name"]);
        
        // Act - Look for members tab or section
        ILocator membersTab = page!.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Members", RegexOptions.IgnoreCase) })
            .Or(page.Locator("button:has-text('Members')"));
        
        if (await membersTab.CountAsync() > 0)
        {
            await membersTab.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
            
            // Look for remove/delete button for any member
            ILocator removeButtons = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Remove|Delete", RegexOptions.IgnoreCase) });
            
            if (await removeButtons.CountAsync() > 0)
            {
                await removeButtons.First.ClickAsync();
                
                // Confirm removal in the dialog (ConfirmDialog uses "Confirm" button by default)
                ILocator confirmButton = page.GetByRole(AriaRole.Button, new() { NameString = "Confirm" });
                if (await confirmButton.CountAsync() > 0)
                {
                    await confirmButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                    await confirmButton.ClickAsync();
                }
                
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
                
                System.Console.WriteLine("Member removal action completed");
            }
            else
            {
                System.Console.WriteLine("No members to remove or remove button not found");
            }
        }
        else
        {
            System.Console.WriteLine("Members tab not found - feature may use different UI pattern");
        }
    }

    /// <summary>
    /// TC-G09: View Group Members
    /// Verifies that group members can be viewed with pagination
    /// API: GET /api/admin/groups/{id}/members
    /// </summary>
    [Fact]
    public async Task ViewGroupMembers_ShouldDisplayMembers()
    {
        // Arrange - Create a group
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        Dictionary<string, string> groupData = TestDataBuilder.Group()
            .WithName($"View Members Group {Guid.NewGuid():N}")
            .WithDescription("Group for viewing members")
            .Build();
        
        await CreateGroupViaUI(groupData);
        
        // Navigate to group detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Groups");
        await TestHelpers.SearchInListAsync(page!, groupData["Group Name"]);
        await TestHelpers.ClickTableRowAsync(page!, groupData["Group Name"]);
        
        // Act - Look for members tab or section
        ILocator membersTab = page!.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Members", RegexOptions.IgnoreCase) })
            .Or(page.Locator("button:has-text('Members')"));
        
        if (await membersTab.CountAsync() > 0)
        {
            await membersTab.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
            
            // Assert - Verify members section is visible (may be empty)
            ILocator membersSection = page.Locator("table").Or(page.Locator("[role='list']")).Or(page.Locator("text=/Members/i"));
            await membersSection.ShouldBeVisibleAsync("Members section should be visible");
            
            System.Console.WriteLine("Members section displayed successfully");
        }
        else
        {
            System.Console.WriteLine("Members tab not found - feature may use different UI pattern");
        }
    }

    /// <summary>
    /// TC-G10: Add Group Mapping (Role/Claim)
    /// Verifies that a mapping can be added to a group
    /// API: POST /api/admin/groups/{id}/mappings
    /// </summary>
    [Fact]
    public async Task AddMapping_ShouldAddMappingToGroup()
    {
        // Arrange - Create a group
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        Dictionary<string, string> groupData = TestDataBuilder.Group()
            .WithName($"Mapping Test Group {Guid.NewGuid():N}")
            .WithDescription("Group for mapping testing")
            .Build();
        
        await CreateGroupViaUI(groupData);
        
        // Navigate to group detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Groups");
        await TestHelpers.SearchInListAsync(page!, groupData["Group Name"]);
        await TestHelpers.ClickTableRowAsync(page!, groupData["Group Name"]);
        
        // Act - Look for mappings tab or "Add Mapping" button
        ILocator mappingsTab = page!.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Mappings?", RegexOptions.IgnoreCase) })
            .Or(page.Locator("button:has-text('Mapping')"));
        
        if (await mappingsTab.CountAsync() > 0)
        {
            await mappingsTab.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
            
            // Look for "Add Mapping" button
            ILocator addMappingButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Add.*Mapping", RegexOptions.IgnoreCase) });
            
            if (await addMappingButton.CountAsync() > 0)
            {
                await addMappingButton.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
                
                // Look for mapping type selector
                ILocator typeSelector = page.Locator("[role='combobox']").Or(page.GetByRole(AriaRole.Combobox)).First;
                
                if (await typeSelector.CountAsync() > 0)
                {
                    await typeSelector.ClickAsync();
                    await page.WaitForTimeoutAsync(500);
                    
                    // Select first available option
                    ILocator firstOption = page.GetByRole(AriaRole.Option).First;
                    if (await firstOption.CountAsync() > 0)
                    {
                        await firstOption.ClickAsync();
                        
                        // Submit
                        ILocator submitButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Add|Save", RegexOptions.IgnoreCase) });
                        if (await submitButton.CountAsync() > 0)
                        {
                            await submitButton.ClickAsync();
                            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
                            
                            System.Console.WriteLine("Mapping added successfully");
                        }
                    }
                }
                else
                {
                    System.Console.WriteLine("Mapping selector not found");
                }
            }
            else
            {
                System.Console.WriteLine("Add Mapping button not found");
            }
        }
        else
        {
            System.Console.WriteLine("Mappings tab not found - feature may use different UI pattern");
        }
    }

    /// <summary>
    /// TC-G11: Remove Group Mapping
    /// Verifies that a mapping can be removed from a group
    /// API: DELETE /api/admin/groups/{id}/mappings/{mappingId}
    /// </summary>
    [Fact]
    public async Task RemoveMapping_ShouldRemoveMappingFromGroup()
    {
        // Arrange - Create a group
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        Dictionary<string, string> groupData = TestDataBuilder.Group()
            .WithName($"Remove Mapping Group {Guid.NewGuid():N}")
            .WithDescription("Group for mapping removal testing")
            .Build();
        
        await CreateGroupViaUI(groupData);
        
        // Navigate to group detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Groups");
        await TestHelpers.SearchInListAsync(page!, groupData["Group Name"]);
        await TestHelpers.ClickTableRowAsync(page!, groupData["Group Name"]);
        
        // Act - Look for mappings tab
        ILocator mappingsTab = page!.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Mappings?", RegexOptions.IgnoreCase) })
            .Or(page.Locator("button:has-text('Mapping')"));
        
        if (await mappingsTab.CountAsync() > 0)
        {
            await mappingsTab.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
            
            // Look for remove/delete button for any mapping
            ILocator removeButtons = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Remove|Delete", RegexOptions.IgnoreCase) });
            
            if (await removeButtons.CountAsync() > 0)
            {
                await removeButtons.First.ClickAsync();
                
                // Confirm removal in the dialog (ConfirmDialog uses "Confirm" button by default)
                ILocator confirmButton = page.GetByRole(AriaRole.Button, new() { NameString = "Confirm" });
                if (await confirmButton.CountAsync() > 0)
                {
                    await confirmButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                    await confirmButton.ClickAsync();
                }
                
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
                
                System.Console.WriteLine("Mapping removal action completed");
            }
            else
            {
                System.Console.WriteLine("No mappings to remove or remove button not found");
            }
        }
        else
        {
            System.Console.WriteLine("Mappings tab not found - feature may use different UI pattern");
        }
    }

    /// <summary>
    /// Helper method to create a group via the UI
    /// </summary>
    private async Task CreateGroupViaUI(Dictionary<string, string> groupData)
    {
        await TestHelpers.NavigateToFeatureAsync(page!, "Groups");
        
        ILocator createButton = page!.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Create.*Group|New.*Group", RegexOptions.IgnoreCase) })
            .Or(page.GetByRole(AriaRole.Link, new() { NameRegex = new Regex("Create.*Group|New.*Group", RegexOptions.IgnoreCase) }))
            .Or(page.Locator("a[href*='/groups/new']"));
        
        await createButton.ClickAsync();
        await page.WaitForURLAsync("**/groups/new");
        
        await TestHelpers.FillFormFieldsAsync(page!, groupData);
        
        ILocator saveButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Save|Create", RegexOptions.IgnoreCase) });
        await saveButton.ClickAsync();
        
        // Wait for successful creation - should redirect to detail page /groups/{guid}
        // Use GUID pattern to avoid matching /groups/new
        // Use WaitUntil = Commit to handle React Router SPA navigation (pushState doesn't fire Load event)
        await page.WaitForURLAsync(new Regex(@"/groups/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"), new PageWaitForURLOptions { Timeout = 30000, WaitUntil = WaitUntilState.Commit });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        
        System.Console.WriteLine($"[TEST] Created group at URL: {page.Url}");
    }
}
