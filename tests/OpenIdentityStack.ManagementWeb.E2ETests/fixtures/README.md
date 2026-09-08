Management Web E2E fixtures belong here. Runtime tests should prefer Aspire resource discovery over hard-coded local ports when they are converted to executable CI coverage.

The isolated PostgreSQL fixture explicitly bootstraps a local human with a persisted `*` role assignment and a public PKCE browser client with a delegated-only `ois.admin` grant. Production approval handlers remain active. Ordinary test clients receive no administrative approval by default.

`AdministrativeAccessManagementTests` exercises business resource grants, cancellation and acknowledgement of administrative approval, and withdrawal through the real browser and API. It saves review screenshots under the temporary directory's `ois-admin-boundary-e2e` folder: `resource-access.png`, `administrative-approval.png`, and `administrative-approved.png`.
