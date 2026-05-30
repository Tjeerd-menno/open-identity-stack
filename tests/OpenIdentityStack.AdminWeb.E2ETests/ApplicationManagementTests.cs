using System.Text.RegularExpressions;
using Microsoft.Playwright;
using OpenIdentityStack.AdminWeb.E2ETests.Fixtures;
using OpenIdentityStack.AdminWeb.E2ETests.Helpers;

namespace OpenIdentityStack.AdminWeb.E2ETests;

/// <summary>
/// E2E coverage for the consolidated Applications aggregate.
/// Focuses on lifecycle, profile policy behavior, and credential management.
/// </summary>
public class ApplicationManagementTests : IAsyncLifetime
{
    private readonly AdminWebAppHostFixture fixture;
    private IBrowserContext? context;
    private IPage? page;

    public ApplicationManagementTests(AdminWebAppHostFixture fixture)
    {
        this.fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        context = await fixture.CreateBrowserContextAsync();
        page = await context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (page is not null) await page.CloseAsync();
        if (context is not null) await context.CloseAsync();
    }

    [Fact]
    public async Task ApplicationsList_ShouldRenderSearchAndCreateActions()
    {
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await TestHelpers.NavigateToFeatureAsync(page!, "Applications");
        await TestHelpers.WaitForDataTableAsync(page!);

        await page!.GetByRole(AriaRole.Heading, new() { Name = "Applications", Exact = true })
            .ShouldBeVisibleAsync("Applications heading should be visible");
        await page.GetByPlaceholder("Search by client ID or display name...")
            .ShouldBeVisibleAsync("Applications search input should be visible");
        await page.GetByRole(AriaRole.Button, new() { Name = "New Application" })
            .ShouldBeVisibleAsync("New Application button should be visible");

        Task<IResponse> searchResponseTask = WaitForApplicationsSearchResponseAsync("e2e");

        await page.GetByPlaceholder("Search by client ID or display name...").FillAsync("e2e");
        IResponse searchResponse = await searchResponseTask;
        searchResponse.Ok.ShouldBeTrue();
        searchResponse.Url.ShouldContain("/api/admin/applications");
        searchResponse.Url.ShouldNotContain("/api/admin/service-accounts");
    }

    [Fact]
    public async Task CreateMachineToMachineApplication_ShouldRailroadPolicyAndShowDetail()
    {
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        (string applicationId, string clientId, string displayName) = await CreateMachineToMachineApplicationAsync();

        page!.Url.ShouldContain($"/applications/{applicationId}");
        await page.GetByRole(AriaRole.Heading, new() { Name = displayName, Exact = true })
            .ShouldBeVisibleAsync("Application detail heading should be visible");
        await page.GetByText(clientId).ShouldBeVisibleAsync("Client ID should be visible on detail page");
        await page.Locator("[data-status='Active']").ShouldBeVisibleAsync("Application should start Active");
    }

    [Fact]
    public async Task ApplicationLifecycle_ShouldDisableAndEnableFromDetail()
    {
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await CreateMachineToMachineApplicationAsync();

        await page!.GetByRole(AriaRole.Button, new() { Name = "Disable", Exact = true }).ClickAsync();
        await page.Locator("[data-status='Disabled']").ShouldBeVisibleAsync("Application should be disabled");

        await page.GetByRole(AriaRole.Button, new() { Name = "Enable", Exact = true }).ClickAsync();
        await page.Locator("[data-status='Active']").ShouldBeVisibleAsync("Application should be re-enabled");
    }

