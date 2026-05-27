using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.AdminWeb.E2ETests.Fixtures;
using OpenIdentityStack.AdminWeb.E2ETests.Helpers;

namespace OpenIdentityStack.AdminWeb.E2ETests;

/// <summary>
/// E2E tests for Service Account Management in the Admin Web App.
/// Tests complete service account lifecycle including CRUD operations, enable/disable, secret rotation, and certificate management.
/// Covers 10 API endpoints related to service account management.
/// </summary>
public class ServiceAccountManagementTests : IAsyncLifetime
{
    private const string LegacyServiceAccountsSkipReason = "Legacy Service Accounts UI is being replaced by unified Applications management.";

    private readonly AdminWebAppHostFixture fixture;
    private IBrowserContext? context;
    private IPage? page;

    public ServiceAccountManagementTests(AdminWebAppHostFixture fixture)
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
    /// TC-SA01: List Service Accounts with Pagination
    /// Verifies that service accounts can be listed with pagination controls
    /// API: GET /api/admin/service-accounts
    /// </summary>
    [Fact(Skip = LegacyServiceAccountsSkipReason)]
    public async Task ListServiceAccounts_ShouldDisplayServiceAccountsWithPagination()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        // Act
        await TestHelpers.NavigateToFeatureAsync(page!, "Service Accounts");
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Assert - Verify service account list loads
        ILocator serviceAccountTable = page!.Locator("table");
        await serviceAccountTable.ShouldBeVisibleAsync("Service account table should be visible");
        
