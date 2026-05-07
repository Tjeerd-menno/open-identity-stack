# E2E Test Coverage Visualization

## Test Suite Structure

```
OpenIdentityStack.AdminWeb.E2ETests/
├── Fixtures/
│   └── AdminWebAppHostFixture.cs          # Aspire test fixture
├── Helpers/
│   ├── TestHelpers.cs                     # Reusable helper methods
│   └── TestDataBuilder.cs                 # Test data builders
├── Existing Tests (14 tests)
│   ├── AuthenticationFlowTests.cs         # 6 tests ✅
│   └── AdminWebLayoutTests.cs             # 8 tests ✅
└── Planned Tests (63 tests)
    ├── UserManagementTests.cs             # 14 tests 📋
    ├── RoleManagementTests.cs             # 8 tests 📋
    ├── GroupManagementTests.cs            # 11 tests 📋
    ├── ServiceAccountManagementTests.cs   # 11 tests 📋
    ├── SessionManagementTests.cs          # 5 tests 📋
    ├── ProviderManagementTests.cs         # 8 tests 📋
    ├── DashboardTests.cs                  # 3 tests 📋
    └── IntegrationTests.cs                # 3 tests 📋
```

## API Endpoint Coverage Map

### Users Feature (15 endpoints)
```
GET    /api/admin/users                                    → TC-U01: List Users
GET    /api/admin/users/{id}                               → TC-U04: View User Details
POST   /api/admin/users                                    → TC-U03: Create User
PATCH  /api/admin/users/{id}                               → TC-U05: Edit User
DELETE /api/admin/users/{id}                               → TC-U08: Delete User
POST   /api/admin/users/{id}/disable                       → TC-U06: Disable User
POST   /api/admin/users/{id}/enable                        → TC-U07: Enable User
POST   /api/admin/users/{id}/reset-password                → TC-U09: Reset Password
GET    /api/admin/users/{id}/roles                         → TC-U10: Assign Role
POST   /api/admin/users/{id}/roles/{roleId}                → TC-U10: Assign Role
DELETE /api/admin/users/{id}/roles/{roleId}                → TC-U11: Remove Role
GET    /api/admin/users/{id}/groups                        → TC-U12: View User Groups
GET    /api/admin/users/{id}/upstream-identities           → TC-U13: Link Identity
POST   /api/admin/users/{id}/upstream-identities           → TC-U13: Link Identity
DELETE /api/admin/users/{id}/upstream-identities/{id}      → TC-U14: Unlink Identity
```

### Roles Feature (5 endpoints)
```
GET    /api/admin/roles                                    → TC-R01: List Roles
GET    /api/admin/roles/{id}                               → TC-R04: View Role Details
POST   /api/admin/roles                                    → TC-R03: Create Role
PATCH  /api/admin/roles/{id}                               → TC-R05: Edit Role
DELETE /api/admin/roles/{id}                               → TC-R06: Delete Role
```

### Groups Feature (11 endpoints)
```
GET    /api/admin/groups                                   → TC-G01: List Groups
GET    /api/admin/groups/{id}                              → TC-G04: View Group Details
POST   /api/admin/groups                                   → TC-G03: Create Group
PATCH  /api/admin/groups/{id}                              → TC-G05: Edit Group
DELETE /api/admin/groups/{id}                              → TC-G06: Delete Group
GET    /api/admin/groups/{id}/members                      → TC-G09: View Members
POST   /api/admin/groups/{id}/members/{userId}             → TC-G07: Add Member
DELETE /api/admin/groups/{id}/members/{userId}             → TC-G08: Remove Member
GET    /api/admin/groups/{id}/mappings                     → TC-G10: Add Mapping
POST   /api/admin/groups/{id}/mappings                     → TC-G10: Add Mapping
DELETE /api/admin/groups/{id}/mappings/{mappingId}         → TC-G11: Remove Mapping
```

