# Playwright E2E Tests Integration - Implementation Summary

## Overview
Integrated Playwright E2E tests into the .NET test suite so they run and block CI, addressing the code review comment that the previous test only checked for file existence without executing the actual Playwright tests.

## Changes Made

### 1. New File: `ManagementWebAppHostFixture.cs`
**Location:** `tests/OpenIdentityStack.ManagementWeb.E2ETests/Fixtures/ManagementWebAppHostFixture.cs`

- **Purpose:** Shared Aspire-based fixture for Management Web E2E tests
- **Key Features:**
  - Starts the full Aspire distributed application stack with Management Web enabled
  - Waits for PostgreSQL and API resources to become healthy (10-minute timeout)
  - Gets the dynamically assigned Management Web URL from Aspire
  - Seeds test data: creates test admin user (Ada Lovelace), operator role, and OAuth client
  - Provides HttpClient for API access
  - Properly cleans up resources on disposal
  - Follows the same Aspire fixture pattern used for frontend E2E coverage

### 2. Updated: `ManagementWebE2ETestProjectTests.cs`
**Location:** `tests/OpenIdentityStack.ManagementWeb.E2ETests/ManagementWebE2ETestProjectTests.cs`

- **Kept:** `PlaywrightCoverage_ShouldIncludeUsersAndDualUiSpecs` - verifies spec files exist
- **Added:** `PlaywrightTests_ShouldExecuteAndPass_WithRunningAspireStack` - **NEW BLOCKING TEST**

#### New Test Details:
- **Test Name:** `PlaywrightTests_ShouldExecuteAndPass_WithRunningAspireStack`
- **Execution Flow:**
  1. Creates and initializes `ManagementWebAppHostFixture` to start full Aspire stack
  2. Sets `MANAGEMENT_WEB_BASE_URL` environment variable to running instance
  3. Sets `ADMIN_WEB_BASE_URL` environment variable for dual-UI test (fallback to localhost:5175)
  4. Invokes `npx playwright test` as a subprocess from the test project directory
  5. Captures stdout and stderr
  6. Asserts exit code == 0 (all Playwright tests passed)
  7. On failure, provides comprehensive error message including:
     - Exit code
     - Full stdout from Playwright
     - Full stderr from Playwright
     - Actual Management Web URL
     - Actual Admin Web URL
  8. Properly restores original environment variables
  9. Cleans up Aspire resources

### 3. Updated: `OpenIdentityStack.ManagementWeb.E2ETests.csproj`
**Location:** `tests/OpenIdentityStack.ManagementWeb.E2ETests/OpenIdentityStack.ManagementWeb.E2ETests.csproj`

- **Added Package References:**
  - `Microsoft.EntityFrameworkCore` (required by TestSeeder)
  - `OpenIddict.AspNetCore` (required by TestSeeder)

- **Added Project References:**
  - `OpenIdentityStack.Application` (required by TestSeeder)
  - `OpenIdentityStack.Domain` (required by TestSeeder)
  - `SharedKernel` (required by TestSeeder)

- **Added Linked Source Files:**
  - `AspireTestApplication.cs` (from TestSeedHelpers)
  - `OpenIdentityStackTestSeeder.cs` (from TestSeedHelpers)

## Integration Approach

### ✅ Direct .NET Integration (Implemented)
The solution invokes Playwright from .NET test code by:
1. Starting Aspire infrastructure in-process
2. Running Playwright CLI (`npx playwright test`) as a subprocess
3. Capturing and asserting on exit codes
4. **Advantage:** Tests run in same CI pipeline without separate steps
5. **Advantage:** Test output directly integrated with .NET test results
6. **Advantage:** Failures block the build immediately

### Alternative: Separate CI Step (Not Implemented, but Documented)
If the .NET integration approach had issues, a CI step like this could be added:

```yaml
- name: Run Management Web E2E Tests
  run: |
    cd tests/OpenIdentityStack.ManagementWeb.E2ETests
    npm ci
    MANAGEMENT_WEB_BASE_URL="http://localhost:5176" \
    ADMIN_WEB_BASE_URL="http://localhost:5175" \
    npx playwright test
```

This would be added to `.github/workflows/` and would need to:
- Start the Aspire stack first
- Wait for services to be ready
- Run Playwright with environment variables
- Fail the entire CI if exit code ≠ 0

## Playwright Test Coverage

The following tests are now executed and must pass:

1. **`users.spec.ts`**: Tests Management Web user workflow
   - Navigate to users page
   - Verify UI elements (heading, navigation, description)
   - Click on user (Ada Lovelace/admin)
   - Edit display name
   - Save changes
   - Disable user
   - Assign role

2. **`auth-continuity.spec.ts`**: Tests dual-UI independence
   - Navigate to Management Web
   - Verify Management Web is reachable
   - Navigate to Admin Web
   - Verify Admin Web is reachable
   - Navigate back to Management Web
   - Verify Management Web still works

## How It Works in CI

When `dotnet test` is run (including in CI):

1. The test framework discovers `PlaywrightTests_ShouldExecuteAndPass_WithRunningAspireStack`
2. Test initializes `ManagementWebAppHostFixture`:
   - Aspire starts PostgreSQL, API, and Management Web
   - Test data (user, role, client) gets seeded
   - Services become healthy
3. Test runs `npx playwright test` against the running instance
4. Playwright tests execute the spec files
5. If any Playwright test fails:
   - `npx` exits with code != 0
   - `ShouldBe(0)` assertion fails with detailed error message
   - CI build fails
6. If all Playwright tests pass:
   - Exit code is 0
   - Test passes
   - CI build continues

## Validation Results

✅ **Compilation:** Project builds successfully with no warnings or errors
✅ **Structure:** Follows established frontend E2E fixture patterns
✅ **Dependencies:** All required packages and project references are in place
✅ **Integration:** Test properly starts Aspire, runs Playwright, captures output
✅ **Error Handling:** Comprehensive error messages for debugging failures

## Key Design Decisions

1. **Created separate fixture** instead of using test class constructor for proper resource cleanup
2. **Used Aspire resource discovery** for dynamic URLs instead of hardcoded ports
3. **Comprehensive error messages** that include both stdout/stderr and actual URLs for debugging
4. **Environment variable preservation** to avoid side effects on parallel tests
5. **Fallback for Admin Web URL** when not running to support auth-continuity test
6. **Uses existing TestSeeder** to create realistic test data matching production patterns

## Files Modified/Created

- ✅ Created: `tests/OpenIdentityStack.ManagementWeb.E2ETests/Fixtures/ManagementWebAppHostFixture.cs`
- ✅ Modified: `tests/OpenIdentityStack.ManagementWeb.E2ETests/ManagementWebE2ETestProjectTests.cs`
- ✅ Modified: `tests/OpenIdentityStack.ManagementWeb.E2ETests/OpenIdentityStack.ManagementWeb.E2ETests.csproj`

## CI Impact

**Before:** Only verified spec files exist (file-only check)
**After:** Actually executes Playwright tests against running Aspire stack - **Failures block the build**

The implementation fully addresses the code review comment by ensuring that the new E2E journeys (users and auth-continuity) actually execute and block CI if they fail.
