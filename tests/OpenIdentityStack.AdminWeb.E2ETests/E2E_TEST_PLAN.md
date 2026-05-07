# AdminWeb E2E Test Extension Plan

## Overview

This document outlines a comprehensive plan to extend the E2E test suite for the OpenIdentityStack AdminWeb application. The goal is to achieve complete end-to-end test coverage for all main functions and API endpoints in the AdminWeb application.

## Current Test Coverage

### Existing Tests
1. **AuthenticationFlowTests.cs** - OAuth2/OIDC authentication flows
   - Login flow with PKCE
   - Logout flow
   - Token refresh
   - Protected route access
   - Callback handling

2. **AdminWebLayoutTests.cs** - Navigation and UI structure
   - Home page loading
   - Sidebar navigation
   - Navigation to all module pages
   - Header elements

### Infrastructure
- **AdminWebAppHostFixture** - Aspire-based test fixture
- **Playwright** - Browser automation
- **xUnit v3** - Test framework
- **Shouldly** - Assertions

## Testing Principles

### E2E Philosophy
- ✅ **No Mocking** - All tests interact with real services
- ✅ **Full Stack** - Tests run against complete application stack (DB, API, Frontend)
- ✅ **Browser-Based** - Real user interactions via Playwright
- ✅ **Independent** - Each test can run in isolation
- ✅ **Idempotent** - Tests can be run multiple times safely

### Test Structure Pattern
```csharp
public class FeatureTests : IClassFixture<AdminWebAppHostFixture>, IAsyncLifetime
{
    private readonly AdminWebAppHostFixture fixture;
    private IPlaywright? playwright;
    private IBrowser? browser;
    private IBrowserContext? context;
    private IPage? page;
    
    // Setup/teardown methods
    // Helper methods (login, navigation, etc.)
    // Test methods
}
```

## Detailed Test Plan

---

## 1. User Management E2E Tests

**File**: `UserManagementTests.cs`

### API Endpoints Covered
- `GET /api/admin/users` - List users
- `GET /api/admin/users/{id}` - Get user details
- `POST /api/admin/users` - Create user
- `PATCH /api/admin/users/{id}` - Update user
- `DELETE /api/admin/users/{id}` - Delete user
- `POST /api/admin/users/{id}/disable` - Disable user
- `POST /api/admin/users/{id}/enable` - Enable user
- `POST /api/admin/users/{id}/reset-password` - Reset password
- `GET /api/admin/users/{id}/roles` - Get user roles
- `POST /api/admin/users/{id}/roles/{roleId}` - Assign role
- `DELETE /api/admin/users/{id}/roles/{roleId}` - Unassign role
- `GET /api/admin/users/{id}/groups` - Get user groups
- `GET /api/admin/users/{id}/upstream-identities` - Get linked identities
- `POST /api/admin/users/{id}/upstream-identities` - Link identity
- `DELETE /api/admin/users/{id}/upstream-identities/{providerId}` - Unlink identity

### Test Cases

#### TC-U01: List Users with Pagination
**Steps**:
1. Login as test admin
2. Navigate to /users
3. Verify user list loads
4. Verify pagination controls are visible
5. Click next page
6. Verify different users are displayed

#### TC-U02: Search Users
**Steps**:
1. Login as test admin
2. Navigate to /users
3. Enter search term in search box
4. Verify filtered results match search
5. Clear search
6. Verify all users displayed again

#### TC-U03: Create New User
**Steps**:
1. Login as test admin
2. Navigate to /users
3. Click "Create User" button
4. Fill in user form (email, display name, password)
5. Submit form
6. Verify redirect to user detail page
7. Verify user appears in user list

#### TC-U04: View User Details
**Steps**:
1. Login as test admin
2. Navigate to /users
3. Click on a user
4. Verify user detail page loads
5. Verify user information is displayed correctly
6. Verify tabs for Roles, Groups, Identities are visible

#### TC-U05: Edit User
**Steps**:
1. Login as test admin
2. Navigate to user detail page
3. Click "Edit" button
4. Modify user details
5. Save changes
6. Verify changes are reflected on detail page

#### TC-U06: Disable User
**Steps**:
1. Login as test admin
2. Navigate to active user detail page
3. Click "Disable" button
4. Confirm action in dialog
5. Verify user status changes to "Disabled"
6. Verify "Enable" button is now visible

