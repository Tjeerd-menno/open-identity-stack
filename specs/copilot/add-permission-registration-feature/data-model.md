# Data Model: Service/API Permission Registry

## Overview

The registry introduces first-class records for services/APIs and the permissions they expose. Stable identifiers are immutable RBAC/audit keys. Metadata and lifecycle status change through authorized use cases. Hard deletion is avoided when permissions have dependencies or audit history.

## Entity: RegisteredService

Represents a service, API, or product component that exposes permissions.

### Fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `RegisteredServiceId` (`Guid`) | Yes | Strongly typed ID. |
| `ServiceIdentifier` | `string` | Yes | Stable external identifier, lowercase-normalized and immutable. |
| `DisplayName` | `string` | Yes | Human-readable service/API name. |
| `Description` | `string` | No | Administrative description. |
| `OwnerId` | `string` | Yes | Accountable owner or owning group. |
| `OwnerType` | enum | Yes | `user`, `group`, or `externalGroup`. |
| `Status` | `ServiceLifecycleStatus` | Yes | `active`, `disabled`, `retired`. |
| `CreatedAt` / `CreatedBy` | timestamp/string | Yes | Audit metadata. |
| `UpdatedAt` / `UpdatedBy` | timestamp/string | Yes | Audit metadata. |
| `DisabledAt` / `RetiredAt` | timestamp? | No | Lifecycle timestamps. |
| `ConcurrencyToken` | row version/token | Yes | Rejects conflicting updates. |

### Relationships

- Owns many `ServicePermission` records.
- Owns many `DelegatedMaintainer` records.
- Has many `ServicePermissionAuditEvent` records.
- Has runtime `RoleAssignmentDependency` query results through existing role data.

### Validation Rules

- `ServiceIdentifier` is required, unique, lowercase-normalized, and immutable.
- Identifier format: `^[a-z][a-z0-9-]{2,62}$`.
- The service identifier minimum length is 3 characters because it represents an organization-wide namespace and must be harder to confuse with short platform or team abbreviations.
- Identifier must not equal or impersonate reserved namespaces: `users`, `roles`, `groups`, `service-accounts`, `sessions`, `providers`, `clients`, `audit-logs`, `system`, wildcard `*`, or configured reserved prefixes.
- `DisplayName` is required and limited to 120 characters.
- `Description` is limited to 1,000 characters.
- `OwnerId` is required before any permission becomes assignable.
- Initial registration requires at least one valid permission.
- Registration/update is atomic: no partial service or permission records are saved on validation failure.

### State Transitions

```text
active -> disabled -> active
active -> retired
disabled -> retired
retired -> active        # administrator restoration only
retired -> disabled      # not allowed
```

Disabled or retired services are not offered for new RBAC assignments; existing assignments remain visible with status indicators.

## Entity: ServicePermission

Represents a permission exposed by a registered service.

### Fields

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `ServicePermissionId` (`Guid`) | Yes | Strongly typed ID. |
| `RegisteredServiceId` | `RegisteredServiceId` | Yes | Parent service. |
| `PermissionKey` | `string` | Yes | Stable key unique within service. |
| `FullPermissionKey` | `string` | Yes | Canonical RBAC key, e.g. `{serviceIdentifier}:{permissionKey}`. |
| `DisplayName` | `string` | Yes | Human-readable label. |
| `Description` | `string` | No | Admin-facing detail. |
| `IntendedUse` | `string` | No | Guidance for administrators and service owners. |
| `DocumentationUrl` | `Uri?` | No | Optional HTTPS reference. |
| `Status` | `PermissionLifecycleStatus` | Yes | `active`, `deprecated`, `disabled`, `retired`. |
| `IsAssignable` | `bool` | Yes | Derived from service status, permission status, and policy. |
| `CreatedAt` / `CreatedBy` | timestamp/string | Yes | Audit metadata. |
| `UpdatedAt` / `UpdatedBy` | timestamp/string | Yes | Audit metadata. |
| `DeprecatedAt` / `DisabledAt` / `RetiredAt` | timestamp? | No | Lifecycle timestamps. |
| `ConcurrencyToken` | row version/token | Yes | Rejects conflicting updates. |

### Relationships

- Belongs to one `RegisteredService`.
- Has many audit events.
- Has runtime dependency references to roles and administrative assignments storing matching permission strings.

### Validation Rules

