# Feature Specification: Service/API Permission Registry

**Feature Branch**: `copilot/add-permission-registration-feature`  
**Created**: 2026-04-28  
**Status**: Implemented and code-aligned  
**Input**: User description: "Currently permissions are hard coded. But the Open Id module should be usable within various service based systems. We need a way to register services/API's and the permissions they expose. The spec should cover registering services/APIs, declaring and updating exposed permissions, validation/ownership/security expectations, how registered permissions relate to RBAC/admin APIs, and key user scenarios/acceptance criteria."

**Implementation Note**: The codebase now implements the registry as Minimal APIs under `/api/admin/service-permissions`, EF Core aggregate persistence, role dependency reads, role-assignment validation against assignable registered permissions, audit logging through the existing audit log abstraction, and AdminWeb registry screens. Built-in platform permissions remain code-defined; service-owned permissions are registered dynamically and exposed through the assignable catalog.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Register a service and its permissions (Priority: P1)

A service owner registers a service or API with the Open Id module and declares the permissions that the service exposes so that identity and authorization administrators can manage those permissions without code changes.

**Why this priority**: This removes hard-coded permissions and provides the minimum usable capability for service-based systems.

**Independent Test**: Can be fully tested by registering a new service with multiple permissions, then verifying the service and all declared permissions are visible for administrative and RBAC assignment workflows.

**Acceptance Scenarios**:

1. **Given** an authenticated service owner with permission to manage a service, **When** they register a new service with a unique service identifier, display name, owner, and at least one permission, **Then** the service is recorded and its active permissions become available for RBAC and administrative use.
2. **Given** a registered service, **When** an administrator views the service catalog, **Then** the service appears with its current permission list, ownership information, status, and last update details.
3. **Given** a service registration request contains duplicate permission keys within the same service, **When** the request is submitted, **Then** the registration is rejected with clear validation errors and no partial registration is created.

---

### User Story 2 - Update exposed permissions over time (Priority: P1)

A service owner updates the permissions exposed by an existing service as that service adds, changes, or retires capabilities, while preserving safe behavior for permissions already used in RBAC assignments.

**Why this priority**: Service permissions evolve after initial registration; safe updates are required to keep RBAC accurate and avoid breaking existing access decisions.

**Independent Test**: Can be fully tested by adding, editing, and attempting to remove permissions for a registered service while observing how existing role assignments and administrative views are affected.

**Acceptance Scenarios**:

1. **Given** a registered service, **When** the owning service owner adds a new valid permission, **Then** the permission is available for future RBAC assignments and is shown in administrative views.
2. **Given** an existing permission already assigned to one or more roles, **When** the service owner updates its display metadata, **Then** existing assignments remain visible and administrators can identify impacted roles.
3. **Given** a permission that is assigned to roles or otherwise in active use, **When** the service owner attempts to remove it, **Then** the removal is blocked unless the permission is first removed from all dependent assignments.
4. **Given** a service owner edits a permission's display name or description without changing its stable key, **When** the update is saved, **Then** RBAC assignments continue to refer to the same permission and administrative users see the updated label.

---

### User Story 3 - Enforce ownership, validation, and security boundaries (Priority: P2)

An organization administrator defines who may register and manage service permissions, and the Open Id module enforces those ownership boundaries and records sensitive changes.

**Why this priority**: A permission registry becomes a security-sensitive source of truth; unauthorized or invalid permission changes can grant or conceal access.

**Independent Test**: Can be fully tested by attempting service registration and permission updates as authorized owners, non-owners, and administrators, then verifying allowed actions, denied actions, and audit visibility.

**Acceptance Scenarios**:

1. **Given** a user who is not a service owner and lacks administrative override rights, **When** they attempt to change a service registration or permission, **Then** the action is denied and the existing registration remains unchanged.
2. **Given** an organization administrator with override rights, **When** they transfer service ownership or disable a service, **Then** the change is applied, recorded, and visible to service owners and administrators.
3. **Given** a registration or permission update is accepted, denied, or fails validation, **When** the outcome is finalized, **Then** the system records who attempted the action, what changed or was rejected, when it happened, and the result.