#### TC-U07: Enable User
**Steps**:
1. Login as test admin
2. Navigate to disabled user detail page
3. Click "Enable" button
4. Confirm action in dialog
5. Verify user status changes to "Active"

#### TC-U08: Delete User
**Steps**:
1. Login as test admin
2. Create a test user
3. Navigate to test user detail page
4. Click "Delete" button
5. Confirm action in dialog
6. Verify redirect to user list
7. Verify user is no longer in list

#### TC-U09: Reset User Password
**Steps**:
1. Login as test admin
2. Navigate to user detail page
3. Click "Reset Password" button
4. Enter new password
5. Submit form
6. Verify success message

#### TC-U10: Assign Role to User
**Steps**:
1. Login as test admin
2. Navigate to user detail page
3. Click on "Roles" tab
4. Click "Assign Role" button
5. Select a role from dropdown
6. Submit
7. Verify role appears in user's role list

#### TC-U11: Remove Role from User
**Steps**:
1. Login as test admin
2. Navigate to user detail page with assigned role
3. Click on "Roles" tab
4. Click "Remove" on a role
5. Confirm action
6. Verify role is removed from list

#### TC-U12: View User Groups
**Steps**:
1. Login as test admin
2. Navigate to user detail page
3. Click on "Groups" tab
4. Verify groups list is displayed

#### TC-U13: Link Upstream Identity
**Steps**:
1. Login as test admin
2. Navigate to user detail page
3. Click on "Identities" tab
4. Click "Link Identity" button
5. Select provider and enter subject
6. Submit
7. Verify identity appears in list

#### TC-U14: Unlink Upstream Identity
**Steps**:
1. Login as test admin
2. Navigate to user with linked identity
3. Click on "Identities" tab
4. Click "Unlink" on an identity
5. Confirm action
6. Verify identity is removed

---

## 2. Role Management E2E Tests

**File**: `RoleManagementTests.cs`

### API Endpoints Covered
- `GET /api/admin/roles` - List roles
- `GET /api/admin/roles/{id}` - Get role details
- `POST /api/admin/roles` - Create role
- `PATCH /api/admin/roles/{id}` - Update role
- `DELETE /api/admin/roles/{id}` - Delete role

### Test Cases

#### TC-R01: List Roles with Pagination
**Steps**:
1. Login as test admin
2. Navigate to /roles
3. Verify role list loads
4. Verify pagination controls
5. Test pagination navigation

#### TC-R02: Search Roles
**Steps**:
1. Login as test admin
2. Navigate to /roles
3. Enter search term
4. Verify filtered results
5. Clear search and verify all roles shown

#### TC-R03: Create New Role
**Steps**:
1. Login as test admin
2. Navigate to /roles
3. Click "Create Role" button
4. Fill in role name and description
5. Select permissions
6. Submit form
7. Verify redirect to role detail page
8. Verify role appears in list

#### TC-R04: View Role Details
**Steps**:
1. Login as test admin
2. Navigate to /roles
3. Click on a role
4. Verify role detail page loads
5. Verify role information and permissions displayed

#### TC-R05: Edit Role
**Steps**:
1. Login as test admin
2. Navigate to role detail page
3. Click "Edit" button
4. Modify role details and permissions
5. Save changes
6. Verify changes are reflected

#### TC-R06: Delete Role
**Steps**:
1. Login as test admin
2. Create a test role
3. Navigate to test role detail page
4. Click "Delete" button
5. Confirm action
6. Verify redirect to role list
7. Verify role is no longer in list

#### TC-R07: Assign Permissions to Role
**Steps**:
1. Login as test admin
2. Navigate to role detail page
3. Click "Edit" button
4. Add new permissions
5. Save changes
6. Verify permissions are updated

#### TC-R08: Remove Permissions from Role
**Steps**:
1. Login as test admin
2. Navigate to role with permissions
3. Click "Edit" button
4. Remove permissions
5. Save changes
6. Verify permissions are removed

---

## 3. Group Management E2E Tests

**File**: `GroupManagementTests.cs`

