# Tasks: React Admin Web App with Shadcn UI

**Branch**: `copilot/add-react-web-app-shadcn-ui` | **Date**: 2026-01-18  
**Input**: Design documents from `/specs/copilot/add-react-web-app-shadcn-ui/`  
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api-summary.md, quickstart.md

**Feature Summary**: Build a modern React-based administrative web application with Shadcn UI component library to manage users, roles, groups, service accounts, sessions, and identity providers. The application authenticates via OAuth2/OIDC and is orchestrated by .NET Aspire AppHost.

**Tests**: Test-First Development (TDD) is required per constitution. All test tasks must be completed and verified as FAILING before implementing features.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each administrative module.

---

## Format: `- [ ] [ID] [P?] [Story?] Description`

- **Checkbox**: `- [ ]` (required)
- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story (US1-US7 for auth + 6 admin modules)
- File paths are based on project structure from plan.md

---

## Phase 1: Setup (Project Infrastructure)

**Purpose**: Initialize the React project with required tooling and configuration

- [X] T001 Create React TypeScript project with Vite in src/OpenIdentityStack.AdminWeb/
- [X] T002 [P] Install core dependencies (react, react-dom, react-router-dom, @tanstack/react-query) in package.json
- [X] T003 [P] Install TypeScript and type definitions in package.json
- [X] T004 [P] Configure TypeScript compiler options in tsconfig.json and tsconfig.node.json
- [X] T005 [P] Configure Vite build tool in vite.config.ts with proxy settings for API
- [X] T006 [P] Install and configure Tailwind CSS in tailwind.config.js and postcss.config.js
- [X] T007 [P] Initialize Shadcn UI components library with components.json configuration
- [X] T008 [P] Install authentication library (oidc-client-ts) in package.json
- [X] T009 [P] Install form validation library (zod, react-hook-form) in package.json
- [X] T010 [P] Configure ESLint in .eslintrc.cjs with React and TypeScript rules
- [X] T011 [P] Configure Prettier in .prettierrc for code formatting
- [X] T012 [P] Setup test infrastructure with Vitest in vitest.config.ts
- [X] T013 [P] Install testing libraries (vitest, @testing-library/react, @testing-library/jest-dom) in package.json
- [X] T014 [P] Install Playwright for E2E testing in tests/ directory
- [X] T015 Create environment variable template in .env.example with OIDC and API configuration
- [X] T016 Create basic project structure: src/features/, src/components/, src/lib/, src/hooks/, src/routes/, src/types/
- [X] T017 [P] Add base CSS file with Tailwind directives in src/index.css
- [X] T018 [P] Create initial HTML entry point in index.html

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T019 Create TypeScript type definitions from data-model.md in src/types/index.ts
- [X] T020 [P] Create Zod validation schemas for all data models in src/types/schemas.ts
- [X] T021 [P] Create base API client with Axios interceptors in src/lib/api/client.ts
- [X] T022 [P] Implement API error handling utilities in src/lib/api/error-handler.ts
- [X] T023 [P] Setup TanStack Query client configuration in src/lib/api/query-client.ts
- [X] T024 [P] Create common utility functions in src/lib/utils.ts (cn, formatters, etc.)
- [X] T025 [P] Create application constants in src/lib/constants.ts (API endpoints, permissions)
- [X] T026 Add Shadcn UI base components: Button, Input, Label, Card, Badge in src/components/ui/
- [X] T027 [P] Add Shadcn UI table components: Table, DataTable in src/components/ui/
- [X] T028 [P] Add Shadcn UI form components: Form, Select, Checkbox, Textarea in src/components/ui/
- [X] T029 [P] Add Shadcn UI dialog components: Dialog, AlertDialog in src/components/ui/
- [ ] T030 [P] Add Shadcn UI feedback components: Toast, Alert in src/components/ui/
- [ ] T031 [P] Add Shadcn UI navigation components: Dropdown Menu, Tabs in src/components/ui/
- [X] T032 Create reusable DataTable component with pagination in src/components/common/DataTable.tsx
- [X] T033 [P] Create reusable ConfirmDialog component in src/components/common/ConfirmDialog.tsx
- [X] T034 [P] Create reusable LoadingSpinner component in src/components/common/LoadingSpinner.tsx
- [X] T035 [P] Create reusable ErrorBoundary component in src/components/common/ErrorBoundary.tsx
- [ ] T036 Create AppShell layout component in src/components/layout/AppShell.tsx
- [ ] T037 [P] Create Sidebar navigation component in src/components/layout/Sidebar.tsx
- [ ] T038 [P] Create Header component with user menu in src/components/layout/Header.tsx
- [ ] T039 Create route configuration in src/routes/index.tsx with lazy loading
- [X] T040 Create main App component in src/App.tsx with QueryClientProvider and Router
- [X] T041 Create application entry point in src/main.tsx
- [ ] T042 Update .NET Aspire AppHost to add admin web app with AddNpmApp() in src/OpenIdentityStack.AppHost/AppHost.cs
- [ ] T043 [P] Configure CORS policy in API for admin web app origin in src/OpenIdentityStack.Api/Program.cs
- [ ] T044 [P] Register admin-web-client in OpenIddict server with PKCE support in src/OpenIdentityStack.Api/Program.cs

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Authentication (Priority: P1) 🎯 MVP

