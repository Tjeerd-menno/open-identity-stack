# Tasks: Management Web Management Web Parity

**Input**: Design documents from `/specs/007-management-web/`

**Prerequisites**: plan.md, spec.md, data-model.md, contracts/management-web.md, quickstart.md

**Tests**: Required. Test tasks are listed before implementation tasks for each slice. A slice is not complete without good E2E coverage for operator-critical paths.

**Organization**: Tasks are grouped by implementation phase and vertical slice so each slice can be verified independently.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel with tasks that do not touch the same files
- **[Story]**: Which user story or slice the task belongs to
- Include exact file paths in descriptions
- Required tests appear before implementation tasks for the same slice

## Current Resume Checkpoint (2026-06-02)

**Completed scope**:

- Shared foundation parity is complete through T018.
- Applications slice is complete through T027.
- Users slice refactor/parity is complete through T034.
- Roles slice is complete through T038.
- Groups slice is complete through T042.
- Sessions slice is complete through T046.
- Providers slice is complete through T050.
- Settings slice is complete through T054.
- Application Permissions slice is complete through T058.
- Audit slice is complete through T068.
- Overview, documentation, and final validation are complete through T072.
- ManagementWeb E2E tests are .NET/xUnit Playwright tests, not JavaScript/TypeScript Playwright specs.
- ManagementWeb contains no Clients or Service Accounts navigation; Applications uses only `/api/admin/applications`.

**Latest foundation correction**:

- ManagementWeb authorization must stay granular. The frontend reads concrete grants from `permission`, `permissions`, `scope`, and `scp` claims in both OIDC profile data and decoded access-token payloads.
- Role names such as `admin` or `super-admin` must not become frontend wildcard grants. The backend authorization-code path expands effective role permissions into concrete `permission` token claims, and backend policy remains authoritative.
- Current backend permission constants to preserve in ManagementWeb route/action gates include `system:settings` for Settings and `audit-logs:read` for Audit.

**Latest verification evidence**:

- `cd src/OpenIdentityStack.ManagementWeb; npm test -- src/lib/auth-claims.test.ts src/lib/permissions.test.ts src/routes/AppRoutes.test.tsx src/features/users/UsersPage.test.tsx` passed with 21 tests.
- `cd src/OpenIdentityStack.ManagementWeb; npm test -- src/features/roles/roles-api.test.ts src/features/roles/RolesPage.test.tsx src/routes/AppRoutes.test.tsx` passed with 20 tests.
- `cd src/OpenIdentityStack.ManagementWeb; npm test -- src/features/groups/groups-api.test.ts src/features/groups/GroupsPage.test.tsx src/routes/AppRoutes.test.tsx src/lib/admin-api.test.ts` passed with 26 tests.
- `cd src/OpenIdentityStack.ManagementWeb; npm test -- src/features/sessions/sessions-api.test.ts src/features/sessions/SessionsPage.test.tsx src/routes/AppRoutes.test.tsx` passed with 22 tests.
- `cd src/OpenIdentityStack.ManagementWeb; npm test -- src/features/providers/providers-api.test.ts src/features/providers/ProvidersPage.test.tsx src/routes/AppRoutes.test.tsx` passed with 26 tests.
- `cd src/OpenIdentityStack.ManagementWeb; npm test -- src/features/settings/settings-api.test.ts src/features/settings/SettingsPage.test.tsx src/routes/AppRoutes.test.tsx` passed with 26 tests.
- `cd src/OpenIdentityStack.ManagementWeb; npm test -- src/features/application-permissions/application-permissions-api.test.ts src/features/application-permissions/ApplicationPermissionsPage.test.tsx src/routes/AppRoutes.test.tsx` passed with 30 tests.
- `dotnet test --project tests\OpenIdentityStack.Application.Tests\OpenIdentityStack.Application.Tests.csproj --no-restore -- --filter-class OpenIdentityStack.Application.Tests.Audit.ListAuditEntriesQueryHandlerTests` passed with 2 tests.
- `dotnet test --project tests\OpenIdentityStack.Infrastructure.Tests\OpenIdentityStack.Infrastructure.Tests.csproj --no-restore -- --filter-class OpenIdentityStack.Infrastructure.Tests.Audit.AuditEntryReaderTests` passed with 2 tests.
- `dotnet test --project tests\OpenIdentityStack.Contract.Tests\OpenIdentityStack.Contract.Tests.csproj --no-restore -- --filter-class OpenIdentityStack.Contract.Tests.Admin.Audit.AuditEntriesEndpointContractTests` passed with 2 tests.
- `dotnet test --project tests\OpenIdentityStack.Api.Tests\OpenIdentityStack.Api.Tests.csproj --no-restore -- --filter-class OpenIdentityStack.Api.Tests.Admin.Audit.AuditEntriesEndpointWorkflowTests` passed with 4 tests.
- `cd src/OpenIdentityStack.ManagementWeb; npm test -- src/features/audit/audit-entries-api.test.ts src/features/audit/AuditEntriesPage.test.tsx` passed with 3 tests.
- `cd src/OpenIdentityStack.ManagementWeb; npm run type-check` passed.
- `dotnet test --project tests\OpenIdentityStack.ManagementWeb.E2ETests\OpenIdentityStack.ManagementWeb.E2ETests.csproj --no-restore -- --filter-class OpenIdentityStack.ManagementWeb.E2ETests.AuditEntryManagementTests` passed with 1 test.
- `dotnet test --project tests\OpenIdentityStack.ManagementWeb.E2ETests\OpenIdentityStack.ManagementWeb.E2ETests.csproj --no-restore -- --filter-namespace OpenIdentityStack.ManagementWeb.E2ETests` passed with 10 tests.

