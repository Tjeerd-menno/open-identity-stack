# Feature Specification: Management Web AdminWeb Parity

**Feature Branch**: `008-management-web`

**Created**: 2026-05-30

**Updated**: 2026-06-02

**Status**: In Progress

**Current implementation checkpoint**: Shared foundation, Applications, Users, Roles, Groups, Sessions, Identity providers, Settings, Application permissions, Audit, and Overview parity slices are complete through 2026-06-02. Next planned work is documentation/final validation.

**Input**: ManagementWeb must reach functional parity with AdminWeb while using a Mantine-first frontend. The port preserves AdminWeb behavior one-for-one before any product redesign, uses the consolidated Applications API only, removes legacy Clients and Service accounts navigation, and adds a ManagementWeb Audit area backed by a read-only audit entries endpoint.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Use a parity-grade ManagementWeb foundation (Priority: P1)

An operator signs in to ManagementWeb and sees the same authentication, authorization, API error, loading, empty-state, confirmation, pagination, and one-time-secret behavior expected from AdminWeb, rendered through Mantine components.

**Why this priority**: Every vertical slice depends on these primitives. Building them first prevents inconsistent permission checks, API errors, dialogs, and table behavior across slices.

**Independent Test**: Component and integration tests prove token injection, 401 logout handling, normalized API errors, permission wildcard behavior, route protection, table pagination, confirm dialogs, and secret display/copy behavior.

**Acceptance Scenarios**:

1. **Given** an authenticated operator, **When** ManagementWeb calls the Admin API, **Then** it attaches the bearer token and normalizes Problem Details errors consistently.
2. **Given** a 401 API response, **When** a protected request fails, **Then** ManagementWeb starts the logout/re-authentication path without losing route protection semantics.
3. **Given** permissions such as `*`, `applications:*`, or concrete grants, **When** the UI evaluates actions and routes, **Then** it uses one normalized permission helper while backend authorization remains authoritative.
4. **Given** a signed-in operator with effective role permissions emitted as concrete token claims, **When** ManagementWeb initializes authentication, **Then** it reads granular `permission`/`permissions`/scope claims from the profile and access-token payload and does not infer access from role names alone.

---

### User Story 2 - Manage consolidated applications (Priority: P1)

An operator manages OAuth/OIDC applications in one Applications area with the same behavior currently implemented in AdminWeb, backed only by `/api/admin/applications`.

**Why this priority**: Applications replace the old split Clients and Service accounts model and are the highest-value strategic domain after the shared foundation.

**Independent Test**: An operator can list, filter, create, inspect, edit, configure OAuth settings, enable/disable, delete, and manage credentials for applications without any Clients or Service accounts UI.

**Acceptance Scenarios**:

1. **Given** application records with multiple profiles, **When** the operator opens Applications, **Then** all profiles appear in one list with filters rather than split navigation.
2. **Given** the operator creates an application, **When** they select a profile, **Then** the form applies profile policy defaults and restrictions from `/api/admin/applications/policies/profiles`.
3. **Given** a confidential application, **When** the operator adds a secret or certificate, **Then** credential metadata updates and one-time secrets are shown only in the immediate response UI.

---

### User Story 3 - Operate users with AdminWeb parity (Priority: P1)

An operator completes the full AdminWeb user workflow in ManagementWeb after the Users slice is refactored onto the shared foundation.

**Why this priority**: Users are already partially implemented but must be normalized into the new parity baseline.

**Independent Test**: An operator can list, search, create, inspect, edit, enable/disable, delete, reset passwords, assign/unassign roles, inspect groups, and link/unlink upstream identities with AdminWeb-equivalent behavior.

**Acceptance Scenarios**:

1. **Given** the current partial Users screen, **When** the slice is ported, **Then** it no longer uses ad hoc permission names or one-off API handling.
2. **Given** an operator has insufficient permission, **When** they attempt privileged user actions, **Then** ManagementWeb hides or disables the action and still handles backend 403s clearly.

---

### User Story 4 - Port remaining AdminWeb domains as vertical slices (Priority: P2)

An operator can use ManagementWeb for Roles, Groups, Sessions, Identity providers, Settings, Application permissions, and Overview with AdminWeb-equivalent behavior.

**Why this priority**: Full parity requires all retained AdminWeb domains, but these can be delivered after foundation, Applications, and Users.

**Independent Test**: Each slice has unit/component coverage plus E2E coverage for operator-critical workflows before it is marked complete.