**Goal**: Implement OAuth2/OIDC authentication flow with PKCE to allow administrators to securely log in

**Independent Test**: 
1. Navigate to admin web app
2. Click "Login" - should redirect to OpenIddict authorization endpoint
3. Enter credentials - should redirect back with authorization code
4. Should exchange code for tokens and show user information
5. Can log out successfully

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T045 [P] [US1] E2E test for login flow in tests/e2e/auth.spec.ts
- [X] T046 [P] [US1] E2E test for logout flow in tests/e2e/auth.spec.ts
- [X] T047 [P] [US1] E2E test for token refresh in tests/e2e/auth.spec.ts
- [ ] T048 [P] [US1] Unit test for AuthContext in src/features/auth/__tests__/AuthContext.test.tsx
- [ ] T049 [P] [US1] Unit test for useAuth hook in src/features/auth/__tests__/useAuth.test.ts

### Implementation for User Story 1

- [X] T050 [US1] Configure OIDC client settings in src/features/auth/services/oidc-config.ts
- [X] T051 [US1] Create AuthContext with UserManager from oidc-client-ts in src/features/auth/AuthContext.tsx
- [X] T052 [US1] Implement useAuth custom hook in src/features/auth/hooks/useAuth.ts
- [X] T053 [US1] Implement useRequireAuth hook for protected routes in src/features/auth/hooks/useRequireAuth.ts
- [X] T054 [P] [US1] Create Login page component in src/features/auth/components/Login.tsx
- [X] T055 [P] [US1] Create Callback page component in src/features/auth/components/Callback.tsx
- [X] T056 [P] [US1] Create SilentCallback component for token refresh in src/features/auth/components/SilentCallback.tsx
- [X] T057 [US1] Create ProtectedRoute wrapper component in src/features/auth/components/ProtectedRoute.tsx
- [X] T058 [US1] Add authentication routes to router in src/routes/index.tsx
- [X] T059 [US1] Integrate AuthContext into App.tsx
- [X] T060 [US1] Add API client axios interceptor for Bearer token injection in src/lib/api/client.ts
- [X] T061 [US1] Add axios interceptor for 401 handling and logout in src/lib/api/client.ts
- [X] T062 [US1] Create user profile display in Header component in src/components/layout/Header.tsx

**Checkpoint**: Authentication flow complete - admin can log in, tokens are managed, API calls are authenticated

---

## Phase 4: User Story 2 - User Management (Priority: P1)

**Goal**: Enable administrators to perform full CRUD operations on users including roles, groups, and upstream identities

**Independent Test**:
1. Log in as admin
2. Navigate to Users page - should display paginated user list
3. Create new user - should appear in list
4. View user details - should show roles, groups, upstream identities
5. Update user - changes should persist
6. Disable/enable user - status should change
7. Delete user - should be removed from list

### Tests for User Story 2

- [ ] T063 [P] [US2] Contract test for GET /api/admin/users in tests/unit/api/users-api.test.ts
- [ ] T064 [P] [US2] Contract test for POST /api/admin/users in tests/unit/api/users-api.test.ts
- [ ] T065 [P] [US2] Contract test for PATCH /api/admin/users/{id} in tests/unit/api/users-api.test.ts
- [ ] T066 [P] [US2] Contract test for DELETE /api/admin/users/{id} in tests/unit/api/users-api.test.ts
- [ ] T067 [P] [US2] Unit test for useUsers hook in src/features/users/__tests__/useUsers.test.ts
- [ ] T068 [P] [US2] Unit test for useCreateUser hook in src/features/users/__tests__/useCreateUser.test.ts
- [ ] T069 [P] [US2] Component test for UserList in src/features/users/components/__tests__/UserList.test.tsx
- [ ] T070 [P] [US2] Component test for UserForm in src/features/users/components/__tests__/UserForm.test.tsx
- [X] T071 [P] [US2] E2E test for user CRUD operations in tests/e2e/users.spec.ts