**Next continuation point**:

- The planned ManagementWeb parity foundation tasks are complete. Continue with branch-level hardening, PR review feedback, full CI validation, or any new parity gaps discovered during operator smoke testing.

## Phase 1: Existing Baseline

**Purpose**: Record the already-completed ManagementWeb scaffold so new work starts from the current repository state.

- [X] T001 [P] Create ManagementWeb Vite/Mantine app scaffold in `src/OpenIdentityStack.ManagementWeb/`
- [X] T002 Add ManagementWeb Aspire resource and runtime wiring in `src/OpenIdentityStack.AppHost/AppHost.cs`
- [X] T003 [P] Add ManagementWeb E2E test project scaffolding in `tests/OpenIdentityStack.ManagementWeb.E2ETests/`
- [X] T004 [P] Add initial shell, theme, auth, admin API helper, Users slice, and cross-UI rollout coverage in `src/OpenIdentityStack.ManagementWeb/`, `tests/OpenIdentityStack.ManagementWeb.E2ETests/`, `docs/management-web.md`, and `deploy/management-web.md`

---

## Phase 2: Shared Foundation Parity (Blocking)

**Purpose**: Build the shared Mantine foundation required by all parity slices.

**Critical**: No new vertical slice should be marked complete until this phase is done.

### Tests

- [X] T005 [P] [Foundation] Add API client tests for token injection, query parameters, normalized Problem Details errors, 204 handling, and 401 logout behavior in `src/OpenIdentityStack.ManagementWeb/src/lib/admin-api.test.ts`
- [X] T006 [P] [Foundation] Add permission helper tests for exact permissions, `*`, resource wildcards, missing permissions, and case handling in `src/OpenIdentityStack.ManagementWeb/src/lib/permissions.test.ts`
- [X] T006A [Foundation] Add granular OIDC/profile/access-token permission extraction tests in `src/OpenIdentityStack.ManagementWeb/src/lib/auth-claims.test.ts`
- [X] T007 [P] [Foundation] Add route guard and access-denied tests in `src/OpenIdentityStack.ManagementWeb/src/lib/auth.test.tsx` or `src/OpenIdentityStack.ManagementWeb/src/routes/AppRoutes.test.tsx`
- [X] T008 [P] [Foundation] Add shared Mantine table, confirm dialog, one-time secret display, and API error display component tests under `src/OpenIdentityStack.ManagementWeb/src/components/`
- [X] T009 [P] [Foundation] Extend cross-UI sign-in continuity E2E coverage in `tests/OpenIdentityStack.ManagementWeb.E2ETests/AuthContinuityTests.cs`

### Implementation

