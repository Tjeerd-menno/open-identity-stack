# Data Model: Management Web Management Web Parity

## ManagementWebApp

- **Purpose**: Represents the Mantine-based operator frontend as a deployable surface.
- **Fields**:
  - `appName`
  - `baseUrl`
  - `oidcClientId`
  - `apiBaseUrl`
  - `defaultRoute`
  - `deploymentStatus`
- **Relationships**:
  - Uses the shared admin API.
  - Relies on an identity-provider session for cross-UI sign-in continuity.
- **Validation rules**:
  - Must have a unique hostname per environment.
  - Must not share deployment identity with Management Web.
  - Must remain operable when Management Web is unavailable.

## SharedFoundation

- **Purpose**: Common ManagementWeb infrastructure used by every parity slice.
- **Fields**:
  - `apiClient`
  - `authProvider`
  - `permissionMatrix`
  - `routeGuards`
  - `tablePrimitives`
  - `dialogPrimitives`
  - `formPrimitives`
  - `errorDisplay`
  - `secretDisplay`
  - `themeProvider`
- **Relationships**:
  - Used by all ManagementWeb features.
  - Mirrors Management Web behavior contracts while using Mantine UI components.
- **Validation rules**:
  - Backend authorization is authoritative.
  - Frontend permission checks must support exact permissions, `*`, and resource wildcards.
  - 401 responses must trigger session exit or logout handling.
  - Validation and authorization errors must be visible near the initiating action where possible.

## NavigationSurface

- **Purpose**: Describes top-level ManagementWeb areas.
- **Fields**:
  - `label`
  - `route`
  - `enabled`
  - `requiredPermission`
  - `visibility`
- **Relationships**:
  - Points to vertical slices.
- **Validation rules**:
  - Must include Overview, Users, Roles, Groups, Applications, Permissions, Sessions, Identity providers, Settings, and Audit.
  - Must not include Clients.
  - Must not include Service Accounts.
  - Routes should match Management Web where the domain still exists.

## VerticalSlice

- **Purpose**: A domain workflow ported from Management Web into ManagementWeb.
- **Fields**:
  - `domain`
  - `routes`
  - `apiEndpoints`
  - `permissions`
  - `paritySource`
  - `e2eCoverage`
  - `completionStatus`
- **Relationships**:
  - Uses SharedFoundation.
  - Maps to one Management Web domain except Audit, which is ManagementWeb-only.
- **Validation rules**:
  - Must match Management Web behavior before redesign.
  - Must have targeted tests and meaningful E2E coverage before completion.

## ApplicationWorkspaceState

- **Purpose**: Represents the consolidated Applications workflow.
- **Fields**:
  - `searchQuery`
  - `profileFilter`
  - `statusFilter`
  - `clientTypeFilter`
  - `selectedApplication`
  - `selectedProfile`
  - `pendingCredentialSecret`
- **Relationships**:
  - Uses `/api/admin/applications`.
  - Uses application profile policy metadata.
- **Validation rules**:
  - Uses one Applications list with filters.
  - Does not use legacy Clients or Service Accounts endpoints.
  - One-time secrets are visible only immediately after API return.

## AuditEntry

- **Purpose**: Read-only audit trail record shown in ManagementWeb.
- **Fields**:
  - `id`
  - `timestamp`
  - `userId`
  - `action`
  - `entityType`
  - `entityId`
  - `details`
  - `beforeState`
  - `afterState`
- **Relationships**:
  - Backed by existing persisted audit log entries.
  - Listed through `GET /api/admin/audit-entries`.
- **Validation rules**:
  - Read access requires `audit-logs:read`.
  - List responses include `details`, `beforeState`, and `afterState` in v1.
  - Filtering supports date range, user, action, entity, and search text.

## ThemePreference

- **Purpose**: Stores the operator's appearance choice.
- **Fields**:
  - `mode` (`light`, `dark`, or `system`)
  - `source` (`system-default` or `saved-preference`)
  - `updatedAt`
- **Relationships**:
  - Belongs to the current operator/browser profile.
- **Validation rules**:
  - If no saved value exists, the UI uses system appearance.
  - Saved preference overrides the system choice on later visits.