### Implementation for User Story 2

- [X] T072 [P] [US2] Create Users API client in src/features/users/api/users-api.ts with all endpoints
- [X] T073 [P] [US2] Create useUsers query hook in src/features/users/hooks/useUsers.ts
- [X] T074 [P] [US2] Create useUser query hook in src/features/users/hooks/useUser.ts
- [X] T075 [P] [US2] Create useCreateUser mutation hook in src/features/users/hooks/useCreateUser.ts
- [X] T076 [P] [US2] Create useUpdateUser mutation hook in src/features/users/hooks/useUpdateUser.ts
- [X] T077 [P] [US2] Create useDisableUser mutation hook in src/features/users/hooks/useDisableUser.ts
- [X] T078 [P] [US2] Create useEnableUser mutation hook in src/features/users/hooks/useEnableUser.ts
- [X] T079 [P] [US2] Create useDeleteUser mutation hook in src/features/users/hooks/useDeleteUser.ts
- [X] T080 [P] [US2] Create useResetPassword mutation hook in src/features/users/hooks/useResetPassword.ts
- [X] T081 [P] [US2] Create useUserRoles query hook in src/features/users/hooks/useUserRoles.ts
- [X] T082 [P] [US2] Create useAssignRole mutation hook in src/features/users/hooks/useAssignRole.ts
- [X] T083 [P] [US2] Create useUnassignRole mutation hook in src/features/users/hooks/useUnassignRole.ts
- [X] T084 [P] [US2] Create useUserGroups query hook in src/features/users/hooks/useUserGroups.ts
- [X] T085 [P] [US2] Create useUpstreamIdentities query hook in src/features/users/hooks/useUpstreamIdentities.ts
- [X] T086 [P] [US2] Create useLinkIdentity mutation hook in src/features/users/hooks/useLinkIdentity.ts
- [X] T087 [P] [US2] Create useUnlinkIdentity mutation hook in src/features/users/hooks/useUnlinkIdentity.ts
- [X] T088 [US2] Create UserList component with pagination and search in src/features/users/components/UserList.tsx
- [X] T089 [P] [US2] Create UserDetail component in src/features/users/components/UserDetail.tsx
- [X] T090 [P] [US2] Create UserForm component for create/update in src/features/users/components/UserForm.tsx
- [X] T091 [P] [US2] Create UserStatusBadge component in src/features/users/components/UserStatusBadge.tsx
- [X] T092 [P] [US2] Create UserRolesList component in src/features/users/components/UserRolesList.tsx
- [X] T093 [P] [US2] Create UserGroupsList component in src/features/users/components/UserGroupsList.tsx
- [X] T094 [P] [US2] Create UpstreamIdentitiesList component in src/features/users/components/UpstreamIdentitiesList.tsx
- [X] T095 [P] [US2] Create ResetPasswordDialog component in src/features/users/components/ResetPasswordDialog.tsx
- [X] T096 [US2] Add user management routes to router in src/routes/index.tsx
- [X] T097 [US2] Add Users navigation item to Sidebar in src/components/layout/Sidebar.tsx

**Checkpoint**: User management complete - full CRUD operations on users with roles, groups, and identity linking

---

## Phase 5: User Story 3 - Role Management (Priority: P1)

**Goal**: Enable administrators to create, view, update, and delete roles with permission management

**Independent Test**:
1. Navigate to Roles page - should display all roles
2. Create new role with permissions - should appear in list
3. View role details - should show permissions
4. Update role permissions - changes should persist
5. Delete non-system role - should be removed
6. System roles cannot be deleted

### Tests for User Story 3

- [ ] T098 [P] [US3] Contract test for GET /api/admin/roles in tests/unit/api/roles-api.test.ts
- [ ] T099 [P] [US3] Contract test for POST /api/admin/roles in tests/unit/api/roles-api.test.ts
- [ ] T100 [P] [US3] Contract test for PATCH /api/admin/roles/{id} in tests/unit/api/roles-api.test.ts
- [ ] T101 [P] [US3] Unit test for useRoles hook in src/features/roles/__tests__/useRoles.test.ts
- [ ] T102 [P] [US3] Component test for RoleList in src/features/roles/components/__tests__/RoleList.test.tsx
- [X] T103 [P] [US3] E2E test for role CRUD operations in tests/e2e/roles.spec.ts

### Implementation for User Story 3