- [X] T010 [Foundation] Replace the lightweight fetch helper with a parity-grade ManagementWeb admin API client in `src/OpenIdentityStack.ManagementWeb/src/lib/admin-api.ts`
- [X] T011 [Foundation] Add normalized API error formatting and validation error helpers in `src/OpenIdentityStack.ManagementWeb/src/lib/api-errors.ts`
- [X] T012 [Foundation] Normalize permission constants, wildcard handling, and route/action permission matrices in `src/OpenIdentityStack.ManagementWeb/src/lib/permissions.ts`
- [X] T012A [Foundation] Normalize auth claim extraction from OIDC profile data and decoded access-token payloads without role-name wildcard elevation in `src/OpenIdentityStack.ManagementWeb/src/lib/auth-claims.ts` and `src/OpenIdentityStack.ManagementWeb/src/lib/auth.tsx`
- [X] T013 [Foundation] Add route guards, access-denied state, and 401 logout integration in `src/OpenIdentityStack.ManagementWeb/src/lib/auth.tsx` and `src/OpenIdentityStack.ManagementWeb/src/routes/AppRoutes.tsx`
- [X] T014 [P] [Foundation] Add shared Mantine data table, pagination, empty/loading/error, and filter primitives in `src/OpenIdentityStack.ManagementWeb/src/components/`
- [X] T015 [P] [Foundation] Add shared Mantine confirm dialog, destructive-action dialog, form section, and action bar primitives in `src/OpenIdentityStack.ManagementWeb/src/components/`
- [X] T016 [P] [Foundation] Add one-time secret display/copy primitive in `src/OpenIdentityStack.ManagementWeb/src/components/SecretDisplay.tsx`
- [X] T017 [Foundation] Update ManagementWeb navigation to remove Service Accounts, avoid Clients, add Applications, Permissions, Sessions, Identity providers, Settings, and Audit in `src/OpenIdentityStack.ManagementWeb/src/components/Navigation.tsx`
- [X] T018 [Foundation] Preserve Management Web-compatible route paths for existing domains in `src/OpenIdentityStack.ManagementWeb/src/routes/AppRoutes.tsx`

**Checkpoint**: Shared foundation is ready for parity slices.

---

## Phase 3: Applications Slice (Priority P1)

**Goal**: Port Management Web Applications behavior to ManagementWeb using only `/api/admin/applications`.

### Tests

- [X] T019 [P] [Applications] Add Applications API client tests for list filters, profile policies, create, metadata update, OAuth configure, lifecycle, credentials, and credential revoke in `src/OpenIdentityStack.ManagementWeb/src/features/applications/applications-api.test.ts`
- [X] T020 [P] [Applications] Add component tests for application list, policy-driven form defaults, detail tabs/sections, credential management, public-client credential blocking, and one-time secret display in `src/OpenIdentityStack.ManagementWeb/src/features/applications/`
- [X] T021 [P] [Applications] Add ManagementWeb Applications E2E coverage in `tests/OpenIdentityStack.ManagementWeb.E2ETests/ApplicationManagementTests.cs`
- [X] T022 [P] [Applications] Add navigation regression test proving Clients and Service Accounts are not exposed in `src/OpenIdentityStack.ManagementWeb/src/routes/AppRoutes.test.tsx`

### Implementation

- [X] T023 [Applications] Add Applications API client and TanStack Query hooks in `src/OpenIdentityStack.ManagementWeb/src/features/applications/`
- [X] T024 [Applications] Implement the Applications list with search, pagination, profile/status/client-type filters, status display, and row actions in `src/OpenIdentityStack.ManagementWeb/src/features/applications/ApplicationsPage.tsx`
- [X] T025 [Applications] Implement create/edit forms with policy-driven profile behavior in `src/OpenIdentityStack.ManagementWeb/src/features/applications/ApplicationForm.tsx`
- [X] T026 [Applications] Implement application detail, OAuth configuration, lifecycle actions, delete confirmation, and credential sections in `src/OpenIdentityStack.ManagementWeb/src/features/applications/`
- [X] T027 [Applications] Wire `/applications`, `/applications/new`, `/applications/:id`, and `/applications/:id/edit` routes in `src/OpenIdentityStack.ManagementWeb/src/routes/AppRoutes.tsx`

**Checkpoint**: Applications parity complete.

---

## Phase 4: Users Slice Refactor (Priority P1)

**Goal**: Refactor the existing partial Users slice into the shared foundation and complete Management Web parity.

### Tests

