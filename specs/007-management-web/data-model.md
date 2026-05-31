# Data Model: Management Web Foundation

## ManagementWebApp

- **Purpose**: Represents the new operator-facing frontend as a deployable surface.
- **Fields**:
  - `appName`
  - `baseUrl`
  - `oidcClientId`
  - `apiBaseUrl`
  - `defaultRoute`
  - `deploymentStatus`
- **Relationships**:
  - Uses the shared admin API.
  - Relies on a backend identity session for cross-UI sign-in continuity.
- **Validation rules**:
  - Must have a unique hostname per environment.
  - Must not share deployment identity with AdminWeb.
  - Must remain operable when AdminWeb is unavailable.

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

## NavigationSurface

- **Purpose**: Describes the top-level areas shown in the Management Web shell.
- **Fields**:
  - `label`
  - `route`
  - `enabled`
  - `visibility`
- **Relationships**:
  - Groups the Users slice and future placeholder domains.
- **Validation rules**:
  - Users must be visible and functional in phase 1.
  - Later domains may be present as placeholders without full workflow support.

## UsersWorkspaceState

- **Purpose**: Represents the operator's active Users workflow context.
- **Fields**:
  - `searchQuery`
  - `selectedUser`
  - `activeTab`
  - `pendingChanges`
- **Relationships**:
  - Uses data from the admin API.
- **Validation rules**:
  - Unsaved edits should not be discarded silently on recoverable failures.
  - Role assignment must remain limited to existing roles in phase 1.