- [X] T104 [P] [US3] Create Roles API client in src/features/roles/api/roles-api.ts with all endpoints
- [X] T105 [P] [US3] Create useRoles query hook in src/features/roles/hooks/useRoles.ts
- [X] T106 [P] [US3] Create useRole query hook in src/features/roles/hooks/useRole.ts
- [X] T107 [P] [US3] Create useCreateRole mutation hook in src/features/roles/hooks/useCreateRole.ts
- [X] T108 [P] [US3] Create useUpdateRole mutation hook in src/features/roles/hooks/useUpdateRole.ts
- [X] T109 [P] [US3] Create useDeleteRole mutation hook in src/features/roles/hooks/useDeleteRole.ts
- [X] T110 [US3] Create RoleList component with pagination in src/features/roles/components/RoleList.tsx
- [X] T111 [P] [US3] Create RoleDetail component in src/features/roles/components/RoleDetail.tsx
- [X] T112 [P] [US3] Create RoleForm component for create/update in src/features/roles/components/RoleForm.tsx
- [X] T113 [P] [US3] Create PermissionSelector component in src/features/roles/components/PermissionSelector.tsx
- [X] T114 [P] [US3] Create RoleTypeBadge component in src/features/roles/components/RoleTypeBadge.tsx
- [X] T115 [US3] Add role management routes to router in src/routes/index.tsx
- [X] T116 [US3] Add Roles navigation item to Sidebar in src/components/layout/Sidebar.tsx

**Checkpoint**: Role management complete - full CRUD operations on roles with permission management

---

## Phase 6: User Story 4 - Group Management (Priority: P2)

**Goal**: Enable administrators to create groups, manage members, and configure role/claim mappings

**Independent Test**:
1. Navigate to Groups page - should display all groups
2. Create new group - should appear in list
3. Add members to group - members should show in group detail
4. Add role mappings - roles should be assigned to group
5. Remove members and mappings - should update correctly
6. Delete group - should be removed

### Tests for User Story 4

- [ ] T117 [P] [US4] Contract test for GET /api/admin/groups in tests/unit/api/groups-api.test.ts
- [ ] T118 [P] [US4] Contract test for POST /api/admin/groups in tests/unit/api/groups-api.test.ts
- [ ] T119 [P] [US4] Unit test for useGroups hook in src/features/groups/__tests__/useGroups.test.ts
- [ ] T120 [P] [US4] Component test for GroupList in src/features/groups/components/__tests__/GroupList.test.tsx
- [ ] T121 [P] [US4] E2E test for group CRUD operations in tests/e2e/groups.spec.ts

### Implementation for User Story 4

- [ ] T122 [P] [US4] Create Groups API client in src/features/groups/api/groups-api.ts with all endpoints
- [ ] T123 [P] [US4] Create useGroups query hook in src/features/groups/hooks/useGroups.ts
- [ ] T124 [P] [US4] Create useGroup query hook in src/features/groups/hooks/useGroup.ts
- [ ] T125 [P] [US4] Create useCreateGroup mutation hook in src/features/groups/hooks/useCreateGroup.ts
- [ ] T126 [P] [US4] Create useUpdateGroup mutation hook in src/features/groups/hooks/useUpdateGroup.ts
- [ ] T127 [P] [US4] Create useDeleteGroup mutation hook in src/features/groups/hooks/useDeleteGroup.ts
- [ ] T128 [P] [US4] Create useGroupMembers query hook in src/features/groups/hooks/useGroupMembers.ts
- [ ] T129 [P] [US4] Create useAddMember mutation hook in src/features/groups/hooks/useAddMember.ts
- [ ] T130 [P] [US4] Create useRemoveMember mutation hook in src/features/groups/hooks/useRemoveMember.ts
- [ ] T131 [P] [US4] Create useGroupMappings query hook in src/features/groups/hooks/useGroupMappings.ts
- [ ] T132 [P] [US4] Create useAddMapping mutation hook in src/features/groups/hooks/useAddMapping.ts
- [ ] T133 [P] [US4] Create useRemoveMapping mutation hook in src/features/groups/hooks/useRemoveMapping.ts
- [ ] T134 [US4] Create GroupList component with pagination in src/features/groups/components/GroupList.tsx
- [ ] T135 [P] [US4] Create GroupDetail component in src/features/groups/components/GroupDetail.tsx
- [ ] T136 [P] [US4] Create GroupForm component for create/update in src/features/groups/components/GroupForm.tsx
- [ ] T137 [P] [US4] Create GroupMembersList component in src/features/groups/components/GroupMembersList.tsx
- [ ] T138 [P] [US4] Create GroupMappingsList component in src/features/groups/components/GroupMappingsList.tsx
- [ ] T139 [P] [US4] Create AddMemberDialog component in src/features/groups/components/AddMemberDialog.tsx
- [ ] T140 [P] [US4] Create AddMappingDialog component in src/features/groups/components/AddMappingDialog.tsx
- [ ] T141 [US4] Add group management routes to router in src/routes/index.tsx
- [ ] T142 [US4] Add Groups navigation item to Sidebar in src/components/layout/Sidebar.tsx

