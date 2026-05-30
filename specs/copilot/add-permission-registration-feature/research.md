# Phase 0 Research: Service/API Permission Registry

## Decision: Persist registry entities instead of extending hard-coded permission constants

**Rationale**: The current `OpenIdentityStack.Application.Authorization.Permissions` static class is appropriate for platform/admin permissions but cannot support service-specific permissions without redeployment. Persisting `RegisteredService` and `ServicePermission` records in PostgreSQL lets service owners update service permissions dynamically while preserving validation, search, RBAC consumption, and auditability.

**Alternatives considered**:
- Add more constants to `Permissions`: rejected because every permission change would require code changes and deployment.
- Store arbitrary JSON blobs: rejected because uniqueness, dependency checks, indexing, and auditability would be weak.
- Use OpenIddict scopes as the registry: rejected because OAuth scopes and internal RBAC/admin permission catalog entries have overlapping but distinct purposes.

## Decision: Keep RBAC permission values as stable strings while validating new assignments against the registry

**Rationale**: Existing roles store permissions as strings and `Permissions.Matches` supports exact and wildcard matches. Keeping string keys avoids disruptive migration of role storage and token claim semantics. The registry becomes the source of truth for new service-exposed assignments: permissions defined by active services are assignable; missing permissions or permissions from disabled services remain visible for existing assignments but are blocked or warned for new assignments according to policy.

**Alternatives considered**:
- Replace role permissions with foreign keys immediately: rejected for initial delivery because it risks breaking current roles and authorization behavior.
- Duplicate registry and role data without validation: rejected because the registry would not be authoritative.
- Remove wildcard support: rejected because existing authorization semantics rely on it.

## Decision: Model application lifecycle in the domain and keep permissions defined-or-absent

**Rationale**: Registration, stable identifier immutability, owner/delegation rules, permission uniqueness, application status changes, restoration, and deletion safeguards are domain invariants. The domain should expose explicit lifecycle transitions for applications (`active`, `disabled`, `retired`). Permissions should not expose lifecycle states; each permission is either defined by the application or absent from the registry.

**Alternatives considered**:
- Put rules in API handlers only: rejected because invariants would be duplicated and bypassable.
- Model permissions independently from services only: rejected because service-level uniqueness and ownership are central.
- Allow hard deletion by default: rejected because assigned/audited permissions need historical visibility.

## Decision: Enforce authorization in application use cases and at API boundaries

**Rationale**: API endpoints should require OpenIddict bearer authentication and coarse RBAC permissions. Application use cases should enforce fine-grained service ownership, delegated-maintainer, and administrator override rules so that non-HTTP callers and tests receive consistent enforcement. The domain remains independent from the current authenticated principal.

**Alternatives considered**:
- Endpoint-only authorization: rejected because use cases could be invoked without consistent checks.
- Repository-level authorization: rejected because repositories should persist data, not decide policy.
- Trust service-supplied ownership claims: rejected due to impersonation risk.

## Decision: Audit accepted, denied, and validation-failed attempts

**Rationale**: The registry is a security-sensitive source of truth. Audit records must cover service registration, permission creation/update, lifecycle/status change, ownership transfer, denied update attempts, validation failures, and conflicts. Records should include actor, target, action, result, timestamp, safe before/after values, reason code, and correlation ID.

**Alternatives considered**:
- Audit successful mutations only: rejected because denied/invalid attempts can indicate abuse or misconfiguration.
- Application logs only: rejected because administrators need queryable historical records.
- Store full raw request payloads: rejected because audit data must avoid secrets and unnecessary sensitive data.

## Decision: Protect stable identifiers and reserved namespaces

**Rationale**: Service identifiers and permission keys are long-lived RBAC/audit identifiers. They must be normalized to lowercase invariant form, unique, format-validated, and protected from reserved platform namespaces such as `users`, `roles`, `groups`, `service-accounts`, `sessions`, `providers`, `clients`, `audit-logs`, `system`, and wildcard `*`.

**Alternatives considered**:
- Allow stable key renames with cascades: rejected because cascading across roles, audit records, tokens, and access reviews risks losing traceability.
- Rely only on database constraints: rejected because users need actionable validation before persistence failures.
- Preserve mixed-case identifiers: rejected because existing permission matching is case-insensitive and role permissions are normalized.

## Decision: Query role dependencies before unsafe lifecycle transitions

**Rationale**: FR-010 and FR-011 require preventing silent breakage and showing impacted roles/assignments. Infrastructure should implement an `IRolePermissionDependencyReader` over existing role permission data for exact keys and wildcard-related dependencies. Application lifecycle transitions to disabled/retired and any permission removal operation must surface dependencies and block unsafe destructive changes.

**Alternatives considered**:
- Skip dependency checks: rejected by acceptance criteria.
- Check dependencies asynchronously after the change: rejected because administrators need impact information before approval.
- Maintain snapshots only: rejected because role assignments can change after snapshots are created.

## Decision: Expose REST Minimal APIs under `/api/admin/service-permissions`

**Rationale**: Existing admin APIs are Minimal API route groups under `/api/admin/*`, use OpenAPI/Scalar, and are testable through API/contract tests. REST endpoints for service registration, metadata updates, permission updates, lifecycle changes, dependencies, ownership, and catalog queries fit current patterns.

**Alternatives considered**:
- GraphQL: rejected because the repository's admin surface is REST/OpenAPI.
- Service-self-registration outside admin APIs: rejected initially because owner/admin governance and RBAC visibility are required.
- Separate microservice: rejected because the OpenId module is already the RBAC source of truth and uses Aspire orchestration.

## Decision: Use EF Core/Npgsql indexes and optimistic concurrency

**Rationale**: PostgreSQL is the production storage target. Unique indexes on normalized service identifier, `(service_id, permission_key)`, and full permission key enforce invariants. Status, owner, key, and updated-at indexes support search/filter workflows. Concurrency tokens protect conflicting updates listed in edge cases.

**Alternatives considered**:
- In-memory cache as source of truth: rejected because registry data must be durable and auditable.
- No concurrency token: rejected because concurrent service-owner/admin updates can conflict.
- Full-text search initially: deferred because indexed filters satisfy initial scale and success criteria.

## Decision: Align future tasks with TDD and Clean Architecture

**Rationale**: The constitution requires tests first, clean code, vertical slices, security by design, UX consistency, and performance checks. Future Phase 2 tasks must begin with failing tests for domain invariants, use-case authorization/validation, repository persistence, OpenAPI contract behavior, and API integration flows.

**Alternatives considered**:
- Implement API first and test later: rejected by non-negotiable TDD.
- Add new CQRS/mediator dependencies: rejected because existing use-case patterns are sufficient.
- Create new projects: rejected because existing OpenIdentityStack layer projects already express the desired architecture.