    [Fact(Skip = "Backend delete operation has concurrency issue: NpgsqlOperationInProgressException when deleting projected OpenIddict application")]
    public async Task ApplicationDetail_ShouldDeleteApplicationAndRemoveItFromList()
    {
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        (_, string clientId, _) = await CreateMachineToMachineApplicationAsync();

        // Click delete button and wait for dialog
        await page!.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
        
        ILocator deleteDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Delete Application", Exact = true });
        await deleteDialog.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15000 });
        
        // Click confirm and wait for navigation to applications list
        ILocator confirmButton = deleteDialog.GetByRole(AriaRole.Button, new() { Name = "Confirm", Exact = true });
        await Task.WhenAll(
            page.WaitForURLAsync(new Regex(@"/applications/?$"), new() { Timeout = 20000 }),
            confirmButton.ClickAsync());

        // Verify application is deleted by searching
        Task<IResponse> searchResponseTask = WaitForApplicationsSearchResponseAsync(clientId);
        await page.GetByPlaceholder("Search by client ID or display name...").FillAsync(clientId);
        IResponse searchResponse = await searchResponseTask;
        searchResponse.Ok.ShouldBeTrue();
        await page.GetByText("No applications found")
            .ShouldBeVisibleAsync("Deleted application should not appear in list");
    }

    [Fact]
    public async Task ApplicationCredentials_ShouldAddSecretAndCertificateAndAllowRevocation()
    {
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await CreateMachineToMachineApplicationAsync();

        // Add secret and verify one-time display.
        await page!.GetByRole(AriaRole.Button, new() { Name = "Add Secret", Exact = true }).ClickAsync();
        ILocator addSecretDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Add Secret", Exact = true });
        await addSecretDialog.GetByLabel("Description").FillAsync("Primary secret");
        await addSecretDialog.GetByRole(AriaRole.Button, new() { Name = "Add Secret", Exact = true }).ClickAsync();
        await addSecretDialog.GetByText("Client secret created")
            .ShouldBeVisibleAsync("One-time secret result should be visible");
        await addSecretDialog.GetByText("You will not be able to see this secret again. Store it securely now.")
            .ShouldBeVisibleAsync("One-time secret warning should be visible");
        await addSecretDialog.GetByRole(AriaRole.Button, new() { Name = "Done", Exact = true }).ClickAsync();

        // Add certificate and revoke it.
        string thumbprint = $"THUMBPRINT-{Guid.NewGuid():N}";
        await page.GetByRole(AriaRole.Button, new() { Name = "Add Certificate", Exact = true }).ClickAsync();
        ILocator addCertificateDialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Add Certificate", Exact = true });
        await addCertificateDialog.GetByLabel("Thumbprint").FillAsync(thumbprint);
        await addCertificateDialog.GetByLabel("Subject").FillAsync("CN=E2E App Cert");
        await addCertificateDialog.GetByLabel("Description").FillAsync("E2E certificate");
        await addCertificateDialog.GetByRole(AriaRole.Button, new() { Name = "Add Certificate", Exact = true }).ClickAsync();

        await page.GetByText(thumbprint).ShouldBeVisibleAsync("Certificate thumbprint should be listed");

        ILocator revokeButton = page.GetByRole(AriaRole.Button, new() { Name = "Revoke", Exact = true }).First;
        await revokeButton.ClickAsync();
        await page.GetByText("Revoked").ShouldBeVisibleAsync("Revoked marker should be shown");
    }

    [Fact]
    public async Task SinglePageApplication_ShouldBlockCredentialManagementAsPublicClient()
    {
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        await CreateSinglePageApplicationAsync();

        await page!.GetByText("Public applications cannot use client secrets or certificates.")
            .ShouldBeVisibleAsync("Public profile credential restriction should be visible");
    }

    [Fact]
    public async Task CreateApplication_WithDuplicateClientId_ShouldShowValidationError()
    {
        await TestHelpers.LoginAsTestAdminAsync(page!, fixture.AdminWebUrl!);
        (_, string clientId, _) = await CreateMachineToMachineApplicationAsync();

        await TestHelpers.NavigateToFeatureAsync(page!, "Applications");
        await page!.GetByRole(AriaRole.Button, new() { Name = "New Application" }).ClickAsync();
        await page.WaitForURLAsync("**/applications/new");

        await page.GetByLabel("Client ID").FillAsync(clientId);
        await page.GetByLabel("Display Name").FillAsync($"Duplicate {DateTime.UtcNow:HHmmssfff}");
        await SelectApplicationProfileAsync("Machine-to-machine");
        await CheckScopeAsync("api");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create Application", Exact = true }).ClickAsync();

        await page.GetByText(new Regex("already exists|conflict", RegexOptions.IgnoreCase))
            .ShouldBeVisibleAsync("Duplicate clientId should surface a validation error");
    }

    private async Task<(string applicationId, string clientId, string displayName)> CreateMachineToMachineApplicationAsync()
    {
        string clientId = $"e2e-m2m-{Guid.NewGuid():N}";
        string displayName = $"E2E M2M {DateTime.UtcNow:HHmmssfff}";
        var requestUrls = new List<string>();
        page!.Request += (_, request) =>
        {
            requestUrls.Add(request.Url);
        };

        await TestHelpers.NavigateToFeatureAsync(page!, "Applications");
        await page!.GetByRole(AriaRole.Button, new() { Name = "New Application" }).ClickAsync();
        await page.WaitForURLAsync("**/applications/new");

        await page.GetByLabel("Client ID").FillAsync(clientId);
        await page.GetByLabel("Display Name").FillAsync(displayName);
        await SelectApplicationProfileAsync("Machine-to-machine");

        await page.GetByText("Machine-to-machine applications use only the client credentials grant. Redirects, PKCE, and consent do not apply.")
            .ShouldBeVisibleAsync("Machine-to-machine guidance should be visible");
        (await page.GetByRole(AriaRole.Heading, new() { Name = "Redirect URIs", Exact = true }).CountAsync())
            .ShouldBe(0);
        await page.GetByText("client_credentials").ShouldBeVisibleAsync("Grant defaults should include client_credentials");
        await CheckScopeAsync("api");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create Application", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/applications/[0-9a-fA-F-]+$"), new() { Timeout = 15000 });
        requestUrls.Any((url) => url.Contains("/api/admin/applications", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
        requestUrls.Any((url) => url.Contains("/api/admin/service-accounts", StringComparison.OrdinalIgnoreCase)).ShouldBeFalse();

        string applicationId = GetApplicationIdFromUrl(page.Url);
        return (applicationId, clientId, displayName);
    }

    private async Task<(string applicationId, string clientId)> CreateSinglePageApplicationAsync()
    {
        string clientId = $"e2e-spa-{Guid.NewGuid():N}";
        string displayName = $"E2E SPA {DateTime.UtcNow:HHmmssfff}";

        await TestHelpers.NavigateToFeatureAsync(page!, "Applications");
        await page!.GetByRole(AriaRole.Button, new() { Name = "New Application" }).ClickAsync();
        await page.WaitForURLAsync("**/applications/new");

        await page.GetByLabel("Client ID").FillAsync(clientId);
        await page.GetByLabel("Display Name").FillAsync(displayName);
        await SelectApplicationProfileAsync("Single Page");

        await page.GetByText("Browser applications are public clients. Shared secrets and certificates stay hidden, and PKCE is always required.")
            .ShouldBeVisibleAsync("Single Page profile guidance should be visible");
        await page.GetByPlaceholder("https://example.com/callback")
            .First
            .FillAsync("https://spa.example.com/callback");
        await CheckScopeAsync("openid");
        await page.GetByRole(AriaRole.Button, new() { Name = "Create Application", Exact = true }).ClickAsync();
        await page.WaitForURLAsync(new Regex(@"/applications/[0-9a-fA-F-]+$"), new() { Timeout = 15000 });

        string applicationId = GetApplicationIdFromUrl(page.Url);
        return (applicationId, clientId);
    }

    private async Task SelectApplicationProfileAsync(string profileLabel)
    {
        await page!.GetByRole(AriaRole.Combobox).First.ClickAsync();
        await page.GetByRole(AriaRole.Option, new() { Name = profileLabel, Exact = true }).ClickAsync();
    }

    private Task<IResponse> WaitForApplicationsSearchResponseAsync(string searchTerm)
    {
        string encodedSearchTerm = Uri.EscapeDataString(searchTerm);
        return page!.WaitForResponseAsync((response) =>
            response.Url.Contains("/api/admin/applications", StringComparison.OrdinalIgnoreCase) &&
            response.Url.Contains($"search={encodedSearchTerm}", StringComparison.OrdinalIgnoreCase));
    }

    private async Task CheckScopeAsync(string scopeName)
    {
        ILocator scopeCheckbox = page!.GetByRole(AriaRole.Checkbox, new() { Name = scopeName, Exact = true });
        if (await scopeCheckbox.GetAttributeAsync("data-state") != "checked")
        {
            await scopeCheckbox.ClickAsync();
        }

        (await scopeCheckbox.GetAttributeAsync("data-state")).ShouldBe("checked");
    }

    private static string GetApplicationIdFromUrl(string url)
    {
        var uri = new Uri(url);
        return uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
    }
}