### API Endpoints Covered
- `GET /api/admin/groups` - List groups
- `GET /api/admin/groups/{id}` - Get group details
- `POST /api/admin/groups` - Create group
- `PATCH /api/admin/groups/{id}` - Update group
- `DELETE /api/admin/groups/{id}` - Delete group
- `GET /api/admin/groups/{id}/members` - Get group members
- `POST /api/admin/groups/{id}/members/{userId}` - Add member
- `DELETE /api/admin/groups/{id}/members/{userId}` - Remove member
- `GET /api/admin/groups/{id}/mappings` - Get group mappings
- `POST /api/admin/groups/{id}/mappings` - Add mapping
- `DELETE /api/admin/groups/{id}/mappings/{mappingId}` - Remove mapping

### Test Cases

#### TC-G01: List Groups with Pagination
#### TC-G02: Search Groups
#### TC-G03: Create New Group
#### TC-G04: View Group Details
#### TC-G05: Edit Group
#### TC-G06: Delete Group
#### TC-G07: Add Member to Group
#### TC-G08: Remove Member from Group
#### TC-G09: View Group Members
#### TC-G10: Add Group Mapping (Role/Claim)
#### TC-G11: Remove Group Mapping

---

## 4. Service Account Management E2E Tests

**File**: `ServiceAccountManagementTests.cs`

### API Endpoints Covered
- `GET /api/admin/service-accounts` - List service accounts
- `GET /api/admin/service-accounts/{id}` - Get service account details
- `POST /api/admin/service-accounts` - Create service account
- `PATCH /api/admin/service-accounts/{id}` - Update service account
- `POST /api/admin/service-accounts/{id}/enable` - Enable service account
- `POST /api/admin/service-accounts/{id}/disable` - Disable service account
- `DELETE /api/admin/service-accounts/{id}` - Delete service account
- `POST /api/admin/service-accounts/{id}/rotate-secret` - Rotate secret
- `POST /api/admin/service-accounts/{id}/certificates` - Add certificate
- `GET /api/admin/service-accounts/{id}/certificates` - Get certificates

### Test Cases

#### TC-SA01: List Service Accounts with Pagination
#### TC-SA02: Search Service Accounts
#### TC-SA03: Create New Service Account
**Special**: Verify secret is displayed once and can be copied

#### TC-SA04: View Service Account Details
#### TC-SA05: Edit Service Account
#### TC-SA06: Enable Service Account
#### TC-SA07: Disable Service Account
#### TC-SA08: Delete Service Account
#### TC-SA09: Rotate Service Account Secret
**Special**: Verify new secret is displayed once

#### TC-SA10: Add Certificate to Service Account
#### TC-SA11: View Service Account Certificates

---

## 5. Session Management E2E Tests

**File**: `SessionManagementTests.cs`

### API Endpoints Covered
- `GET /api/admin/sessions` - List sessions
- `GET /api/admin/sessions/{id}` - Get session details
- `DELETE /api/admin/sessions/{id}` - Revoke session
- `POST /api/admin/users/{userId}/sessions/revoke-all` - Revoke all user sessions

### Test Cases

#### TC-S01: List Sessions with Pagination
#### TC-S02: Search Sessions
#### TC-S03: View Session Details
#### TC-S04: Revoke Single Session
**Steps**:
1. Login as test admin (creates a session)
2. Navigate to /sessions
3. Find the admin user's session
4. Click on session details
5. Click "Revoke" button
6. Confirm action
7. Verify session status changes

#### TC-S05: Revoke All User Sessions
**Steps**:
1. Login as test admin
2. Create another test user and session
3. Navigate to user detail page
4. Click "Revoke All Sessions" button
5. Confirm action
6. Verify all sessions are revoked

---

## 6. Provider Management E2E Tests

**File**: `ProviderManagementTests.cs`

### API Endpoints Covered
- `GET /api/admin/providers` - List providers
- `GET /api/admin/providers/{id}` - Get provider details
- `POST /api/admin/providers` - Create provider
- `PATCH /api/admin/providers/{id}` - Update provider
- `DELETE /api/admin/providers/{id}` - Delete provider

### Test Cases

#### TC-P01: List Providers with Pagination
#### TC-P02: Search Providers
#### TC-P03: Create OIDC Provider
**Steps**:
1. Login as test admin
2. Navigate to /providers
3. Click "Create Provider" button
4. Select "OIDC" provider type
5. Fill in OIDC-specific fields
6. Submit form
7. Verify provider created

#### TC-P04: Create OAuth2 Provider
**Steps**: Similar to TC-P03 but with OAuth2 type