**Checkpoint**: Group management complete - full CRUD operations with member and mapping management

---

## Phase 7: User Story 5 - Service Account Management (Priority: P2)

**Goal**: Enable administrators to create and manage service accounts with credentials and certificates

**Independent Test**:
1. Navigate to Service Accounts page - should display all service accounts
2. Create new service account - should show initial secret (only once)
3. Rotate secret - should generate new secret (only once)
4. Add certificate - should appear in service account details
5. Enable/disable service account - status should change
6. Delete service account - should be removed

### Tests for User Story 5

- [ ] T143 [P] [US5] Contract test for GET /api/admin/service-accounts in tests/unit/api/service-accounts-api.test.ts
- [ ] T144 [P] [US5] Contract test for POST /api/admin/service-accounts in tests/unit/api/service-accounts-api.test.ts
- [ ] T145 [P] [US5] Unit test for useServiceAccounts hook in src/features/service-accounts/__tests__/useServiceAccounts.test.ts
- [ ] T146 [P] [US5] Component test for ServiceAccountList in src/features/service-accounts/components/__tests__/ServiceAccountList.test.tsx
- [ ] T147 [P] [US5] E2E test for service account CRUD in tests/e2e/service-accounts.spec.ts

### Implementation for User Story 5

- [ ] T148 [P] [US5] Create ServiceAccounts API client in src/features/service-accounts/api/service-accounts-api.ts
- [ ] T149 [P] [US5] Create useServiceAccounts query hook in src/features/service-accounts/hooks/useServiceAccounts.ts
- [ ] T150 [P] [US5] Create useServiceAccount query hook in src/features/service-accounts/hooks/useServiceAccount.ts
- [ ] T151 [P] [US5] Create useCreateServiceAccount mutation hook in src/features/service-accounts/hooks/useCreateServiceAccount.ts
- [ ] T152 [P] [US5] Create useUpdateServiceAccount mutation hook in src/features/service-accounts/hooks/useUpdateServiceAccount.ts
- [ ] T153 [P] [US5] Create useDeleteServiceAccount mutation hook in src/features/service-accounts/hooks/useDeleteServiceAccount.ts
- [ ] T154 [P] [US5] Create useEnableServiceAccount mutation hook in src/features/service-accounts/hooks/useEnableServiceAccount.ts
- [ ] T155 [P] [US5] Create useDisableServiceAccount mutation hook in src/features/service-accounts/hooks/useDisableServiceAccount.ts
- [ ] T156 [P] [US5] Create useRotateSecret mutation hook in src/features/service-accounts/hooks/useRotateSecret.ts
- [ ] T157 [P] [US5] Create useAddCertificate mutation hook in src/features/service-accounts/hooks/useAddCertificate.ts
- [ ] T158 [US5] Create ServiceAccountList component in src/features/service-accounts/components/ServiceAccountList.tsx
- [ ] T159 [P] [US5] Create ServiceAccountDetail component in src/features/service-accounts/components/ServiceAccountDetail.tsx
- [ ] T160 [P] [US5] Create ServiceAccountForm component in src/features/service-accounts/components/ServiceAccountForm.tsx
- [ ] T161 [P] [US5] Create SecretDisplay component for one-time secret display in src/features/service-accounts/components/SecretDisplay.tsx
- [ ] T162 [P] [US5] Create RotateSecretDialog component in src/features/service-accounts/components/RotateSecretDialog.tsx
- [ ] T163 [P] [US5] Create AddCertificateDialog component in src/features/service-accounts/components/AddCertificateDialog.tsx
- [ ] T164 [P] [US5] Create ServiceAccountStatusBadge component in src/features/service-accounts/components/ServiceAccountStatusBadge.tsx
- [ ] T165 [US5] Add service account routes to router in src/routes/index.tsx
- [ ] T166 [US5] Add Service Accounts navigation to Sidebar in src/components/layout/Sidebar.tsx

**Checkpoint**: Service account management complete - full CRUD with credential and certificate management