- [X] T028 [P] [Users] Expand Users API client tests for create, update, delete, enable/disable, reset password, roles, groups, and upstream identities in `src/OpenIdentityStack.ManagementWeb/src/features/users/`
- [X] T029 [P] [Users] Expand Users component tests for list, create/edit forms, detail, role assignment, group display, upstream identities, reset password, and permission-gated actions in `src/OpenIdentityStack.ManagementWeb/src/features/users/`
- [X] T030 [P] [Users] Expand ManagementWeb Users E2E coverage in `tests/OpenIdentityStack.ManagementWeb.E2ETests/UserManagementTests.cs`

### Implementation

- [X] T031 [Users] Refactor Users API calls and mutations out of `src/OpenIdentityStack.ManagementWeb/src/lib/admin-api.ts` into `src/OpenIdentityStack.ManagementWeb/src/features/users/`
- [X] T032 [Users] Replace Users page local table/form/dialog patterns with shared foundation components in `src/OpenIdentityStack.ManagementWeb/src/features/users/`
- [X] T033 [Users] Add missing Management Web-equivalent Users workflows for create, edit, delete, enable/disable, reset password, roles, groups, and upstream identities in `src/OpenIdentityStack.ManagementWeb/src/features/users/`
- [X] T034 [Users] Wire `/users`, `/users/create`, `/users/:id`, and `/users/:id/edit` routes in `src/OpenIdentityStack.ManagementWeb/src/routes/AppRoutes.tsx`

**Checkpoint**: Users parity complete.

---

## Phase 5: Roles Slice (Priority P2)

### Tests

- [X] T035 [P] [Roles] Add Roles API client and component tests for list, create, detail, update, delete, system role behavior, wildcard acknowledgement, and permission selector/catalog in `src/OpenIdentityStack.ManagementWeb/src/features/roles/`
- [X] T036 [P] [Roles] Add ManagementWeb Roles E2E coverage in `tests/OpenIdentityStack.ManagementWeb.E2ETests/RoleManagementTests.cs`

### Implementation

- [X] T037 [Roles] Port Roles API client, hooks, list, form, detail, badges, and permission selector to Mantine in `src/OpenIdentityStack.ManagementWeb/src/features/roles/`
- [X] T038 [Roles] Wire `/roles`, `/roles/new`, and `/roles/:id` routes in `src/OpenIdentityStack.ManagementWeb/src/routes/AppRoutes.tsx`

**Checkpoint**: Roles parity complete. The ManagementWeb Roles slice uses `/api/admin/roles` and `/api/admin/permissions/platform`, preserves `/roles`, `/roles/new`, and `/roles/:id`, enforces wildcard acknowledgement in the form, hides delete for system roles, and covers the workflow with Vitest plus .NET/xUnit Playwright E2E.

---

## Phase 6: Groups Slice (Priority P2)

### Tests

- [X] T039 [P] [Groups] Add Groups API client and component tests for list, create, edit, delete, members, and mappings in `src/OpenIdentityStack.ManagementWeb/src/features/groups/`
- [X] T040 [P] [Groups] Add ManagementWeb Groups E2E coverage in `tests/OpenIdentityStack.ManagementWeb.E2ETests/GroupManagementTests.cs`

### Implementation

- [X] T041 [Groups] Port Groups API client, hooks, list, forms, detail, members, mappings, and dialogs to Mantine in `src/OpenIdentityStack.ManagementWeb/src/features/groups/`
- [X] T042 [Groups] Wire `/groups`, `/groups/new`, `/groups/:id`, and `/groups/:id/edit` routes in `src/OpenIdentityStack.ManagementWeb/src/routes/AppRoutes.tsx`

**Checkpoint**: Groups parity complete. The ManagementWeb Groups slice uses `/api/admin/groups`, `/api/admin/users`, and `/api/admin/roles` for group CRUD, member selection, and role mapping selection; preserves `/groups`, `/groups/new`, `/groups/:id`, and `/groups/:id/edit`; gates write/delete/member-management actions with granular permissions; removes no legacy Clients or Service Accounts surface; and covers the workflow with Vitest plus .NET/xUnit Playwright E2E.

---

## Phase 7: Sessions Slice (Priority P2)

### Tests

