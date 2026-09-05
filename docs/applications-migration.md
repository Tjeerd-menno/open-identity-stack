# Applications migration

OpenIdentityStack now uses **Applications** as the administrator-facing model for OAuth/OIDC software registrations. Existing Clients and Service Accounts are migrated into Applications while keeping protocol terms such as `client_id` where OAuth requires them.

## Resource access boundary migration

`AddExplicitResourceAccess` creates `ProtectedResources` and `ClientResourceGrants`. Existing applications with resource-like scopes are marked `RequiresMigrationReview` with migration source `resource-access-boundary-v1`. It creates **no client grants**: historical scope lists, scope resource strings, similarly named clients/namespaces, and the former `api` shortcut are insufficient evidence of authorization.

ManagementWeb requests the dedicated `ois.admin` scope and requires an explicit delegated grant for the reserved Admin resource. The following administrative-access policy layer adds current entitlement checks at Admin API entry points; resource projection already requires this client configuration.

1. Inventory each client’s actual target APIs and approve its delegated ceiling and machine permissions separately. Record ambiguous mappings for operator review; leave them ungranted until resolved.
2. Run DbMigrator. It initializes the reserved Admin audience/scope without granting existing clients access. Establish the controlled Management Web entitlement through the administrative bootstrap/approval procedure.
3. Register each business API’s audience, resource scope, and existing permission namespaces through Applications → Resource access. Configure explicit grants for each approved client and permit the resource scope in that client’s OAuth configuration.
4. Update callers to request those scopes and APIs to validate their exact audience. Treat `client_id` as the caller identity, not an audience or permission namespace. Obtain new authorization grants; refresh cannot acquire newly added authority.
5. Verify allowed and denied token cases, audience mismatch, empty ceilings, grant reduction, and introspection using a caller explicitly assigned to the resource. Inspect `ResourceMappingChanged` and `ClientResourceGrantChanged` audit records before resuming traffic.

Deploy this with the coordinated credential/session cutover. Existing signed access tokens cannot be safely recalled at offline resource servers merely by changing these tables; the cutover must retire old artifacts and external validators must use the agreed validation policy. Keep the new boundary in place during rollback. A schema downgrade deletes mappings and grants and an older binary restores unsafe issuance behavior, so do not use an older binary as an authorization fallback.

## Legacy application preflight checks

Run migration preflight before applying production database changes. Treat any blocking issue as a deployment stop:

- duplicate `client_id` values across legacy `Clients` and `ServiceAccounts`
- service accounts with grants other than `client_credentials`
- ambiguous legacy client profiles that require operator review

Fix duplicates or invalid grants before retrying. The preflight step is read-only and must complete before any backfill writes Application rows.

## Backfill behavior

Legacy `Clients` become Applications with new internal Application IDs and preserved `client_id`, redirect URIs, scopes, grants, PKCE, consent, and inferred application profile. Public authorization-code clients and unsupported legacy grant combinations are marked for migration review.

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