---

## Phase 8: User Story 6 - Session Management (Priority: P2)

**Goal**: Enable administrators to view active sessions and revoke them individually or in bulk

**Independent Test**:
1. Navigate to Sessions page - should display all active sessions
2. View session details - should show user, IP, user agent, clients
3. Revoke single session - should change status to Revoked
4. Revoke all user sessions - should revoke multiple sessions
5. Session list should update after revocation

### Tests for User Story 6

- [ ] T167 [P] [US6] Contract test for GET /api/admin/sessions in tests/unit/api/sessions-api.test.ts
- [ ] T168 [P] [US6] Contract test for DELETE /api/admin/sessions/{id} in tests/unit/api/sessions-api.test.ts
- [ ] T169 [P] [US6] Unit test for useSessions hook in src/features/sessions/__tests__/useSessions.test.ts
- [ ] T170 [P] [US6] Component test for SessionList in src/features/sessions/components/__tests__/SessionList.test.tsx
- [ ] T171 [P] [US6] E2E test for session revocation in tests/e2e/sessions.spec.ts

### Implementation for User Story 6

- [ ] T172 [P] [US6] Create Sessions API client in src/features/sessions/api/sessions-api.ts with all endpoints
- [ ] T173 [P] [US6] Create useSessions query hook in src/features/sessions/hooks/useSessions.ts
- [ ] T174 [P] [US6] Create useSession query hook in src/features/sessions/hooks/useSession.ts
- [ ] T175 [P] [US6] Create useRevokeSession mutation hook in src/features/sessions/hooks/useRevokeSession.ts
- [ ] T176 [P] [US6] Create useRevokeAllUserSessions mutation hook in src/features/sessions/hooks/useRevokeAllUserSessions.ts
- [ ] T177 [US6] Create SessionList component with pagination in src/features/sessions/components/SessionList.tsx
- [ ] T178 [P] [US6] Create SessionDetail component in src/features/sessions/components/SessionDetail.tsx
- [ ] T179 [P] [US6] Create SessionStatusBadge component in src/features/sessions/components/SessionStatusBadge.tsx
- [ ] T180 [P] [US6] Create RevokeSessionDialog component in src/features/sessions/components/RevokeSessionDialog.tsx
- [ ] T181 [US6] Add session management routes to router in src/routes/index.tsx
- [ ] T182 [US6] Add Sessions navigation item to Sidebar in src/components/layout/Sidebar.tsx

**Checkpoint**: Session management complete - view and revoke operations working

---

## Phase 9: User Story 7 - Provider Management (Priority: P3)

**Goal**: Enable administrators to configure and manage identity providers (OIDC, OAuth2, SAML2)

**Independent Test**:
1. Navigate to Providers page - should display all configured providers
2. Create new OIDC provider - should appear in list
3. View provider details - should show configuration (no client secret)
4. Update provider settings - changes should persist
5. Enable/disable provider - status should change
6. Delete provider - should be removed

### Tests for User Story 7

- [ ] T183 [P] [US7] Contract test for GET /api/admin/providers in tests/unit/api/providers-api.test.ts
- [ ] T184 [P] [US7] Contract test for POST /api/admin/providers in tests/unit/api/providers-api.test.ts
- [ ] T185 [P] [US7] Unit test for useProviders hook in src/features/providers/__tests__/useProviders.test.ts
- [ ] T186 [P] [US7] Component test for ProviderList in src/features/providers/components/__tests__/ProviderList.test.tsx
- [ ] T187 [P] [US7] E2E test for provider CRUD operations in tests/e2e/providers.spec.ts

### Implementation for User Story 7

- [ ] T188 [P] [US7] Create Providers API client in src/features/providers/api/providers-api.ts with all endpoints
- [ ] T189 [P] [US7] Create useProviders query hook in src/features/providers/hooks/useProviders.ts
- [ ] T190 [P] [US7] Create useProvider query hook in src/features/providers/hooks/useProvider.ts
- [ ] T191 [P] [US7] Create useCreateProvider mutation hook in src/features/providers/hooks/useCreateProvider.ts
- [ ] T192 [P] [US7] Create useUpdateProvider mutation hook in src/features/providers/hooks/useUpdateProvider.ts
- [ ] T193 [P] [US7] Create useDeleteProvider mutation hook in src/features/providers/hooks/useDeleteProvider.ts
- [ ] T194 [US7] Create ProviderList component with pagination in src/features/providers/components/ProviderList.tsx
- [ ] T195 [P] [US7] Create ProviderDetail component in src/features/providers/components/ProviderDetail.tsx
- [ ] T196 [P] [US7] Create ProviderForm component for create/update in src/features/providers/components/ProviderForm.tsx
- [ ] T197 [P] [US7] Create ProviderTypeBadge component in src/features/providers/components/ProviderTypeBadge.tsx
- [ ] T198 [P] [US7] Create OIDCConfigSection component in src/features/providers/components/OIDCConfigSection.tsx
- [ ] T199 [US7] Add provider management routes to router in src/routes/index.tsx
- [ ] T200 [US7] Add Providers navigation item to Sidebar in src/components/layout/Sidebar.tsx