---

### User Story 4 - Use registered permissions in RBAC and admin workflows (Priority: P2)

An administrator uses registered service permissions when defining roles, assigning access, reviewing access, and maintaining RBAC policies across multiple services.

**Why this priority**: The registry delivers business value only when permissions are consumable by existing RBAC and administrative workflows.

**Independent Test**: Can be fully tested by registering permissions for multiple services, assigning those permissions to roles, reviewing assignments, and confirming permissions from unavailable services are handled safely.

**Acceptance Scenarios**:

1. **Given** multiple services have registered permissions, **When** an administrator creates or edits a role, **Then** they can select permissions grouped or filterable by service.
2. **Given** a permission has been removed from the registry while historical assignments still reference it, **When** an administrator reviews roles, **Then** the affected assignments are highlighted with guidance for replacement or removal.
3. **Given** a service is disabled, **When** RBAC policies are evaluated or administered, **Then** its permissions are no longer offered for new assignments and existing assignments are visibly marked as tied to a disabled service.

---

### Edge Cases

- A service identifier or permission key conflicts with an existing service or permission in the same namespace.
- A service registration includes no permissions, malformed permission keys, duplicate keys, missing owner information, or descriptions that exceed allowed limits.
- A service owner tries to change the stable key of a permission that is already assigned to roles.
- Two authorized users submit conflicting updates for the same service or permission at nearly the same time.
- A service is disabled while its permissions remain assigned to active roles.
- A permission is removed from the registry after administrators have already used it in role definitions.
- A service owner loses ownership or leaves the organization while their service still has active permissions.
- A registration or update fails validation after some submitted permissions are valid and others are invalid.
- A user attempts to register a service or permission using names intended to impersonate a system service or another team's service.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST allow authorized service owners or administrators to register a service or API with a stable unique identifier, human-readable name, description, owner, lifecycle status, and declared permissions.
- **FR-002**: The system MUST require each registered service to have an accountable owner or owning group before its permissions can be made available for RBAC use.
- **FR-003**: The system MUST allow authorized service owners or administrators to declare one or more permissions for a registered service, including a stable permission key, display name, description, and intended use.
- **FR-004**: The system MUST validate service identifiers and permission keys for uniqueness, allowed format, reserved words, required fields, and duplicate declarations before accepting a registration or update.
- **FR-005**: The system MUST reject invalid service or permission registrations without creating partial or ambiguous records.
- **FR-006**: The system MUST allow authorized service owners to add new permissions to services they own without requiring changes to the Open Id module's hard-coded permission list.
- **FR-007**: The system MUST allow authorized service owners to update non-identity metadata for permissions they own, such as display name, description, category, and documentation reference.
- **FR-008**: The system MUST prevent changes to stable service identifiers and stable permission keys when those changes would break existing RBAC assignments, audit records, or access reviews.
- **FR-009**: The system MUST treat permissions as defined or absent; permissions MUST NOT have lifecycle states.
- **FR-010**: The system MUST prevent active-use permissions from being removed in a way that silently breaks role assignments, access reviews, or auditability.
- **FR-011**: The system MUST show dependency information before a permission is removed, including impacted roles and administrative assignments.
- **FR-012**: The system MUST distinguish between currently defined permissions and missing permissions retained only through historical, migration, or existing-assignment visibility.
- **FR-013**: The system MUST expose registered permissions from active services to RBAC role creation and role editing workflows so administrators can assign permissions by service.
- **FR-014**: The system MUST ensure RBAC and administrative permission lists use the registry as the source of truth for service-exposed permissions instead of relying on hard-coded service permission definitions.
- **FR-015**: The system MUST clearly indicate missing permissions and permissions from unavailable services in role management, access review, and administrative views.
- **FR-016**: The system MUST enforce ownership boundaries so only service owners, delegated maintainers, or administrators with explicit override rights can modify a service's registration and permissions.
- **FR-017**: The system MUST allow administrators to transfer service ownership, disable a service, or perform emergency changes while preserving an audit trail.
- **FR-018**: The system MUST record audit events for service registration, permission creation, permission update, application status change, ownership change, denied update attempts, and validation failures.
- **FR-019**: The system MUST provide administrators and service owners with searchable and filterable views of registered services and permissions by service, owner, status, and permission key.
- **FR-020**: The system MUST provide clear validation and authorization error messages that explain what failed and what the user can do next, without exposing sensitive information.
- **FR-021**: The system MUST prevent users from registering services or permissions that impersonate protected platform services, reserved permission namespaces, or another owner's service.
- **FR-022**: The system MUST allow service permission updates to be reviewed consistently by administrators, including who requested the change, the before-and-after values, and the current approval or application status when review is required.
- **FR-023**: The system MUST maintain historical visibility of previously registered permissions that have been assigned or audited, even after those permissions are removed from the registry.
- **FR-024**: The system MUST provide a safe way to re-declare an accidentally removed permission when policy permits restoration.