### Service Accounts Feature (10 endpoints)
```
GET    /api/admin/service-accounts                         → TC-SA01: List Service Accounts
GET    /api/admin/service-accounts/{id}                    → TC-SA04: View Service Account
POST   /api/admin/service-accounts                         → TC-SA03: Create Service Account
PATCH  /api/admin/service-accounts/{id}                    → TC-SA05: Edit Service Account
POST   /api/admin/service-accounts/{id}/enable             → TC-SA06: Enable Service Account
POST   /api/admin/service-accounts/{id}/disable            → TC-SA07: Disable Service Account
DELETE /api/admin/service-accounts/{id}                    → TC-SA08: Delete Service Account
POST   /api/admin/service-accounts/{id}/rotate-secret      → TC-SA09: Rotate Secret
POST   /api/admin/service-accounts/{id}/certificates       → TC-SA10: Add Certificate
GET    /api/admin/service-accounts/{id}/certificates       → TC-SA11: View Certificates
```

### Sessions Feature (4 endpoints)
```
GET    /api/admin/sessions                                 → TC-S01: List Sessions
GET    /api/admin/sessions/{id}                            → TC-S03: View Session Details
DELETE /api/admin/sessions/{id}                            → TC-S04: Revoke Session
POST   /api/admin/users/{userId}/sessions/revoke-all       → TC-S05: Revoke All Sessions
```

### Providers Feature (5 endpoints)
```
GET    /api/admin/providers                                → TC-P01: List Providers
GET    /api/admin/providers/{id}                           → TC-P06: View Provider Details
POST   /api/admin/providers                                → TC-P03-05: Create Provider
PATCH  /api/admin/providers/{id}                           → TC-P07: Edit Provider
DELETE /api/admin/providers/{id}                           → TC-P08: Delete Provider
```

## Test Flow Diagram

### User Management Test Flow
```
Login → Navigate to Users → Create User → Verify Created → Edit User → 
Verify Edited → Assign Role → Verify Role → Disable User → Verify Disabled → 
Enable User → Verify Enabled → Delete User → Verify Deleted
```

### Role Management Test Flow
```
Login → Navigate to Roles → Create Role → Add Permissions → Verify Created → 
Edit Role → Update Permissions → Verify Updated → Delete Role → Verify Deleted
```

### Group Management Test Flow
```
Login → Navigate to Groups → Create Group → Add Member → Verify Member → 
Add Mapping → Verify Mapping → Remove Mapping → Verify Removed → 
Remove Member → Delete Group
```

### Service Account Test Flow
```
Login → Navigate to Service Accounts → Create SA → Copy Secret → 
Rotate Secret → Copy New Secret → Add Certificate → Disable SA → 
Enable SA → Delete SA
```

### Session Management Test Flow
```
Login (creates session) → Navigate to Sessions → View Session Details → 
Revoke Session → Verify Revoked → Create User Session → Revoke All → 
Verify All Revoked
```

### Provider Management Test Flow
```
Login → Navigate to Providers → Create OIDC Provider → Verify Created → 
Create OAuth2 Provider → Create SAML2 Provider → Edit Provider → 
Verify Type-Specific Fields → Delete Provider
```

## Test Dependency Graph

```
                    ┌─────────────────┐
                    │ AdminWebApp     │
                    │ Fixture         │
                    │ (Aspire)        │
                    └────────┬────────┘
                             │
                ┌────────────┴────────────┐
                │                         │
         ┌──────▼──────┐           ┌─────▼──────┐
         │ PostgreSQL  │           │    API     │
         │ Container   │◄──────────│  Service   │
         └─────────────┘           └─────┬──────┘
                                         │
                                  ┌──────▼──────┐
                                  │  AdminWeb   │
                                  │   (Vite)    │
                                  └──────┬──────┘
                                         │
                              ┌──────────▼──────────┐
                              │   Playwright        │
                              │   Browser Tests     │
                              └─────────────────────┘
```

## Implementation Timeline (6 Weeks)

```
Week 1: Foundation
├── Create TestHelpers.cs ✅
├── Create TestDataBuilder.cs ✅
└── Documentation ✅

Week 2: User & Role Tests
├── UserManagementTests.cs (14 tests)
└── RoleManagementTests.cs (8 tests)

Week 3: Group & Service Account Tests
├── GroupManagementTests.cs (11 tests)
└── ServiceAccountManagementTests.cs (11 tests)

Week 4: Session & Provider Tests (Part 1)
├── SessionManagementTests.cs (5 tests)
└── ProviderManagementTests.cs (4 tests)

Week 5: Provider & Dashboard Tests
├── ProviderManagementTests.cs (4 tests cont.)
├── DashboardTests.cs (3 tests)
└── IntegrationTests.cs (3 tests)

Week 6: Finalization
├── Documentation Updates
├── Full Test Suite Run
└── CI/CD Integration
```