**Checkpoint**: Provider management complete - full CRUD operations on identity providers

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Final improvements that enhance the overall application quality

- [ ] T201 [P] Create Dashboard/Home page with statistics in src/features/dashboard/components/Dashboard.tsx
- [ ] T202 [P] Add NotFound (404) page component in src/components/common/NotFound.tsx
- [ ] T203 [P] Add global error handler with user-friendly messages in src/lib/error-handler.ts
- [ ] T204 [P] Implement permission-based UI rendering in src/lib/auth/permissions.ts
- [ ] T205 [P] Add loading states with skeleton components throughout application
- [ ] T206 [P] Add success/error toast notifications for all mutations using Shadcn Toast
- [ ] T207 [P] Implement optimistic updates for critical mutations (user status, etc.)
- [ ] T208 [P] Add form field validation error displays across all forms
- [ ] T209 [P] Create README.md for admin web app in src/OpenIdentityStack.AdminWeb/README.md
- [ ] T210 [P] Update root README.md with admin web app documentation
- [ ] T211 [P] Add JSDoc comments to all API client functions
- [ ] T212 [P] Add JSDoc comments to all custom hooks
- [ ] T213 [P] Verify all tests pass with npm run test
- [ ] T214 [P] Run E2E tests with npm run test:e2e
- [ ] T215 [P] Run linter and fix all issues with npm run lint:fix
- [ ] T216 [P] Run Prettier to format all code with npm run format
- [ ] T217 [P] Generate test coverage report with npm run test:coverage
- [ ] T218 Build production bundle and verify no errors with npm run build
- [ ] T219 Test production build locally with npm run preview
- [ ] T220 Validate quickstart.md instructions by following setup steps
- [ ] T221 [P] Add accessibility improvements (ARIA labels, keyboard navigation)
- [ ] T222 [P] Performance optimization: code splitting, lazy loading verification
- [ ] T223 [P] Security review: XSS protection, CSRF tokens, input sanitization
- [ ] T224 [P] Test application with different permission sets
- [ ] T225 Run full Aspire stack and verify all services work together

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **Authentication (Phase 3)**: Depends on Foundational - REQUIRED for all admin features
- **User Management (Phase 4)**: Depends on Authentication - Can proceed after Phase 3
- **Role Management (Phase 5)**: Depends on Authentication - Can proceed after Phase 3
- **Group Management (Phase 6)**: Depends on Authentication - Can proceed after Phase 3
- **Service Account Management (Phase 7)**: Depends on Authentication - Can proceed after Phase 3
- **Session Management (Phase 8)**: Depends on Authentication - Can proceed after Phase 3
- **Provider Management (Phase 9)**: Depends on Authentication - Can proceed after Phase 3
- **Polish (Phase 10)**: Depends on completion of desired user stories

### User Story Dependencies

- **US1 (Authentication)**: Can start after Foundational (Phase 2) - BLOCKS all other admin features
- **US2 (User Management)**: Depends on US1 (requires authentication)
- **US3 (Role Management)**: Depends on US1 (requires authentication) - Can run parallel with US2
- **US4 (Group Management)**: Depends on US1 (requires authentication) - Can run parallel with US2, US3
- **US5 (Service Accounts)**: Depends on US1 (requires authentication) - Can run parallel with US2-US4
- **US6 (Sessions)**: Depends on US1 (requires authentication) - Can run parallel with US2-US5
- **US7 (Providers)**: Depends on US1 (requires authentication) - Can run parallel with US2-US6

### Within Each User Story

- Tests MUST be written and verified as FAILING before implementation
- API client before hooks
- Query hooks can be parallel with mutation hooks (different files)
- Components can be parallel (different files)
- Routes and navigation at end of story

### Parallel Opportunities

**Phase 1 (Setup)**: Tasks T002-T014 can run in parallel (different config files)