#### TC-P05: Create SAML2 Provider
**Steps**: Similar to TC-P03 but with SAML2 type

#### TC-P06: View Provider Details
#### TC-P07: Edit Provider
**Special**: Verify type-specific fields are shown/hidden correctly

#### TC-P08: Delete Provider

---

## 7. Dashboard E2E Tests

**File**: `DashboardTests.cs`

### Test Cases

#### TC-D01: View Dashboard
**Steps**:
1. Login as test admin
2. Verify landing on dashboard
3. Verify dashboard metrics are displayed
4. Verify quick action cards are visible

#### TC-D02: Dashboard Metrics Display
**Steps**:
1. Login as test admin
2. Verify user count metric
3. Verify active sessions metric
4. Verify other dashboard statistics

#### TC-D03: Quick Navigation Links
**Steps**:
1. Login as test admin
2. Click "View active sessions" link
3. Verify redirect to /sessions
4. Go back to dashboard
5. Test other quick links

---

## 8. Integration & Cross-Feature Tests

**File**: `IntegrationTests.cs`

### Test Cases

#### TC-I01: User to Role to Group Workflow
**Steps**:
1. Create a new role with permissions
2. Create a new group
3. Add role mapping to group
4. Create a new user
5. Add user to group
6. Verify user has role via group membership

#### TC-I02: Service Account Full Lifecycle
**Steps**:
1. Create service account
2. Rotate secret
3. Add certificate
4. Disable account
5. Enable account
6. Delete account

#### TC-I03: Session Management After User Disable
**Steps**:
1. Create user and login as that user (create session)
2. As admin, disable the user
3. Verify user's sessions are still visible
4. Revoke user's sessions
5. Verify sessions are revoked

---

## Implementation Strategy

### Phase 1: Foundation (Week 1)
1. Create shared helper methods in `TestHelpers.cs`
   - `LoginHelper` - Reusable login method
   - `NavigationHelper` - Common navigation utilities
   - `FormHelper` - Form filling utilities
   - `AssertionHelper` - Custom Playwright assertions
   
2. Create `TestDataBuilder.cs` for test data generation
   - Random email generator
   - Random name generator
   - Test user factory
   - Test role factory

### Phase 2: User & Role Tests (Week 2)
1. Implement UserManagementTests.cs
2. Implement RoleManagementTests.cs
3. Run and debug tests
4. Update documentation

### Phase 3: Group & Service Account Tests (Week 3)
1. Implement GroupManagementTests.cs
2. Implement ServiceAccountManagementTests.cs
3. Run and debug tests
4. Update documentation

### Phase 4: Session & Provider Tests (Week 4)
1. Implement SessionManagementTests.cs
2. Implement ProviderManagementTests.cs
3. Run and debug tests
4. Update documentation

### Phase 5: Dashboard & Integration Tests (Week 5)
1. Implement DashboardTests.cs
2. Implement IntegrationTests.cs
3. Final test run and debugging
4. Performance optimization

### Phase 6: Documentation & CI/CD (Week 6)
1. Update README.md with all new tests
2. Create test execution guide
3. Add CI/CD pipeline configuration
4. Create test maintenance guide

---

## Test Utilities & Helpers

### Shared Helper Methods

```csharp
public static class TestHelpers
{
    // Reusable login method
    public static async Task LoginAsTestAdminAsync(IPage page, string url);
    
    // Wait for data table to load
    public static async Task WaitForDataTableAsync(IPage page);
    
    // Fill and submit a form
    public static async Task FillFormAsync(IPage page, Dictionary<string, string> fields);
    
    // Navigate to a specific feature page
    public static async Task NavigateToAsync(IPage page, string feature);
    
    // Click a button with confirmation dialog
    public static async Task ClickWithConfirmationAsync(IPage page, string buttonText);
}
```

### Test Data Builder Pattern

```csharp
public class TestUserBuilder
{
    public string Email { get; set; } = GenerateRandomEmail();
    public string DisplayName { get; set; } = "Test User";
    public string Password { get; set; } = "Test123!";
    
    public TestUserBuilder WithEmail(string email);
    public TestUserBuilder WithDisplayName(string name);
    public TestUserBuilder WithPassword(string password);
    public async Task<User> CreateViaUIAsync(IPage page);
}
```

---

## Success Metrics