**Acceptance Scenarios**:

1. **Given** a retained AdminWeb route, **When** the equivalent ManagementWeb route exists, **Then** the route path is preserved wherever the domain still exists.
2. **Given** legacy Clients or Service accounts routes, **When** ManagementWeb navigation is rendered, **Then** neither appears.
3. **Given** the operator manages Roles, **When** they list, create, inspect, update, or delete custom roles, **Then** ManagementWeb uses `/api/admin/roles`, uses `/api/admin/permissions/platform` for platform permission selection, requires wildcard acknowledgement for broad grants, and prevents deleting system roles.
4. **Given** the operator manages Groups, **When** they list, search, create, inspect, update, delete, add/remove members, or add/remove mappings, **Then** ManagementWeb preserves the AdminWeb group routes, uses `/api/admin/groups` plus member and mapping subresources, uses `/api/admin/users` for member selection, uses `/api/admin/roles` for role mapping selection, and gates actions with granular group permissions.
5. **Given** the operator manages Sessions, **When** they list, search, filter by status, inspect details, revoke one session, or revoke all sessions for a user, **Then** ManagementWeb preserves `/sessions` routes, uses the current backend session endpoints, and gates destructive actions with `sessions:revoke`.
6. **Given** the operator manages Identity providers, **When** they list, search, create, inspect, update, enable, disable, or delete an OIDC provider, **Then** ManagementWeb preserves `/providers` routes, uses only `/api/admin/providers` and provider subresources, keeps OIDC as the provider type, and gates actions with granular provider permissions.
7. **Given** the operator manages Settings, **When** they view active authentication providers, change the default provider, or toggle admin local fallback, **Then** ManagementWeb preserves `/settings`, uses `/api/admin/authentication-settings`, and gates access with `system:settings`.
8. **Given** the operator manages Application permissions, **When** they list, register, import, inspect, change lifecycle, transfer ownership, add maintainers, add permissions, view catalog, view history, or view diagnostics, **Then** ManagementWeb preserves `/application-permissions` routes, uses `/api/admin/application-permissions`, and gates access with granular application permission grants.

---

### User Story 5 - View and filter audit trail records (Priority: P2)

An operator with `audit-logs:read` views audit entries in ManagementWeb through a read-only `/api/admin/audit-entries` endpoint.

**Why this priority**: Audit is explicitly required for ManagementWeb and is not currently an AdminWeb screen.

**Independent Test**: An authorized operator can page, filter, search, and expand audit rows showing `details`, `beforeState`, and `afterState`.

**Acceptance Scenarios**:

1. **Given** audit log entries exist, **When** the operator opens Audit, **Then** entries are shown newest-first with page/pageSize pagination.
2. **Given** filter criteria, **When** the operator filters by date range, user id, action, entity type, entity id, or search text, **Then** results update without exposing unauthorized data.
3. **Given** a row with before/after state, **When** the operator expands it, **Then** details, before state, and after state are visible from the list response.

---

### User Story 6 - Run dual UI rollout without operator disruption (Priority: P3)

Product and operations teams run AdminWeb and ManagementWeb side by side on separate hostnames with single sign-on continuity between UIs.

**Why this priority**: Parallel operation is required until ManagementWeb reaches parity and AdminWeb can be decommissioned.

**Independent Test**: Navigating between both UIs in one authenticated session does not require repeated sign-in and each UI remains independently reachable.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: ManagementWeb MUST provide functional parity with retained AdminWeb domains before AdminWeb decommission is considered.
- **FR-002**: ManagementWeb MUST port behavior one-for-one first and redesign only the visual/component layer with Mantine.
- **FR-003**: ManagementWeb MUST use only `/api/admin/applications` for application-like resources.
- **FR-004**: ManagementWeb MUST NOT expose Clients or Service accounts navigation, routes, or legacy endpoint usage.
- **FR-005**: ManagementWeb MUST preserve existing AdminWeb route paths wherever the domain still exists.
- **FR-006**: ManagementWeb MUST normalize permission checks in a shared foundation while treating backend authorization as authoritative. It MUST consume concrete grants from `permission`, `permissions`, `scope`, and `scp` claims in both OIDC profile data and access-token payloads, and MUST NOT elevate privileges from role names alone.
- **FR-007**: ManagementWeb MUST include shared API client, error handling, route protection, permission helpers, table, dialog, loading, empty-state, and one-time-secret primitives before new domain ports.
- **FR-008**: Each vertical slice MUST meet a strict definition of done: UI parity, API client coverage, risky component/unit tests, E2E coverage for operator-critical workflows, permission gates, loading/empty/error states, and validation passing.
- **FR-009**: ManagementWeb MUST provide Applications, Users, Roles, Groups, Sessions, Identity providers, Settings, Application permissions, Audit, and Overview navigation.
- **FR-010**: The system MUST add `GET /api/admin/audit-entries` secured by `audit-logs:read`.
- **FR-011**: The audit entries endpoint MUST support page/pageSize pagination and filters for date range, user id, action, entity type, entity id, and search.
- **FR-012**: The audit entries list response MUST include `details`, `beforeState`, and `afterState` in v1.
- **FR-013**: ManagementWeb MUST keep light, dark, and system appearance modes with persisted preference.
- **FR-014**: ManagementWeb and AdminWeb MUST remain independently deployable on separate hostnames during rollout.

