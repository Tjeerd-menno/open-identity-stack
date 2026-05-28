# OpenIdentityStack.AdminWeb.E2ETests

End-to-End (E2E) tests for the OpenIdentityStack Admin Web App using Playwright and Aspire Testing.

## Overview

This project contains browser-based E2E tests that verify the Admin Web App functionality by running the complete application stack (API, database, and web frontend) in a test environment using .NET Aspire.

### 📋 Documentation
- **[E2E Test Plan](E2E_TEST_PLAN.md)** - Comprehensive test plan with detailed test cases
- **[Extension Summary](E2E_TEST_EXTENSION_SUMMARY.md)** - Executive summary and implementation roadmap
- **[Test Helpers](Helpers/)** - Reusable utilities for test development

## Test Infrastructure

- **Aspire.Hosting.Testing**: Orchestrates the entire application stack for testing
- **Microsoft.Playwright**: Browser automation for E2E testing
- **xUnit v3**: Test framework
- **Shouldly**: Assertion library

## Test Structure

### AdminWebLayoutTests
Tests for the MVP foundation:
- Home page loading
- Sidebar navigation visibility
- Navigation to all admin modules (Users, Roles, Groups, Applications, Sessions, Providers)
- Header UI elements (title, logout button)

### Test Fixture
- `AdminWebAppHostFixture`: Manages the Aspire application lifecycle for tests
  - Starts PostgreSQL, API, and AdminWeb
  - Provides access to API client and AdminWeb URL
  - Ensures clean state for each test run

## Running the Tests

### Prerequisites
- .NET 10 SDK
- Docker (for PostgreSQL container)
- Node.js 22+ (for AdminWeb)
- Playwright Chromium runtime installed for the generated test harness
- Aspire testing parameter `Parameters__default-admin-password` available (set automatically by test helpers/fixture)

### Run Tests
```bash
# From repository root
dotnet test tests/OpenIdentityStack.AdminWeb.E2ETests

# With verbose output
dotnet test tests/OpenIdentityStack.AdminWeb.E2ETests --logger "console;verbosity=detailed"
```

### First Run
Install Playwright Chromium once before running E2E tests:

```bash
# build once to generate the playwright install script
dotnet build tests/OpenIdentityStack.AdminWeb.E2ETests

# Linux/macOS
./tests/OpenIdentityStack.AdminWeb.E2ETests/bin/Debug/net10.0/playwright.sh install chromium

# PowerShell
pwsh tests/OpenIdentityStack.AdminWeb.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium
```

## Writing New E2E Tests

### Test Class Structure
```csharp
public class MyFeatureTests : IClassFixture<AdminWebAppHostFixture>, IAsyncLifetime
{
    private readonly AdminWebAppHostFixture _fixture;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;

    public MyFeatureTests(AdminWebAppHostFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Install and launch browser
        Microsoft.Playwright.Program.Main(["install", "chromium"]);
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        _context = await _browser.NewContextAsync();
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        // Cleanup
        if (_page != null) await _page.CloseAsync();
        if (_context != null) await _context.CloseAsync();
        if (_browser != null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    [Fact]
    public async Task MyTest()
    {
        var url = _fixture.AdminWebUrl!;
        await _page!.GotoAsync(url);
        // Test assertions here
    }
}
```

### Accessing the Application
- **AdminWeb URL**: `_fixture.AdminWebUrl`
- **API Client**: `_fixture.ApiClient` (for setup/teardown via test API)

### Best Practices
1. Use Playwright's `GetByRole` for accessibility-focused selectors
2. Use helper methods from `TestHelpers` class for common operations
3. Use `TestDataBuilder` for generating test data
4. Use `ShouldBeVisibleAsync()` extension for clear assertions
5. Wait for URL changes with `WaitForURLAsync` after navigation
6. Keep tests independent and idempotent
7. Use descriptive test names that explain the scenario
8. Take screenshots on failure for debugging
9. Clean up test data after tests complete

### Available Test Helpers
- **TestHelpers.LoginAsTestAdminAsync()** - Login as the test admin user
- **TestHelpers.NavigateToFeatureAsync()** - Navigate to a feature page
- **TestHelpers.WaitForDataTableAsync()** - Wait for data table to load
- **TestHelpers.FillFormFieldsAsync()** - Fill multiple form fields
- **TestHelpers.SearchInListAsync()** - Search in a list/table
- **TestHelpers.ClickTableRowAsync()** - Click a table row
- **TestHelpers.GenerateRandomEmail()** - Generate test email
- **TestHelpers.GenerateRandomName()** - Generate test name
- And many more... (see Helpers/TestHelpers.cs)

### Test Data Builders
```csharp
// Create test user data
var userData = TestDataBuilder.User()
    .WithEmail("test@example.com")
    .WithDisplayName("Test User")
    .WithPassword("Test123!")
    .Build();

// Create test role data
var roleData = TestDataBuilder.Role()
    .WithName("Test Role")
    .WithDescription("Test description")
    .WithPermissions("users:read", "users:write")
    .Build();
```

## Test Categories

### ✅ Implemented Tests
- **AuthenticationFlowTests** (6 tests) - Login, logout, token refresh, protected routes
- **AdminWebLayoutTests** (8 tests) - Navigation, UI structure, basic rendering

### 📋 Planned Tests (See E2E_TEST_PLAN.md for details)
- **UserManagementTests** (14 tests) - Complete user CRUD, role assignment, identity linking
- **RoleManagementTests** (8 tests) - Role CRUD, permissions management
- **GroupManagementTests** (11 tests) - Group CRUD, member management, mappings
- **ApplicationManagementTests** (new consolidated suite) - Unified application lifecycle, profile policy guardrails, and credential lifecycle
- **SessionManagementTests** (5 tests) - Session viewing and revocation
- **ProviderManagementTests** (8 tests) - OIDC/OAuth2/SAML2 provider configuration
- **DashboardTests** (3 tests) - Dashboard metrics and navigation
- **IntegrationTests** (3 tests) - Cross-feature workflows

**Total**: 14 existing + 63 planned = **77 comprehensive E2E tests**

### API Endpoint Coverage
The test suite covers **50 API endpoints** across all features:
- Users: 15 endpoints
- Roles: 5 endpoints
- Groups: 11 endpoints
- Service Accounts: 10 endpoints
- Sessions: 4 endpoints
- Providers: 5 endpoints

## CI/CD Integration

These tests run in the CI pipeline:
- Headless browser mode
- Retry on failure (2 retries)
- Sequential execution to avoid resource conflicts
- Automatic cleanup of test containers

## Troubleshooting

### Tests timeout waiting for AdminWeb
- Ensure Node.js and npm are installed
- Check that `npm install` completed successfully in AdminWeb
- Increase timeout in `AdminWebAppHostFixture.DefaultTimeout`

### Playwright browser not found
- Run: `pwsh tests/OpenIdentityStack.AdminWeb.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium`
- CI installs Chromium explicitly in `.github/workflows/ci.yml`

### PostgreSQL container issues
- Ensure Docker is running
- Check Docker has sufficient resources
- Verify no port conflicts (5432)