- [X] T043 [P] [Sessions] Add Sessions API client and component tests for list, detail, revoke one session, and revoke all user sessions in `src/OpenIdentityStack.ManagementWeb/src/features/sessions/`
- [X] T044 [P] [Sessions] Add ManagementWeb Sessions E2E coverage in `tests/OpenIdentityStack.ManagementWeb.E2ETests/SessionManagementTests.cs`

### Implementation

- [X] T045 [Sessions] Port Sessions API client, hooks, list, detail, status badges, and revoke dialogs to Mantine in `src/OpenIdentityStack.ManagementWeb/src/features/sessions/`
- [X] T046 [Sessions] Wire `/sessions` and `/sessions/:id` routes in `src/OpenIdentityStack.ManagementWeb/src/routes/AppRoutes.tsx`

**Checkpoint**: Sessions parity complete. The ManagementWeb Sessions slice preserves `/sessions` and `/sessions/:id`, uses `GET /api/admin/sessions`, `GET /api/admin/sessions/{id}`, `DELETE /api/admin/sessions/{id}`, and the current backend `DELETE /api/admin/users/{userId}/sessions` contract for revoking all sessions for a user. It gates destructive actions with `sessions:revoke`, keeps `sessions:read` route access, and covers list/search/status filter/detail/revoke workflows with Vitest plus .NET/xUnit Playwright E2E.

---

## Phase 8: Providers Slice (Priority P2)

### Tests

- [X] T047 [P] [Providers] Add Providers API client and component tests for list, create, edit, delete, enable/disable, and OIDC settings in `src/OpenIdentityStack.ManagementWeb/src/features/providers/`
- [X] T048 [P] [Providers] Add ManagementWeb Providers E2E coverage in `tests/OpenIdentityStack.ManagementWeb.E2ETests/ProviderManagementTests.cs`

### Implementation

- [X] T049 [Providers] Port Providers API client, hooks, list, forms, detail, status/type badges, and dialogs to Mantine in `src/OpenIdentityStack.ManagementWeb/src/features/providers/`
- [X] T050 [Providers] Wire `/providers`, `/providers/new`, `/providers/:id`, and `/providers/:id/edit` routes in `src/OpenIdentityStack.ManagementWeb/src/routes/AppRoutes.tsx`

**Checkpoint**: Providers parity complete. The ManagementWeb Providers slice preserves `/providers`, `/providers/new`, `/providers/:id`, and `/providers/:id/edit`, uses `GET /api/admin/providers?includeDisabled=true`, `GET /api/admin/providers/{id}`, `POST /api/admin/providers`, `PATCH /api/admin/providers/{id}`, `POST /api/admin/providers/{id}/enable`, `POST /api/admin/providers/{id}/disable`, and `DELETE /api/admin/providers/{id}`. It gates create/edit/status changes with `providers:write`, deletion with `providers:delete`, route access with `providers:read`, keeps the OIDC-only type display, and covers list/search/create/detail/edit/enable/disable/delete workflows with Vitest plus .NET/xUnit Playwright E2E.

---

## Phase 9: Settings Slice (Priority P2)

### Tests

- [X] T051 [P] [Settings] Add Settings API client and component tests for authentication settings, default provider, and local fallback in `src/OpenIdentityStack.ManagementWeb/src/features/settings/`
- [X] T052 [P] [Settings] Add ManagementWeb Settings E2E coverage in `tests/OpenIdentityStack.ManagementWeb.E2ETests/SettingsManagementTests.cs`

### Implementation

- [X] T053 [Settings] Port authentication settings API client, hooks, and form to Mantine in `src/OpenIdentityStack.ManagementWeb/src/features/settings/`
- [X] T054 [Settings] Wire `/settings` route in `src/OpenIdentityStack.ManagementWeb/src/routes/AppRoutes.tsx`

**Checkpoint**: Settings parity slice is complete.

---

## Phase 10: Application Permissions Slice (Priority P2)

### Tests

- [X] T055 [P] [ApplicationPermissions] Add API client and component tests for registered applications, register/import, detail, ownership, maintainers, manifest preview/apply, catalog, history, and diagnostics in `src/OpenIdentityStack.ManagementWeb/src/features/application-permissions/`
- [X] T056 [P] [ApplicationPermissions] Add ManagementWeb Application Permissions E2E coverage in `tests/OpenIdentityStack.ManagementWeb.E2ETests/ApplicationPermissionsManagementTests.cs`