## Test Execution Flow

```
┌─────────────────────────────────────────────────────┐
│ 1. Test Runner Starts                               │
│    - Initialize Aspire Fixture                      │
│    - Start PostgreSQL Container                     │
│    - Start API Service                              │
│    - Start AdminWeb Service                         │
│    - Seed Test Data (admin@test.com)                │
└─────────────┬───────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────┐
│ 2. Test Class Initialization                        │
│    - Initialize Playwright                          │
│    - Launch Browser (Chromium, Headless)            │
│    - Create Browser Context                         │
│    - Create New Page                                │
└─────────────┬───────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────┐
│ 3. Test Execution                                   │
│    - Login via OIDC flow                            │
│    - Navigate to feature                            │
│    - Perform test actions                           │
│    - Verify expected results                        │
│    - Clean up test data                             │
└─────────────┬───────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────┐
│ 4. Test Class Cleanup                               │
│    - Close Page                                     │
│    - Close Browser Context                          │
│    - Close Browser                                  │
│    - Dispose Playwright                             │
└─────────────┬───────────────────────────────────────┘
              │
┌─────────────▼───────────────────────────────────────┐
│ 5. Test Runner Cleanup                              │
│    - Stop AdminWeb Service                          │
│    - Stop API Service                               │
│    - Stop PostgreSQL Container                      │
│    - Dispose Aspire Fixture                         │
└─────────────────────────────────────────────────────┘
```

## Success Metrics Dashboard

```
┌──────────────────────────────────────────────────────┐
│ E2E Test Coverage Metrics                            │
├──────────────────────────────────────────────────────┤
│                                                      │
│ Test Count:          77 tests                        │
│   Existing:          14 tests ✅                     │
│   Planned:           63 tests 📋                     │
│                                                      │
│ API Endpoints:       50 endpoints                    │
│   Coverage:          100% 🎯                         │
│                                                      │
│ Features:            8 features                      │
│   Covered:           100% ✅                         │
│                                                      │
│ Test Reliability:    Target: 95%+                    │
│ Execution Time:      Target: <15 min                 │
│ Flaky Tests:         Target: 0                       │
│                                                      │
└──────────────────────────────────────────────────────┘
```

## Test Coverage Heat Map

```
Feature                    | API Endpoints | Test Cases | Coverage
---------------------------|---------------|------------|----------
Users                      |      15       |     14     |  ████████
Roles                      |       5       |      8     |  ████████
Groups                     |      11       |     11     |  ████████
Service Accounts           |      10       |     11     |  ████████
Sessions                   |       4       |      5     |  ████████
Providers                  |       5       |      8     |  ████████
Dashboard                  |       -       |      3     |  ████████
Integration                |       -       |      3     |  ████████
Authentication (existing)  |       -       |      6     |  ████████
Layout (existing)          |       -       |      8     |  ████████
---------------------------|---------------|------------|----------
TOTAL                      |      50       |     77     |  ████████
```

Legend: ████████ = 100% Coverage

## Test Data Flow

```
                  ┌─────────────────────┐
                  │   Test Fixture      │
                  │   Seeds Base Data   │
                  │   (admin@test.com)  │
                  └──────────┬──────────┘
                             │
           ┌─────────────────┴─────────────────┐
           │                                   │
    ┌──────▼──────┐                    ┌──────▼──────┐
    │ TestHelpers │                    │ TestData    │
    │ - Login     │                    │ Builder     │
    │ - Navigate  │                    │ - User()    │
    │ - Search    │                    │ - Role()    │
    │ - Fill Form │                    │ - Group()   │
    └──────┬──────┘                    └──────┬──────┘
           │                                   │
           └─────────────────┬─────────────────┘
                             │
                    ┌────────▼────────┐
                    │  Test Execution │
                    │                 │
                    │ 1. Create Data  │
                    │ 2. Test Action  │
                    │ 3. Verify Result│
                    │ 4. Clean Up     │
                    └─────────────────┘
```

---

This visualization provides a clear overview of the E2E test structure, coverage, and implementation plan.
