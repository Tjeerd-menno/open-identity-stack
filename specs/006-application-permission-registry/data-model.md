# Data Model: Application Permission Registry

## Overview

The registry stores resource/API applications and their complete versioned permission manifests. Dynamic permissions become RBAC strings through `fullPermissionKey = applicationIdentifier + ":" + permissionKey`. Permissions have no lifecycle; they are either current or removed/tombstoned. Applications are current with `active` or `disabled` state, or deleted/tombstoned.

## Entity: RegisteredPermissionApplication

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `Guid` | Yes | Internal/admin API resource id. |
| `ApplicationIdentifier` | `string` | Yes | Immutable namespace; `^[a-z][a-z0-9-]{2,62}$`; unique among current applications. |
| `DisplayName` | `string` | Yes | Manifest-managed display label. |
| `Description` | `string?` | No | Manifest-managed description. |
| `ManifestVersion` | `string` | Yes | Last accepted SemVer 2.0.0 version without build metadata. |
| `SchemaVersion` | `string` | Yes | Initially only `1.0.0`. |
| `Status` | `ApplicationPermissionApplicationStatus` | Yes | `active` or `disabled`. |
| `OwnerId` | `string` | Yes | IAM-managed owner principal id. |
| `OwnerType` | `PrincipalType` | Yes | `user` or `group`. |
| `ManifestBaseUrl` | `Uri?` | No | Trusted origin plus optional base path; no query/fragment. |
| `CreatedAt` / `CreatedBy` | timestamp/string | Yes | Audit metadata. |
| `UpdatedAt` / `UpdatedBy` | timestamp/string | Yes | Audit metadata. |
| `DisabledAt` / `DisabledBy` / `DisableReason` | timestamp/string/string | No | Admin-managed availability state. |
| `DeletedAt` / `DeletedBy` / `DeleteReason` | timestamp/string/string | No | Tombstone metadata; deleted applications excluded by default. |
| `PreviousApplicationId` | `Guid?` | No | Historical linkage when re-registering a deleted identifier. |
| `ConcurrencyToken` | token | Yes | Required for updates/deletes/state changes. |

### Rules

- `ApplicationIdentifier` is immutable and must not collide with platform permission namespaces or OAuth client ids.
- Duplicate display names are allowed.
- `Status` has no `retired`; deletion is a tombstone.
- Disabled applications remain visible in list/detail and can accept non-destructive manifest updates, but permissions are not assignable for new grants.
- Deleted applications are shown only through explicit history/include-deleted flows.
- Re-registration of a deleted identifier requires admin rights, `acknowledgeRedeclare=true`, historical linkage, and a strictly newer manifest version than the last accepted version for that identifier.

## Entity: ApplicationPermission

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `Guid` | Yes | Internal/admin API resource id. |
| `ApplicationId` | `Guid` | Yes | Parent registered permission application. |
| `PermissionKey` | `string` | Yes | Immutable local key `resourceOrAggregate:action`. |
| `ResourceOrAggregate` | `string` | Yes | First local key segment. |
| `Action` | `string` | Yes | Second local key segment. |
| `FullPermissionKey` | `string` | Yes | Canonical RBAC/audit string. |
| `DisplayName` | `string` | Yes | Manifest-managed display label. |
| `Description` | `string?` | No | Manifest-managed description. |
| `Category` | `string?` | No | UI grouping metadata only. |
| `CreatedAt` / `CreatedBy` | timestamp/string | Yes | Audit metadata. |
| `UpdatedAt` / `UpdatedBy` | timestamp/string | Yes | Audit metadata. |
| `RemovedAt` / `RemovedBy` / `RemoveReason` | timestamp/string/string | No | Tombstone metadata. |
| `PreviousPermissionId` | `Guid?` | No | Historical linkage when re-declared. |
| `ReplacementFullPermissionKey` | `string?` | No | Admin-only tombstone annotation. |
| `ReplacementNote` | `string?` | No | Admin-only tombstone annotation. |
| `ConcurrencyToken` | token | Yes | Required for updates/removal/guidance. |

### Rules