### Coverage Goals
- ✅ 100% of API endpoints tested via UI
- ✅ All CRUD operations for each entity tested
- ✅ All critical user workflows tested
- ✅ All error scenarios tested

### Quality Goals
- ✅ All tests pass consistently
- ✅ Test execution time < 15 minutes
- ✅ 95%+ test reliability (no flaky tests)
- ✅ Clear test failure messages
- ✅ Easy to maintain and extend

### Documentation Goals
- ✅ All tests documented with purpose
- ✅ Test maintenance guide created
- ✅ CI/CD integration documented
- ✅ Troubleshooting guide available

---

## Maintenance & Best Practices

### Test Maintenance
1. Review tests monthly for relevance
2. Update tests when features change
3. Remove obsolete tests
4. Refactor duplicated code

### Best Practices
1. **Keep tests focused** - One test per scenario
2. **Use descriptive names** - Test name should explain what it tests
3. **Assert clearly** - Use Shouldly for readable assertions
4. **Clean up** - Each test should clean up its data
5. **Be patient** - Use appropriate wait strategies
6. **Log failures** - Capture screenshots on failure
7. **Run locally** - Test locally before pushing

### Common Pitfalls to Avoid
- ❌ Don't rely on test execution order
- ❌ Don't share state between tests
- ❌ Don't use hard-coded waits (use WaitFor methods)
- ❌ Don't test implementation details
- ❌ Don't create brittle selectors

---

## Appendix

### API Endpoint Summary

**Users**
- GET /api/admin/users
- GET /api/admin/users/{id}
- POST /api/admin/users
- PATCH /api/admin/users/{id}
- DELETE /api/admin/users/{id}
- POST /api/admin/users/{id}/disable
- POST /api/admin/users/{id}/enable
- POST /api/admin/users/{id}/reset-password
- GET /api/admin/users/{id}/roles
- POST /api/admin/users/{id}/roles/{roleId}
- DELETE /api/admin/users/{id}/roles/{roleId}
- GET /api/admin/users/{id}/groups
- GET /api/admin/users/{id}/upstream-identities
- POST /api/admin/users/{id}/upstream-identities
- DELETE /api/admin/users/{id}/upstream-identities/{providerId}

**Roles**
- GET /api/admin/roles
- GET /api/admin/roles/{id}
- POST /api/admin/roles
- PATCH /api/admin/roles/{id}
- DELETE /api/admin/roles/{id}

**Groups**
- GET /api/admin/groups
- GET /api/admin/groups/{id}
- POST /api/admin/groups
- PATCH /api/admin/groups/{id}
- DELETE /api/admin/groups/{id}
- GET /api/admin/groups/{id}/members
- POST /api/admin/groups/{id}/members/{userId}
- DELETE /api/admin/groups/{id}/members/{userId}
- GET /api/admin/groups/{id}/mappings
- POST /api/admin/groups/{id}/mappings
- DELETE /api/admin/groups/{id}/mappings/{mappingId}

**Service Accounts**
- GET /api/admin/service-accounts
- GET /api/admin/service-accounts/{id}
- POST /api/admin/service-accounts
- PATCH /api/admin/service-accounts/{id}
- POST /api/admin/service-accounts/{id}/enable
- POST /api/admin/service-accounts/{id}/disable
- DELETE /api/admin/service-accounts/{id}
- POST /api/admin/service-accounts/{id}/rotate-secret
- POST /api/admin/service-accounts/{id}/certificates
- GET /api/admin/service-accounts/{id}/certificates

**Sessions**
- GET /api/admin/sessions
- GET /api/admin/sessions/{id}
- DELETE /api/admin/sessions/{id}
- POST /api/admin/users/{userId}/sessions/revoke-all

**Providers**
- GET /api/admin/providers
- GET /api/admin/providers/{id}
- POST /api/admin/providers
- PATCH /api/admin/providers/{id}
- DELETE /api/admin/providers/{id}

### Technology Stack
- **.NET 10** - Runtime
- **Aspire** - Application orchestration and testing
- **Playwright** - Browser automation
- **xUnit v3** - Test framework
- **Shouldly** - Assertions
- **PostgreSQL** - Database (via Docker in tests)

### References
- [Playwright .NET Documentation](https://playwright.dev/dotnet/)
- [Aspire Testing Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/testing/)
- [xUnit Documentation](https://xunit.net/)
- [Shouldly Documentation](https://github.com/shouldly/shouldly)