- `PermissionKey` is required, lowercase-normalized, unique within the service, and immutable after creation if it has ever been assigned, audited, or exposed.
- Permission key format: `^[a-z][a-z0-9-]{1,62}$`.
- The permission key minimum length is 2 characters because it is scoped by its parent service identifier and may legitimately use short action names such as `qa` or `go`; the canonical `FullPermissionKey` remains globally descriptive.
- Duplicate permission keys in a registration request reject the entire request.
- `FullPermissionKey` must not collide with reserved platform permissions or wildcard keys.
- `DisplayName` is required and limited to 120 characters.
- `Description` and `IntendedUse` are each limited to 1,000 characters.
- `DocumentationUrl`, when supplied, must be HTTPS unless local-development policy explicitly allows otherwise.
- Metadata updates may change display name, description, intended use, documentation URL, and status, but not stable keys.

### State Transitions

```text
active -> deprecated -> active
active -> disabled -> active
active -> retired
deprecated -> disabled
deprecated -> retired
disabled -> active      # when restoration policy permits
disabled -> retired
retired -> active       # administrator restoration only
retired -> deprecated   # not allowed
```

- `active`: available for new role assignments.
- `deprecated`: retained and visible; new assignments are blocked by default or require explicit override.
- `disabled`: unavailable for new assignments and highlighted in role/access review views.
- `retired`: historical visibility only.
- Hard deletion is blocked when dependencies or audit requirements exist.

## Entity: DelegatedMaintainer

Represents a user or group allowed to manage a service's permission declarations.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `DelegatedMaintainerId` (`Guid`) | Yes | Strongly typed ID. |
| `RegisteredServiceId` | `RegisteredServiceId` | Yes | Parent service. |
| `PrincipalId` | `string` | Yes | Maintainer user/group ID. |
| `PrincipalType` | enum | Yes | `user`, `group`, `externalGroup`. |
| `GrantedAt` / `GrantedBy` | timestamp/string | Yes | Audit metadata. |

Validation: principal ID is required and unique per service. Maintainers may update metadata and permissions but may not transfer ownership or perform emergency lifecycle changes unless policy grants that ability.

## Entity: RoleAssignmentDependency

Read model showing where a permission is used in existing authorization policy records.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `PermissionKey` | `string` | Yes | Full permission key. |
| `DependencyType` | enum | Yes | `role`, `administrativeAssignment`, `accessReview`, `tokenPolicy`. |
| `DependentId` | `Guid` or `string` | Yes | Existing object ID. |
| `DependentName` | `string` | Yes | Display name for administrators. |
| `IsActive` | `bool` | Yes | Whether the dependent record is active. |
| `Impact` | enum | Yes | `blocksDeletion`, `blocksRetirement`, `warningOnly`. |

Query rules: include active and inactive roles when required for historical visibility; surface wildcard role permissions as related dependencies when wildcard semantics cover the permission.

## Entity: ServicePermissionAuditEvent

Records accepted, denied, validation-failed, and conflict outcomes.

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `AuditEventId` (`Guid`) | Yes | Strongly typed ID or existing audit ID. |
| `RegisteredServiceId` | `RegisteredServiceId?` | No | Null for failed attempts before creation. |
| `ServicePermissionId` | `ServicePermissionId?` | No | Null for service-level events. |
| `ActorId` | `string` | Yes | Authenticated principal. |
| `Action` | `string`/enum | Yes | Examples: `service.registered`, `permission.created`, `status.changed`, `ownership.transferred`, `update.denied`, `validation.failed`. |
| `Result` | enum | Yes | `accepted`, `denied`, `validationFailed`, `conflict`. |
| `OccurredAt` | `DateTimeOffset` | Yes | UTC timestamp. |
| `Before` / `After` | JSON object | No | Safe changed values. |
| `ReasonCode` / `Reason` | string? | No | Safe outcome details. |
| `CorrelationId` | string? | No | Request correlation ID. |

Audit records must not contain secrets, raw tokens, or unnecessary sensitive personal data.

## Persistence and Indexing Notes

- Unique index: `RegisteredServices.ServiceIdentifier`.
- Unique index: `ServicePermissions(RegisteredServiceId, PermissionKey)`.
- Unique index: `ServicePermissions.FullPermissionKey`.
- Index owner, service status, permission status, full permission key, and updated timestamp.
- Use optimistic concurrency tokens on service and permission records.
- Store enum values as strings for readability and migration safety.
- Keep OpenIddict tables unchanged; integrate through use cases and RBAC validation.

## RBAC Integration Rules

- Role creation/editing must query the registry catalog for service-exposed permissions instead of hard-coded service definitions.
- Existing platform/admin permissions in `OpenIdentityStack.Application.Authorization.Permissions` remain available unless separately migrated.
- New service-exposed permissions must be registered before assignment to roles.
- Deprecated, disabled, retired, or disabled-service permissions remain visible for existing assignments and access reviews.
- Removing a permission from a role does not delete registry history.