### Implementation

- [X] T057 [ApplicationPermissions] Port Application Permissions API client, hooks, list, register form, detail, ownership/maintainer controls, manifest workflows, diagnostics, and history to Mantine in `src/OpenIdentityStack.ManagementWeb/src/features/application-permissions/`
- [X] T058 [ApplicationPermissions] Wire `/application-permissions`, `/application-permissions/new`, and `/application-permissions/:id` routes in `src/OpenIdentityStack.ManagementWeb/src/routes/AppRoutes.tsx`

**Checkpoint**: Application Permissions parity slice is complete. The ManagementWeb Application Permissions slice preserves `/application-permissions`, `/application-permissions/new`, and `/application-permissions/:id`, uses `/api/admin/application-permissions` registry, catalog, history, diagnostics, ownership, maintainer, lifecycle, import, and manifest endpoints, gates route access with `application-permissions:read`, write actions with `application-permissions:write`, ownership/maintainer controls with `application-permissions:admin` or write access, and covers list/register/import/detail/ownership/maintainer/permission/catalog/history/diagnostics workflows with Vitest plus .NET/xUnit Playwright E2E.

---

## Phase 11: Audit Slice (Priority P2)

**Goal**: Add ManagementWeb Audit backed by one read-only `/api/admin/audit-entries` endpoint.

### Tests

- [X] T059 [P] [Audit] Add application/infrastructure tests for audit entry pagination and filters in `tests/OpenIdentityStack.Application.Tests/` and `tests/OpenIdentityStack.Infrastructure.Tests/`
- [X] T060 [P] [Audit] Add API tests for `GET /api/admin/audit-entries`, `audit-logs:read` authorization, pagination, filters, and search in `tests/OpenIdentityStack.Api.Tests/Admin/Audit/`
- [X] T061 [P] [Audit] Add contract tests for the audit entries response shape including `details`, `beforeState`, and `afterState` in `tests/OpenIdentityStack.Contract.Tests/Admin/Audit/`
- [X] T062 [P] [Audit] Add ManagementWeb Audit API client and component tests for filters, pagination, permission denial, and expandable row details in `src/OpenIdentityStack.ManagementWeb/src/features/audit/`
- [X] T063 [P] [Audit] Add ManagementWeb Audit E2E coverage in `tests/OpenIdentityStack.ManagementWeb.E2ETests/AuditEntryManagementTests.cs`

### Implementation

- [X] T064 [Audit] Add audit query contracts and handler in `src/OpenIdentityStack.Application/Audit/Queries/`
- [X] T065 [Audit] Add audit repository/query implementation over `AuditLogEntries` in `src/OpenIdentityStack.Infrastructure/Audit/`
- [X] T066 [Audit] Add `GET /api/admin/audit-entries` endpoint requiring `audit-logs:read` in `src/OpenIdentityStack.Api/Audit/AuditEntriesApi.cs` and map it from `src/OpenIdentityStack.Api/Program.cs`
- [X] T067 [Audit] Add ManagementWeb Audit API client, hooks, list, filters, pagination, and expandable row detail in `src/OpenIdentityStack.ManagementWeb/src/features/audit/`
- [X] T068 [Audit] Wire `/audit-entries` route in `src/OpenIdentityStack.ManagementWeb/src/routes/AppRoutes.tsx`

**Checkpoint**: Audit slice is complete. The backend exposes one read-only `GET /api/admin/audit-entries` endpoint backed by existing `AuditLogEntries`, requires `audit-logs:read`, supports page/pageSize plus date, user, action, entity, and search filters, and includes `details`, `beforeState`, and `afterState` in v1 list responses. ManagementWeb preserves `/audit-entries`, gates access with `audit-logs:read`, renders a Mantine audit list with filters, pagination, and expandable entry details, and covers the workflow with application/infrastructure/API/contract tests, Vitest, and .NET/xUnit Playwright E2E.

---

## Phase 12: Overview, Documentation, and Final Verification

### Tests

- [X] T069 [P] [Overview] Add Overview/dashboard component tests and E2E smoke coverage in `src/OpenIdentityStack.ManagementWeb/src/features/overview/` and `tests/OpenIdentityStack.ManagementWeb.E2ETests/OverviewSmokeTests.cs`

### Implementation