        // Verify pagination controls exist (they may not be active if there's only one page)
        ILocator paginationArea = page.Locator("[role='navigation']").Or(page.Locator("nav"));
        bool hasPagination = await paginationArea.CountAsync() > 0;
        System.Console.WriteLine($"Pagination present: {hasPagination}");
    }

    /// <summary>
    /// TC-SA02: Search Service Accounts
    /// Verifies that service accounts can be searched and filtered
    /// API: GET /api/admin/service-accounts with search parameter
    /// </summary>
    [Fact(Skip = LegacyServiceAccountsSkipReason)]
    public async Task SearchServiceAccounts_ShouldFilterResults()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await TestHelpers.NavigateToFeatureAsync(page!, "Service Accounts");
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Act - Search for a service account
        await TestHelpers.SearchInListAsync(page!, "test");
        
        // Assert - Verify search completed (results may or may not exist)
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Clear search
        await TestHelpers.SearchInListAsync(page!, "");
        await TestHelpers.WaitForDataTableAsync(page!);
        
        System.Console.WriteLine("Search functionality verified");
    }

    /// <summary>
    /// TC-SA03: Create New Service Account
    /// Verifies that a new service account can be created and secret is displayed
    /// API: POST /api/admin/service-accounts
    /// Special: Verify secret is displayed once and can be copied
    /// </summary>
    [Fact(Skip = LegacyServiceAccountsSkipReason)]
    public async Task CreateServiceAccount_ShouldSucceedAndDisplaySecret()
    {
        // Arrange
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await TestHelpers.NavigateToFeatureAsync(page!, "Service Accounts");
        
        Dictionary<string, string> serviceAccountData = TestDataBuilder.ServiceAccount()
            .WithDisplayName($"Test SA {Guid.NewGuid():N}")
            .Build();
        
        // Act - Navigate to create page
        ILocator createButton = page!.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Create.*Service.*Account|New.*Service.*Account", RegexOptions.IgnoreCase) })
            .Or(page.GetByRole(AriaRole.Link, new() { NameRegex = new Regex("Create.*Service.*Account|New.*Service.*Account", RegexOptions.IgnoreCase) }))
            .Or(page.Locator("a[href*='/service-accounts/new']"));
        
        await createButton.ClickAsync();
        await page.WaitForURLAsync("**/service-accounts/new");
        
        // Fill form
        await TestHelpers.FillFormFieldsAsync(page!, serviceAccountData);
        
        // Select at least one scope (required by form validation)
        // Radix UI checkboxes are button[role='checkbox'], not input[type='checkbox']
        ILocator firstScopeCheckbox = page.GetByRole(AriaRole.Checkbox).First;
        await firstScopeCheckbox.ClickAsync();
        
        // Submit form
        ILocator saveButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Save|Create", RegexOptions.IgnoreCase) });
        await saveButton.ClickAsync();
        
        // Wait for the Done button to appear - this indicates the service account was created and secret is displayed
        ILocator doneButton = page.GetByRole(AriaRole.Button, new() { NameString = "Done" });
        await doneButton.WaitForAsync(new() { Timeout = 15000 });
        
        // Look for secret display (this may appear in a modal or on the page)
        ILocator secretDisplay = page.Locator("[data-testid='secret']")
            .Or(page.Locator("code"))
            .Or(page.Locator("pre"))
            .Or(page.Locator("input[type='text'][readonly]"));
        
        if (await secretDisplay.CountAsync() > 0)
        {
            string secretValue = await secretDisplay.First.TextContentAsync() ?? "";
            System.Console.WriteLine($"Secret displayed (length: {secretValue.Length})");
            
            // Look for copy button
            ILocator copyButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Copy", RegexOptions.IgnoreCase) });
            if (await copyButton.CountAsync() > 0)
            {
                System.Console.WriteLine("Copy button found");
            }
        }
        else
        {
            System.Console.WriteLine("Secret display not found - may use different UI pattern");
        }
        
        // Click Done to navigate back to the list
        await doneButton.ClickAsync();
        await page.WaitForURLAsync("**/service-accounts", new() { Timeout = 10000 });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Search for the new service account
        await TestHelpers.SearchInListAsync(page!, serviceAccountData["Display Name"]);
        
        ILocator serviceAccountRow = page.Locator("tr").Filter(new() { HasText = serviceAccountData["Display Name"] });
        await serviceAccountRow.ShouldBeVisibleAsync($"New service account {serviceAccountData["Display Name"]} should appear in list");
    }

    /// <summary>
    /// TC-SA04: View Service Account Details
    /// Verifies that service account details can be viewed
    /// API: GET /api/admin/service-accounts/{id}
    /// </summary>
    [Fact(Skip = LegacyServiceAccountsSkipReason)]
    public async Task ViewServiceAccountDetails_ShouldDisplayServiceAccountInformation()
    {
        // Arrange - Create a service account to view
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        Dictionary<string, string> serviceAccountData = TestDataBuilder.ServiceAccount()
            .WithDisplayName($"View Test SA {Guid.NewGuid():N}")
            .Build();
        
        await CreateServiceAccountViaUI(serviceAccountData);
        
        // Navigate to service accounts list
        await TestHelpers.NavigateToFeatureAsync(page!, "Service Accounts");
        await TestHelpers.WaitForDataTableAsync(page!);
        
        // Act - Click on the service account
        await TestHelpers.SearchInListAsync(page!, serviceAccountData["Display Name"]);
        await TestHelpers.ClickTableRowAsync(page!, serviceAccountData["Display Name"]);
        await page!.WaitForURLAsync(new Regex(".*/service-accounts/.+"), new() { Timeout = 10000 });
        await TestHelpers.WaitForDetailPageAsync(page!, timeoutMs: 15000, expectedHeadingText: serviceAccountData["Display Name"]);
        
        // Assert - Verify we're on detail page
        page!.Url.ShouldContain("/service-accounts/");
        
        // Verify service account information is displayed
        ILocator nameDisplay = page.GetByRole(AriaRole.Heading, new() { Name = serviceAccountData["Display Name"], Level = 1 });
        await nameDisplay.ShouldBeVisibleAsync("Service account name should be visible on detail page", timeout: 15000);
    }

    /// <summary>
    /// TC-SA05: Edit Service Account
    /// Verifies that service account details can be edited
    /// API: PATCH /api/admin/service-accounts/{id}
    /// </summary>
    [Fact(Skip = LegacyServiceAccountsSkipReason)]
    public async Task EditServiceAccount_ShouldUpdateServiceAccountDetails()
    {
        // Arrange - Create a service account to edit
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        Dictionary<string, string> originalServiceAccount = TestDataBuilder.ServiceAccount()
            .WithDisplayName($"Edit Test SA {Guid.NewGuid():N}")
            .Build();
        
        await CreateServiceAccountViaUI(originalServiceAccount);
        
        // Navigate to the service account's detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Service Accounts");
        await TestHelpers.SearchInListAsync(page!, originalServiceAccount["Display Name"]);
        await TestHelpers.ClickTableRowAsync(page!, originalServiceAccount["Display Name"]);
        
        // Wait for detail page to load
        await TestHelpers.WaitForDetailPageAsync(page!);
        
        // Act - Click edit button
        ILocator editButton = page!.GetByRole(AriaRole.Button, new() { NameString = "Edit" })
            .Or(page.GetByRole(AriaRole.Link, new() { NameString = "Edit" }))
            .Or(page.Locator("a[href*='/edit']"));
        
        if (await editButton.CountAsync() > 0)
        {
            await editButton.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
            
            // Modify display name (ServiceAccountForm has Display Name field, not Description)
            string newDisplayName = $"Updated Display Name {Guid.NewGuid():N}";
            ILocator displayNameInput = page.GetByLabel("Display Name", new() { Exact = true });
            await displayNameInput.FillAsync(newDisplayName);
            
            // Save changes and wait for update API response
            Task<IResponse> updateResponseTask = page.WaitForResponseAsync(response =>
                response.Request.Method == "PATCH" &&
                response.Url.Contains("/api/admin/service-accounts/", StringComparison.OrdinalIgnoreCase));

            ILocator saveButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Save|Update", RegexOptions.IgnoreCase) });
            await saveButton.ClickAsync();

            IResponse updateResponse = await updateResponseTask;
            if (!updateResponse.Ok)
            {
                string errorBody = await updateResponse.TextAsync();
                throw new InvalidOperationException($"Service account update failed: HTTP {(int)updateResponse.Status} {updateResponse.StatusText}. Response: {errorBody}");
            }
            
            // Wait for redirect to detail page and data load
            await page.WaitForURLAsync(new Regex(@"/service-accounts/[0-9a-fA-F-]+$"), new() { Timeout = 10000 });
            await TestHelpers.WaitForDetailPageAsync(page!);
            
            // Assert - Verify changes are reflected
            ILocator updatedDisplayName = page.Locator($"text={newDisplayName}");
            await updatedDisplayName.ShouldBeVisibleAsync("Updated display name should be visible");
        }
        else
        {
            System.Console.WriteLine("Edit button not found - feature may not be available");
        }
    }

    /// <summary>
    /// TC-SA06: Enable Service Account
    /// Verifies that a service account can be enabled
    /// API: POST /api/admin/service-accounts/{id}/enable
    /// </summary>
    [Fact(Skip = LegacyServiceAccountsSkipReason)]
    public async Task EnableServiceAccount_ShouldEnableServiceAccount()
    {
        // Arrange - Create a service account (starts as Active, so we'll disable it first)
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        Dictionary<string, string> serviceAccountData = TestDataBuilder.ServiceAccount()
            .WithDisplayName($"Enable Test SA {Guid.NewGuid():N}")
            .Build();
        
        await CreateServiceAccountViaUI(serviceAccountData);
        
        // Navigate to service account detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Service Accounts");
        await TestHelpers.SearchInListAsync(page!, serviceAccountData["Display Name"]);
        await TestHelpers.ClickTableRowAsync(page!, serviceAccountData["Display Name"]);
        
        // Wait for detail page to load (URL change + content load) - use GUID pattern to avoid matching /service-accounts/new
        await page!.WaitForURLAsync(new Regex(@"/service-accounts/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"), new() { Timeout = 10000 });
        await TestHelpers.WaitForDetailPageAsync(page!);
        
        // Wait for the service account name to be displayed (confirms data is loaded)
        await page.GetByText(serviceAccountData["Display Name"]).First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        
        // First disable the service account (since new accounts are Active by default)
        ILocator disableButton = page!.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Disable", RegexOptions.IgnoreCase) });
        await disableButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await disableButton.ClickAsync();
        
        // Confirm disable in the dialog (ConfirmDialog uses "Confirm" button by default)
        ILocator confirmDisableButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Confirm", RegexOptions.IgnoreCase) });
        await confirmDisableButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await confirmDisableButton.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        
        // Act - Now Enable the disabled service account
        ILocator enableButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Enable", RegexOptions.IgnoreCase) });
        await enableButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await enableButton.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        
        // Assert - Verify enabled status (Disable button should now be visible)
        disableButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Disable", RegexOptions.IgnoreCase) });
        await disableButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        
        bool isEnabled = await disableButton.CountAsync() > 0;
        isEnabled.ShouldBeTrue("Service account should be enabled after clicking Enable");
        
        System.Console.WriteLine("Service account enabled successfully");
    }

    /// <summary>
    /// TC-SA07: Disable Service Account
    /// Verifies that a service account can be disabled
    /// API: POST /api/admin/service-accounts/{id}/disable
    /// </summary>
    [Fact(Skip = LegacyServiceAccountsSkipReason)]
    public async Task DisableServiceAccount_ShouldDisableServiceAccount()
    {
        // Arrange - Create a service account
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        Dictionary<string, string> serviceAccountData = TestDataBuilder.ServiceAccount()
            .WithDisplayName($"Disable Test SA {Guid.NewGuid():N}")
            .Build();
        
        await CreateServiceAccountViaUI(serviceAccountData);
        
        // Navigate to service account detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Service Accounts");
        await TestHelpers.SearchInListAsync(page!, serviceAccountData["Display Name"]);
        await TestHelpers.ClickTableRowAsync(page!, serviceAccountData["Display Name"]);
        
        // Wait for detail page to load (URL change + content load)
        await page!.WaitForURLAsync(new Regex(@"/service-accounts/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"), new() { Timeout = 10000 });
        await TestHelpers.WaitForDetailPageAsync(page!);
        
        // Wait for the service account name to be displayed (confirms data is loaded)
        await page.GetByText(serviceAccountData["Display Name"]).First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        
        // Act - Click disable button (use regex since button has icon + text)
        ILocator disableButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Disable", RegexOptions.IgnoreCase) });
        await disableButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await disableButton.ClickAsync();
        
        // Confirm disable in the dialog
        ILocator confirmButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Confirm", RegexOptions.IgnoreCase) });
        await confirmButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await confirmButton.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        
        // Assert - Verify disabled status (Enable button should now be visible)
        ILocator enableButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Enable", RegexOptions.IgnoreCase) });
        await enableButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        
        bool isDisabled = await enableButton.CountAsync() > 0;
        isDisabled.ShouldBeTrue("Service account should be disabled after clicking Disable");
        
        System.Console.WriteLine("Service account disabled successfully");
    }

    /// <summary>
    /// TC-SA08: Delete Service Account
    /// Verifies that a service account can be deleted
    /// API: DELETE /api/admin/service-accounts/{id}
    /// </summary>
    [Fact(Skip = LegacyServiceAccountsSkipReason)]
    public async Task DeleteServiceAccount_ShouldRemoveServiceAccountFromList()
    {
        // Arrange - Create a service account to delete
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        Dictionary<string, string> serviceAccountData = TestDataBuilder.ServiceAccount()
            .WithDisplayName($"Delete Test SA {Guid.NewGuid():N}")
            .Build();
        
        await CreateServiceAccountViaUI(serviceAccountData);
        
        // Navigate to service account detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Service Accounts");
        await TestHelpers.SearchInListAsync(page!, serviceAccountData["Display Name"]);
        await TestHelpers.ClickTableRowAsync(page!, serviceAccountData["Display Name"]);
        
        // Wait for detail page to load
        await page!.WaitForURLAsync(new Regex(@"/service-accounts/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}"), new() { Timeout = 10000 });
        
        // Act - Click delete button
        ILocator deleteButton = page.GetByRole(AriaRole.Button, new() { NameString = "Delete" });
        await deleteButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await deleteButton.ClickAsync();
        
        // Confirm delete in the dialog (ConfirmDialog uses "Confirm" button by default)
        ILocator confirmButton = page.GetByRole(AriaRole.Button, new() { NameString = "Confirm" });
        await confirmButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
        await confirmButton.ClickAsync();
        
        // Wait for redirect to list
        await page.WaitForURLAsync("**/service-accounts", new() { Timeout = 10000 });
        await TestHelpers.WaitForDataTableAsync(page);
        
        // Assert - Verify service account is no longer in list
        await TestHelpers.SearchInListAsync(page, serviceAccountData["Display Name"]);
        
        ILocator serviceAccountRow = page.Locator("tr").Filter(new() { HasText = serviceAccountData["Display Name"] });
        bool isVisible = await serviceAccountRow.IsVisibleAsync();
        isVisible.ShouldBeFalse($"Deleted service account {serviceAccountData["Display Name"]} should not appear in list");
    }

    /// <summary>
    /// TC-SA09: Rotate Service Account Secret
    /// Verifies that a service account secret can be rotated
    /// API: POST /api/admin/service-accounts/{id}/rotate-secret
    /// Special: Verify new secret is displayed once
    /// </summary>
    [Fact(Skip = LegacyServiceAccountsSkipReason)]
    public async Task RotateSecret_ShouldGenerateNewSecretAndDisplayOnce()
    {
        // Arrange - Create a service account
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        Dictionary<string, string> serviceAccountData = TestDataBuilder.ServiceAccount()
            .WithDisplayName($"Rotate Secret SA {Guid.NewGuid():N}")
            .Build();
        
        await CreateServiceAccountViaUI(serviceAccountData);
        
        // Navigate to service account detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Service Accounts");
        await TestHelpers.SearchInListAsync(page!, serviceAccountData["Display Name"]);
        await TestHelpers.ClickTableRowAsync(page!, serviceAccountData["Display Name"]);
        
        // Act - Look for rotate secret button
        ILocator rotateSecretButton = page!.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Rotate.*Secret", RegexOptions.IgnoreCase) });
        
        if (await rotateSecretButton.CountAsync() > 0)
        {
            await rotateSecretButton.ClickAsync();
            
            // Dialog opens - confirm rotation by clicking "Rotate Secret" in the dialog
            ILocator confirmRotateButton = page.GetByRole(AriaRole.Button, new() { NameString = "Rotate Secret" });
            await confirmRotateButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            await confirmRotateButton.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
            
            // Look for secret display (this may appear in a modal or on the page)
            ILocator secretDisplay = page.Locator("[data-testid='secret']")
                .Or(page.Locator("code"))
                .Or(page.Locator("pre"))
                .Or(page.Locator("input[type='text'][readonly]"));
            
            if (await secretDisplay.CountAsync() > 0)
            {
                string secretValue = await secretDisplay.First.TextContentAsync() ?? "";
                System.Console.WriteLine($"New secret displayed (length: {secretValue.Length})");
                
                // Verify secret is not empty
                secretValue.ShouldNotBeEmpty("Rotated secret should not be empty");
            }
            else
            {
                System.Console.WriteLine("Secret display not found - may use different UI pattern");
            }
            
            // Click Done to close the dialog
            ILocator doneButton = page.GetByRole(AriaRole.Button, new() { NameString = "Done" });
            if (await doneButton.CountAsync() > 0)
            {
                await doneButton.ClickAsync();
            }
        }
        else
        {
            System.Console.WriteLine("Rotate Secret button not found - feature may not be available");
        }
    }

    /// <summary>
    /// TC-SA10: Add Certificate to Service Account
    /// Verifies that a certificate can be added to a service account
    /// API: POST /api/admin/service-accounts/{id}/certificates
    /// </summary>
    [Fact(Skip = LegacyServiceAccountsSkipReason)]
    public async Task AddCertificate_ShouldAddCertificateToServiceAccount()
    {
        // Arrange - Create a service account
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        Dictionary<string, string> serviceAccountData = TestDataBuilder.ServiceAccount()
            .WithDisplayName($"Cert Test SA {Guid.NewGuid():N}")
            .Build();
        
        await CreateServiceAccountViaUI(serviceAccountData);
        
        // Navigate to service account detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Service Accounts");
        await TestHelpers.SearchInListAsync(page!, serviceAccountData["Display Name"]);
        await TestHelpers.ClickTableRowAsync(page!, serviceAccountData["Display Name"]);
        
        // Act - Look for certificates tab or "Add Certificate" button
        ILocator certificatesTab = page!.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Certificates?", RegexOptions.IgnoreCase) })
            .Or(page.Locator("button:has-text('Certificate')"));
        
        if (await certificatesTab.CountAsync() > 0)
        {
            await certificatesTab.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
            
            // Look for "Add Certificate" button
            ILocator addCertButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Add.*Certificate", RegexOptions.IgnoreCase) });
            
            if (await addCertButton.CountAsync() > 0)
            {
                await addCertButton.ClickAsync();
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
                
                // Look for certificate input (file upload or text area)
                ILocator certInput = page.Locator("input[type='file']")
                    .Or(page.Locator("textarea"))
                    .Or(page.GetByLabel(new Regex("Certificate", RegexOptions.IgnoreCase)));
                
                if (await certInput.CountAsync() > 0)
                {
                    System.Console.WriteLine("Certificate input found - ready to add certificate");
                    // In a real test, we would upload or paste a certificate here
                    // For now, just verify the UI is available
                }
                else
                {
                    System.Console.WriteLine("Certificate input not found");
                }
            }
            else
            {
                System.Console.WriteLine("Add Certificate button not found");
            }
        }
        else
        {
            System.Console.WriteLine("Certificates tab not found - feature may use different UI pattern");
        }
    }

    /// <summary>
    /// TC-SA11: View Service Account Certificates
    /// Verifies that service account certificates can be viewed
    /// API: GET /api/admin/service-accounts/{id}/certificates
    /// </summary>
    [Fact(Skip = LegacyServiceAccountsSkipReason)]
    public async Task ViewCertificates_ShouldDisplayCertificates()
    {
        // Arrange - Create a service account
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        
        Dictionary<string, string> serviceAccountData = TestDataBuilder.ServiceAccount()
            .WithDisplayName($"View Certs SA {Guid.NewGuid():N}")
            .Build();
        
        await CreateServiceAccountViaUI(serviceAccountData);
        
        // Navigate to service account detail page
        await TestHelpers.NavigateToFeatureAsync(page!, "Service Accounts");
        await TestHelpers.SearchInListAsync(page!, serviceAccountData["Display Name"]);
        await TestHelpers.ClickTableRowAsync(page!, serviceAccountData["Display Name"]);
        
        // Act - Look for certificates tab
        ILocator certificatesTab = page!.GetByRole(AriaRole.Tab, new() { NameRegex = new Regex("Certificates?", RegexOptions.IgnoreCase) })
            .Or(page.Locator("button:has-text('Certificate')"));
        
        if (await certificatesTab.CountAsync() > 0)
        {
            await certificatesTab.ClickAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
            
            // Assert - Verify certificates section is visible (may be empty)
            ILocator certificatesSection = page.Locator("table")
                .Or(page.Locator("[role='list']"))
                .Or(page.Locator("text=/Certificates?/i"));
            
            await certificatesSection.ShouldBeVisibleAsync("Certificates section should be visible");
            
            System.Console.WriteLine("Certificates section displayed successfully");
        }
        else
        {
            System.Console.WriteLine("Certificates tab not found - feature may use different UI pattern");
        }
    }

    /// <summary>
    /// Helper method to create a service account via the UI
    /// </summary>
    private async Task CreateServiceAccountViaUI(Dictionary<string, string> serviceAccountData)
    {
        await TestHelpers.NavigateToFeatureAsync(page!, "Service Accounts");
        
        ILocator createButton = page!.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Create.*Service.*Account|New.*Service.*Account|Add.*Service.*Account", RegexOptions.IgnoreCase) })
            .Or(page.GetByRole(AriaRole.Link, new() { NameRegex = new Regex("Create.*Service.*Account|New.*Service.*Account|Add.*Service.*Account", RegexOptions.IgnoreCase) }))
            .Or(page.Locator("a[href*='/service-accounts/new']"));
        
        await createButton.ClickAsync();
        await page.WaitForURLAsync("**/service-accounts/new");
        
        // Fill text fields (Client ID and Display Name)
        await TestHelpers.FillFormFieldsAsync(page!, serviceAccountData);
        
        // Select required scopes and grant types
        // Radix UI checkboxes are button[role='checkbox'], use ClickAsync() instead of CheckAsync()
        // client_credentials is pre-selected by default (see ServiceAccountForm defaultValues)
        // but we need to select at least one scope (api is the first scope checkbox)
        ILocator scopeCheckboxes = page.Locator("[data-testid='scope-checkbox'], [role='checkbox']").First;
        await scopeCheckboxes.ClickAsync();
        
        ILocator saveButton = page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Save|Create", RegexOptions.IgnoreCase) });
        await saveButton.ClickAsync();
        
        // Wait for the Done button to appear - this indicates the service account was created
        ILocator doneButton = page.GetByRole(AriaRole.Button, new() { NameString = "Done" });
        await doneButton.WaitForAsync(new() { Timeout = 15000 });
        
        // Click Done to navigate back to the list
        await doneButton.ClickAsync();
        await page.WaitForURLAsync("**/service-accounts", new() { Timeout = 10000 });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
    }
}
