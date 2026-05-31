# Feature Specification: Management Web Foundation

**Feature Branch**: `008-management-web`

**Created**: 2026-05-30

**Status**: Verified

**Input**: User description: "what we just grilled into the context.md"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Operate users in new management UI (Priority: P1)

An operator signs in to the new Management Web and completes core user lifecycle operations, including assigning existing roles, without needing to switch to another domain.

**Why this priority**: This is the first production slice and the default-cutover trigger for moving daily operations to the new UI.

**Independent Test**: Can be fully tested by creating, updating, disabling, and assigning roles to users from Management Web while AdminWeb remains available.

**Acceptance Scenarios**:

1. **Given** an authorized operator, **When** they open the Users area, **Then** they can list and search users and open user details.
2. **Given** an authorized operator, **When** they create or update a user and assign existing roles, **Then** the changes persist and are visible in subsequent views.
3. **Given** an unauthorized operator, **When** they attempt privileged user actions, **Then** the UI blocks access and shows a clear permission message.

---

### User Story 2 - Use reliable light/dark appearance controls (Priority: P2)

An operator can choose light, dark, or system appearance and have that preference applied consistently across sessions.

**Why this priority**: Theme control is part of the quality baseline for a professional management experience and was explicitly requested.

**Independent Test**: Can be tested by switching appearance modes, reloading, and signing in again to verify preference persistence and fallback behavior.

**Acceptance Scenarios**:

1. **Given** first load with no saved preference, **When** the app starts, **Then** it follows the operator's system appearance.
2. **Given** a saved preference, **When** the operator returns, **Then** the same appearance mode is applied automatically.
3. **Given** preference storage is unavailable, **When** the app loads, **Then** it safely falls back to system appearance and remains usable.

---

### User Story 3 - Run dual UI rollout without operator disruption (Priority: P3)

Product and operations teams can run AdminWeb and Management Web side by side on separate hostnames, with single sign-on continuity between UIs.

**Why this priority**: Parallel operation is the agreed rollout strategy and is required before eventual AdminWeb decommission.

**Independent Test**: Can be tested by navigating between both UIs in one authenticated session and validating separate entry points remain available.

**Acceptance Scenarios**:

1. **Given** an active identity-provider session, **When** an operator moves from one UI to the other, **Then** they are not asked to log in again.
2. **Given** both UIs are deployed, **When** an operator accesses either hostname, **Then** each UI loads independently and can be operated.

---

### Edge Cases

- What happens when token renewal fails during an in-progress edit? The UI preserves unsaved input where possible and prompts re-authentication without silent data loss.
- How does the system handle Admin API outages? Action-level errors are shown near the failed action, and the rest of the page remains operable where possible.
- What happens when one UI is unavailable during parallel rollout? Operators can still use the available UI and complete supported tasks.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a new Management Web operator interface that runs in parallel with AdminWeb.
- **FR-002**: The system MUST keep both UIs available on separate hostnames during the transition period.
- **FR-003**: Operators MUST be able to complete core user lifecycle actions in Management Web, including assigning users to existing roles.
- **FR-004**: The system MUST enforce the same authorization semantics in both UIs, with backend policy decisions remaining authoritative.
- **FR-005**: The system MUST provide light, dark, and system appearance modes in Management Web.
- **FR-006**: The system MUST persist Management Web appearance preference and apply it on subsequent sessions.
- **FR-007**: The system MUST allow operators to move between AdminWeb and Management Web without re-authentication when an identity-provider session is already active.
- **FR-008**: The system MUST surface recoverable operation failures close to the initiating action and reserve global error handling for application-breaking failures.
- **FR-009**: The system MUST support silent session renewal attempts before interactive re-authentication.
- **FR-010**: The system MUST include top-level navigation placeholders for management domains beyond Users during phase 1.
- **FR-011**: The system MUST maintain independent deployability and observability for AdminWeb and Management Web.
- **FR-012**: The system MUST support coordinated cross-component releases when management API changes are not backward compatible.

### Key Entities *(include if feature involves data)*

- **Management Web**: New operator-facing management interface with its own availability, rollout lifecycle, and quality baseline.
- **Theme Preference**: Operator appearance selection (`light`, `dark`, `system`) with first-load system fallback and persisted reuse.
- **Management Web Client**: Dedicated authentication client identity for the new UI with independent redirect/scope policy.
- **Users Vertical Slice**: Phase-1 capability boundary covering user lifecycle operations and assignment to existing roles.
- **Parallel UI Rollout State**: Operational state in which both UIs are live and operators may use either entry point.

## Security & Operational Impact *(mandatory)*

- **Authentication/Authorization**: Both UIs use the same identity and permission model; Management Web uses its own client identity while backend authorization remains the source of truth.
- **Secrets & Certificates**: Management Web introduces a separate client registration and corresponding configuration; no new signing key model is introduced.
- **Audit Events**: Privileged user-management actions remain auditable through existing backend audit mechanisms; phase 1 does not require a dedicated in-UI audit history surface.
- **Safe Failure Modes**: Errors avoid sensitive detail exposure, block unauthorized operations, and provide action-local recovery guidance.
- **Operations**: Each UI has independent health and telemetry signals; rollout and rollback may require deployment/configuration changes rather than an instant runtime toggle.

## Test Strategy *(mandatory)*

- **Unit Tests**: Validate Management Web domain interaction logic, theme preference behavior, and action-level error handling.
- **API/Integration Tests**: Validate user lifecycle and role-assignment workflows through existing admin endpoints with Management Web client configuration.
- **Contract Tests**: Validate shared admin API client contracts used by both UIs so response/permission behavior remains aligned.
- **AdminWeb Tests**: Preserve existing AdminWeb coverage and add dedicated Management Web end-to-end coverage for Users flows and cross-UI sign-in continuity.
- **Validation Commands**: `dotnet test`; `cd src/OpenIdentityStack.AdminWeb; npm test`; `cd src/OpenIdentityStack.ManagementWeb; npm test`.

## Documentation & Deployment Impact *(mandatory)*

- **Documentation**: Update domain language, rollout guidance, and UI operation docs for parallel operation and eventual AdminWeb decommission criteria.
- **Deployment**: Add/maintain independent deploy configuration for both UIs, including separate hostnames, client settings, and release-train coordination for breaking changes.
- **Screenshots**: Required for Management Web user workflows and theme modes in release-related change documentation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In production-like testing, 95% of operators complete a standard user create-and-role-assign flow in under 2 minutes.
- **SC-002**: During the first 30 days after users-slice release, 95% of initial Users-page loads complete in under 2 seconds.
- **SC-003**: During the first 30 days after users-slice release, 95% of common table interactions complete in under 300 milliseconds.
- **SC-004**: At least 90% of operators complete their first assigned users workflow in Management Web without support intervention.
- **SC-005**: Cross-UI sign-in continuity succeeds for at least 99% of tested transitions between AdminWeb and Management Web.

## Assumptions

- Management Web phase 1 is limited to single-user workflows and excludes bulk operations.
- English is the only required phase-1 content language, while localization extensibility is prepared.
- AdminWeb remains available during transition and is decommissioned only after full domain coverage, quality readiness, and 30 days of stable operation.
- API changes that are not backward compatible will be delivered through a coordinated release train across API and both management UIs.