**Phase 2 (Foundational)**: 
- Tasks T019-T025 can run in parallel (different library files)
- Tasks T026-T031 can run in parallel (different Shadcn components)
- Tasks T032-T035 can run in parallel (different common components)
- Tasks T037-T038 can run in parallel (different layout components)
- Tasks T043-T044 can run in parallel (different API configurations)

**Phase 3-9 (User Stories)**: After Phase 3 completes, Phases 4-9 can proceed in parallel (different feature modules)

**Within Each User Story**:
- All test tasks marked [P] can run in parallel
- All API hook tasks marked [P] can run in parallel
- All component tasks marked [P] can run in parallel

**Phase 10 (Polish)**: Tasks T201-T224 can run in parallel (different concerns)

---

## Parallel Example: User Story 2 (User Management)

```bash
# After Authentication (US1) is complete, launch tests in parallel:
Task: "Contract test for GET /api/admin/users"
Task: "Contract test for POST /api/admin/users"
Task: "Contract test for PATCH /api/admin/users/{id}"
Task: "Contract test for DELETE /api/admin/users/{id}"

# Then launch API hooks in parallel:
Task: "Create useUsers query hook"
Task: "Create useUser query hook"
Task: "Create useCreateUser mutation hook"
Task: "Create useUpdateUser mutation hook"
# ... etc (all hook files are independent)

# Then launch components in parallel:
Task: "Create UserDetail component"
Task: "Create UserForm component"
Task: "Create UserStatusBadge component"
Task: "Create UserRolesList component"
# ... etc (all component files are independent)
```

---

## Implementation Strategy

### MVP First (Authentication + User Management)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL)
3. Complete Phase 3: Authentication (US1)
4. Complete Phase 4: User Management (US2)
5. **STOP and VALIDATE**: Test login + user CRUD independently
6. Deploy/demo if ready

This gives you a functional admin app with authentication and the most critical admin feature (user management).

### Incremental Delivery

1. **Foundation**: Setup + Foundational → Infrastructure ready
2. **MVP**: Add Authentication (US1) → Can log in
3. **Core Admin**: Add User Management (US2) → Can manage users (MOST VALUABLE)
4. **Extended Admin**: Add Role Management (US3) → Can manage roles
5. **Team Features**: Add Group Management (US4) → Can organize users
6. **Service Integration**: Add Service Accounts (US5) → Can manage M2M clients
7. **Security**: Add Session Management (US6) → Can audit and revoke sessions
8. **Federation**: Add Provider Management (US7) → Can configure SSO providers
9. **Polish**: Final improvements and optimizations

Each increment adds value and is independently testable and deployable.

### Parallel Team Strategy

With 3-4 developers after completing Setup + Foundational + Authentication:

1. **Developer A**: User Management (US2) - Highest priority
2. **Developer B**: Role Management (US3) - Independent of US2
3. **Developer C**: Group Management (US4) - Independent of US2, US3
4. **Developer D**: Service Accounts (US5) - Independent of others

All features integrate through the shared foundation and can be developed in parallel.

---

## Notes

- **[P] tasks**: Different files, no dependencies - safe to parallelize
- **[Story] labels**: Map tasks to user stories for traceability and independent testing
- **Test-First**: All test tasks must be completed and FAIL before implementation
- **Each user story**: Should be independently completable, testable, and deployable
- **Checkpoints**: Stop at any checkpoint to validate the story independently
- **File paths**: Based on project structure from plan.md
- **Commit strategy**: Commit after each task or logical group of parallel tasks
- **Aspire integration**: Allows running entire stack with one command during development

---

## Total Task Count: 225 tasks

**By Phase**:
- Setup: 18 tasks
- Foundational: 26 tasks  
- Authentication (US1): 18 tasks (13 implementation + 5 tests)
- User Management (US2): 35 tasks (26 implementation + 9 tests)
- Role Management (US3): 19 tasks (13 implementation + 6 tests)
- Group Management (US4): 26 tasks (21 implementation + 5 tests)
- Service Accounts (US5): 24 tasks (19 implementation + 5 tests)
- Session Management (US6): 16 tasks (11 implementation + 5 tests)
- Provider Management (US7): 18 tasks (13 implementation + 5 tests)
- Polish: 25 tasks

**Tests**: 45 test tasks (20% of total) covering contract, unit, component, and E2E tests

**Parallel Opportunities**: 100+ tasks marked [P] can be executed in parallel

**MVP Scope** (Phases 1-4): 97 tasks for core authentication and user management

**Suggested First Delivery**: Phases 1-5 (Authentication + User Management + Role Management) = 116 tasks