### Key Entities *(include if feature involves data)*

- **ManagementWeb Foundation**: Shared frontend primitives for auth, API access, permissions, routing, tables, dialogs, errors, and secret display.
- **Application Workspace**: Consolidated application management UI using profiles, policy metadata, OAuth configuration, lifecycle actions, and credentials.
- **Audit Entry**: Read-only administrative event with `id`, `timestamp`, `userId`, `action`, `entityType`, `entityId`, `details`, `beforeState`, and `afterState`.
- **Navigation Surface**: Retained ManagementWeb domains and route paths, excluding Clients and Service accounts.
- **Vertical Slice Definition of Done**: Required parity and verification criteria for each domain.

## Security & Operational Impact *(mandatory)*

- **Authentication/Authorization**: ManagementWeb uses the same identity and permission model as AdminWeb, with normalized client-side checks and backend policy as the final authority. UI gates are based on granular permission grants, including concrete permissions expanded from backend roles into token claims; role names are display/context data, not frontend authorization grants.
- **Secrets & Certificates**: One-time secret displays must avoid storing or re-rendering secrets outside the immediate response state.
- **Audit Events**: ManagementWeb adds a read-only audit trail surface and does not mutate audit data.
- **Safe Failure Modes**: Errors avoid sensitive detail exposure, block unauthorized operations, and keep action-local recovery where possible.
- **Operations**: AdminWeb remains available during rollout until ManagementWeb parity, E2E coverage, and stability criteria are met.

## Test Strategy *(mandatory)*

- **Unit/Component Tests**: Shared foundation primitives and risky domain behavior.
- **API/Contract Tests**: Audit entries endpoint and any backend/API shape consumed by ManagementWeb.
- **E2E Tests**: Good coverage for each operator-critical vertical slice, including Applications, Users, Audit, and cross-UI sign-in continuity.
- **AdminWeb Tests**: Existing AdminWeb tests remain the behavior checklist for parity.
- **Validation Commands**: `dotnet build OpenIdentityStack.slnx --no-restore`; focused API/contract tests; `cd src/OpenIdentityStack.ManagementWeb; npm run build && npm run lint && npm test`; ManagementWeb E2E tests.

## Documentation & Deployment Impact *(mandatory)*

- **Documentation**: Update ManagementWeb operator docs, route/domain language, consolidated Applications guidance, Audit usage, and AdminWeb decommission criteria.
- **Deployment**: Keep separate AdminWeb and ManagementWeb hostnames, client ids, environment configuration, health checks, and rollback guidance.
- **Screenshots**: Required for visually significant ManagementWeb slices before parity sign-off.

## Success Criteria *(mandatory)*

- **SC-001**: Every retained AdminWeb domain has a ManagementWeb route and verified parity workflow.
- **SC-002**: No ManagementWeb code calls `/api/admin/clients` or `/api/admin/service-accounts`.
- **SC-003**: Applications, Users, and Audit E2E suites pass in CI-like validation.
- **SC-004**: 95% of common list/search/filter interactions complete in under 300 milliseconds in production-like testing.
- **SC-005**: Cross-UI sign-in continuity succeeds for at least 99% of tested transitions.

## Assumptions

- Existing AdminWeb behavior is the parity source unless explicitly superseded here.
- Audit is read-only in ManagementWeb v1.
- The first implementation sequence is shared foundation, Applications, Users refactor, Roles, Groups, Sessions, Providers, Settings, Application permissions, Audit, then Overview.