### Key Entities

- **Registered Service/API**: A service, API, or product component that exposes permissions. Key attributes include stable service identifier, display name, description, owner or owning group, lifecycle status, registration date, last updated date, and administrative visibility.
- **Service Permission**: A permission exposed by a registered service. Key attributes include stable permission key, service association, display name, description, intended use, creation date, last updated date, and historical visibility.
- **Service Owner**: A person or group accountable for a registered service's permission declarations and lifecycle decisions.
- **Delegated Maintainer**: A user or group authorized by the service owner or an administrator to manage a registered service's permissions.
- **Administrator**: A privileged user who can oversee registrations, resolve ownership issues, perform policy-compliant overrides, and manage RBAC assignments.
- **Role Assignment Dependency**: A relationship showing where a service permission is used in roles, administrative assignments, access reviews, or other authorization policy records.
- **Audit Event**: A record of registration, update, ownership, lifecycle, authorization, or validation outcomes related to service permissions.

### Assumptions

- The Open Id module already has RBAC and administrative capabilities that can consume a permission catalog.
- A "service" and an "API" are treated as equivalent registry entries unless a future requirement distinguishes them.
- Permission keys are stable identifiers intended for long-term RBAC and audit use; labels and descriptions may change more freely.
- Permissions are either defined by a registered service or absent from the registry; removal must be dependency-aware because role assignments may still reference the historical key.
- Registration and update capabilities are available only to authenticated users with service ownership, delegated maintenance, or administrative authority.
- Review or approval workflows may be required by organizational policy, but the core requirement is that permission changes are attributable, validated, and auditable.
- Stable service identifiers require at least three characters because they define organization-wide namespaces and should not be confused with short platform or team abbreviations.
- Stable permission keys require at least two characters because they are scoped by their parent service identifier and may legitimately use short action names; the combined full permission key remains globally descriptive.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An authorized service owner can register a new service with at least five permissions in under 10 minutes without developer changes to the Open Id module.
- **SC-002**: 100% of accepted permission registrations include a stable service identifier, stable permission key, owner, and audit record.
- **SC-003**: 100% of invalid registration attempts for duplicate keys, missing owners, reserved names, or unauthorized users are rejected without partial registration.
- **SC-004**: Administrators can find and assign active registered permissions to a role in under 2 minutes for services with up to 100 permissions.
- **SC-005**: When a permission is removed from the registry, administrators can identify impacted roles and assignments within 1 minute.
- **SC-006**: Existing RBAC assignments remain visible and attributable for 100% of permissions that have been removed from the registry.
- **SC-007**: At least 95% of service owners in user acceptance testing can complete common permission maintenance tasks without support after reviewing standard guidance.
- **SC-008**: Support or developer requests to add or update hard-coded service permissions are reduced by at least 80% within two release cycles after adoption.