- [X] T070 [Overview] Add ManagementWeb Overview/dashboard quick links and aggregate status in `src/OpenIdentityStack.ManagementWeb/src/features/overview/`
- [X] T071 [Docs] Update ManagementWeb docs, screenshots/checklists, rollout guidance, and Management Web decommission criteria in `docs/management-web.md`, `deploy/management-web.md`, and `src/OpenIdentityStack.ManagementWeb/README.md`
- [X] T072 [Validation] Run the validation commands from `specs/007-management-web/quickstart.md` and fix issues discovered

**Checkpoint**: Overview, documentation, and final validation are complete for the current ManagementWeb parity foundation. `/` now renders the ManagementWeb Overview with permission-aware quick links and aggregate available/unavailable section status, preserving all existing domain route paths. Documentation now records parity scope, audit behavior, rollout/rollback guidance, and Management Web decommission criteria. Validation evidence:

- `npm run type-check` passed.
- `npm run build` passed.
- `npm run lint` passed with existing warnings only.
- `npm test -- src/features/overview/OverviewPage.test.tsx src/routes/AppRoutes.test.tsx src/features/audit/audit-entries-api.test.ts src/features/audit/AuditEntriesPage.test.tsx` passed: 32 tests.
- `dotnet test --project tests\OpenIdentityStack.ManagementWeb.E2ETests\OpenIdentityStack.ManagementWeb.E2ETests.csproj --no-restore -- --filter-class OpenIdentityStack.ManagementWeb.E2ETests.OverviewSmokeTests` passed: 1 test.
- `dotnet test --project tests\OpenIdentityStack.ManagementWeb.E2ETests\OpenIdentityStack.ManagementWeb.E2ETests.csproj --no-restore -- --filter-class OpenIdentityStack.ManagementWeb.E2ETests.AuditEntryManagementTests` passed: 1 test.
- `dotnet test --project tests\OpenIdentityStack.Application.Tests\OpenIdentityStack.Application.Tests.csproj --no-restore -- --filter-class OpenIdentityStack.Application.Tests.Audit.ListAuditEntriesQueryHandlerTests` passed: 2 tests.
- `dotnet test --project tests\OpenIdentityStack.Infrastructure.Tests\OpenIdentityStack.Infrastructure.Tests.csproj --no-restore -- --filter-class OpenIdentityStack.Infrastructure.Tests.Audit.AuditEntryReaderTests` passed: 2 tests.
- `dotnet test --project tests\OpenIdentityStack.Contract.Tests\OpenIdentityStack.Contract.Tests.csproj --no-restore -- --filter-class OpenIdentityStack.Contract.Tests.Admin.Audit.AuditEntriesEndpointContractTests` passed: 2 tests.
- `dotnet test --project tests\OpenIdentityStack.Api.Tests\OpenIdentityStack.Api.Tests.csproj --no-restore -- --filter-class OpenIdentityStack.Api.Tests.Admin.Audit.AuditEntriesEndpointWorkflowTests` passed: 4 tests.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1**: Already complete baseline.
- **Phase 2**: Blocks all parity slices.
- **Applications**: First parity slice after foundation.
- **Users**: Refactor after foundation; may proceed in parallel with later slices only if file ownership is isolated.
- **Roles**: Should precede Groups and Application Permissions because permission selector/catalog behavior is reused.
- **Groups, Sessions, Providers, Settings**: Can proceed after foundation and Roles where their permission dependencies are clear.
- **Application Permissions**: Should follow Roles and shared principal search patterns.
- **Audit**: Requires backend endpoint plus frontend slice; can proceed after foundation.
- **Overview/Docs/Validation**: Final integration phase.

### Parallel Opportunities

- Foundation tests T005-T009 can run in parallel.
- Foundation component primitives T014-T016 can run in parallel after API/auth contracts are stable.
- Independent frontend slices can be assigned to separate agents when file ownership does not overlap.
- Audit backend tasks T059-T066 can proceed in parallel with a frontend slice after the endpoint contract is stable.

### Slice Completion Rule

Do not mark a slice complete until:

- behavior parity or explicit deviation is documented;
- tests for the slice pass;
- E2E coverage exists for critical operator workflows;
- route and navigation wiring are complete;
- permission-gated actions use the shared matrix;
- validation commands relevant to the slice have been run.

