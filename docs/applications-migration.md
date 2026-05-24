# Applications migration

OpenIdentityStack now uses **Applications** as the administrator-facing model for OAuth/OIDC software registrations. Existing Clients and Service Accounts are migrated into Applications while keeping protocol terms such as `client_id` where OAuth requires them.

## Preflight checks

Run migration preflight before applying production database changes. Treat any blocking issue as a deployment stop:

- duplicate `client_id` values across legacy `Clients` and `ServiceAccounts`
- service accounts with grants other than `client_credentials`
- ambiguous legacy client profiles that require operator review

Fix duplicates or invalid grants before retrying. The preflight step is read-only and must complete before any backfill writes Application rows.

## Backfill behavior

Legacy `Clients` become Applications with new internal Application IDs and preserved `client_id`, redirect URIs, scopes, grants, PKCE, consent, and inferred application type. Public authorization-code clients and unsupported legacy grant combinations are marked for migration review.

Legacy `ServiceAccounts` with only `client_credentials` grants become machine-to-machine Applications with new internal Application IDs. Their client secrets and certificates are copied to Application credentials. Revoked legacy credentials stay revoked after migration.

Role permissions are remapped from `clients:*` and `service-accounts:*` to `applications:*` equivalents.

## Removed legacy endpoints

The legacy Admin API endpoints are removed in this pre-1.0 breaking change:

| Removed endpoint | Replacement |
|------------------|-------------|
| `/api/admin/clients` | `/api/admin/applications` |
| `/api/admin/service-accounts` | `/api/admin/applications?type=machine-to-machine` |

Calls to the removed endpoints return `404 Not Found`. No deprecation or replacement headers are emitted.

## Rollback

Roll back by restoring the previous application version and database backup. There is no compatibility flag for the removed Clients or Service Accounts Admin API routes.

Remove legacy tables after verifying Applications and the OpenIddict projection are authoritative.