- Current local permission keys are unique within an application.
- Full permission keys are globally unique by construction.
- Permissions have no active/deprecated/disabled/retired lifecycle.
- Removal creates a tombstone and excludes the permission from current catalogs.
- Re-declaration creates a new current record with linkage; it does not reuse the old `Guid`.
- Replacement guidance is admin-only and must point to a current existing platform or dynamic permission.

## Derived Entity: AggregateWildcardPermission

Derived, not persisted as a permission row.

| Field | Type | Notes |
|-------|------|-------|
| `FullPermissionKey` | `string` | `applicationIdentifier:resourceOrAggregate:*`. |
| `Kind` | `string` | `wildcard`. |
| `ApplicationIdentifier` | `string` | Parent namespace. |
| `ResourceOrAggregate` | `string` | Covered aggregate. |
| `CoveredPermissionCount` | integer | Count of current concrete permissions covered. |
| `Assignable` | boolean | False when application is disabled. |

### Rules

- Exists only when at least one current concrete permission exists for the aggregate.
- Removed when the last current concrete permission in the aggregate is removed.
- Assignment requires broad-grant acknowledgement.
- Tokens and introspection expand it to concrete permissions and never emit the wildcard string.

## Entity: DelegatedMaintainer

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `Id` | `Guid` | Yes | Internal id. |
| `ApplicationId` | `Guid` | Yes | Parent application. |
| `PrincipalId` | `string` | Yes | User or group principal id. |
| `PrincipalType` | `PrincipalType` | Yes | `user` or `group`. |
| `GrantedBy` | `string` | Yes | Actor id. |
| `GrantedAt` | timestamp | Yes | Audit metadata. |
| `GrantReason` | `string` | Yes | Required reason. |

### Rules

- Current owners and admins can add/remove maintainers.
- Maintainers cannot manage maintainers unless admin.
- Group maintainer authority is resolved from current group membership at request time.

## Entity: PermissionAssignmentImpact

Used by previews, destructive results, dependency reads, and audit payloads.

| Field | Type | Notes |
|-------|------|-------|
| `AssignmentStore` | `string` | Initially `role`; abstraction allows future stores. |
| `AssignmentId` | `Guid` | Role id initially. |
| `AssignmentDisplayName` | `string` | Role name initially. |
| `Permission` | `string` | Assigned permission string. |
| `ImpactKind` | `string` | `exactRemoved`, `wildcardRemoved`, `wildcardImpacted`. |

### Rules

- Assignment changes are executed through Application-layer `IPermissionAssignmentStore`.
- Initial implementation covers roles explicitly.
- Permission/application tombstones, assignment mutations, manifest version updates, and audit writes commit atomically.

## Entity: PermissionManifest

```json
{
  "schemaVersion": "1.0.0",
  "application": {
    "id": "orders-api",
    "displayName": "Orders API",
    "description": "Order management resource API",
    "version": "1.2.0"
  },
  "permissions": [
    {
      "key": "order:cancel",
      "displayName": "Cancel order",
      "description": "Allows cancelling orders",
      "category": "Orders"
    }
  ]
}
```

### Rules

- Manifest is complete. Omission in a newer accepted manifest means removal once destructive slice is implemented.
- `schemaVersion` and `application.version` are distinct.
- `application.id` is a resource/API permission namespace, not an OAuth client id.
- Ownership, maintainers, trust settings, and active/disabled state are not manifest-managed.
- Permission `category` is optional UI metadata; default grouping is `resourceOrAggregate`.

## Indexes And Constraints

- Unique current `ApplicationIdentifier`.
- Historical/tombstone lookup by `ApplicationIdentifier`.
- Unique current `(ApplicationId, PermissionKey)`.
- Historical/tombstone lookup by `(ApplicationIdentifier, PermissionKey)` or `FullPermissionKey`.
- Index `FullPermissionKey` for assignment validation and dependency reads.
- Index `ManifestBaseUrl` for current applications.
- Index owner principal and status for application list filtering.
- Row-version/concurrency tokens on applications, permissions, and tombstone annotations.

