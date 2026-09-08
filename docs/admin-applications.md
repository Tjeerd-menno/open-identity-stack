# Admin applications

Administrators manage OAuth/OIDC software registrations as **Applications**.

## Terminology

| Term | Meaning |
|------|---------|
| Application | An administrator-managed software registration that can sign users in or request tokens. |
| Machine-to-machine application | An Application that uses the `client_credentials` grant for non-interactive workloads. |
| Client ID | The OAuth `client_id` protocol identifier for an Application. Keep it stable for integrations. |
| Service Account | Removed product wording. Existing service-account data migrates to machine-to-machine Applications where supported. |

Use "Application" in user-facing instructions. Use `client_id` only when describing OAuth protocol fields, token requests, or integration payloads.

## Application profile choices

The Applications create form is policy-driven. The API is authoritative, and Management Web uses `/api/admin/applications/policies/profiles` to show only sensible choices for each application profile.

| Application profile | Fixed client profile | Default grants | Administrator experience |
|------|---------|---------|---------|
| Web | Confidential | `authorization_code` | Redirect URIs, post-logout redirect URIs, PKCE, consent, and optional refresh tokens stay visible. Credentials are managed after creation. |
| Single Page | Public | `authorization_code` | PKCE is always on. Shared-secret and certificate management stays hidden. Redirect and browser-origin guidance stays visible. |
| Native | Public | `authorization_code` | PKCE is always on. Shared-secret and certificate management stays hidden. The form highlights claimed HTTPS, private-scheme, and loopback redirect guidance. |
| Machine-to-machine | Confidential | `client_credentials` | Redirects, post-logout redirects, PKCE, and consent stay hidden. The form railroads to non-interactive token use and points administrators to credentials management. |
| Device | Public | `urn:ietf:params:oauth:grant-type:device_code` | Reserved. The type is shown as unavailable until the device authorization flow is implemented and tested. |

Advanced protocol capabilities such as `private_key_jwt`, mTLS, JWKS, DPoP, and token lifetime overrides remain metadata-only in this release. Do not document them as working administrator options yet.

## Common administrator flows

### Resource access

The **Resource access** tab separates three identities: the OAuth client (`client_id`), the protected API (an immutable HTTPS or URN audience and resource scope), and registered permission namespaces. Namespaces are mapped explicitly; a client identifier never determines the namespace of permissions in its token.

Create the permission namespace in the permission registry, then add a protected resource and select its namespaces. Add the resource scope to each client’s OAuth scopes and configure that client’s **delegated permission ceiling** and **application permissions** separately. Each field accepts concrete registered permissions or terminal resource wildcards such as `orders:invoice:*`. Empty lists grant no permissions. A missing resource grant rejects resource access entirely.

Delegated access tokens contain the user’s current effective permissions intersected with the client ceiling and requested resource namespaces. Machine tokens contain only explicit application permissions. Neither role names nor the generic `api` scope confer access. Role names may be included in an ID token requested with `roles`; they are not emitted as access-token authority.

Clients request the configured resource scope. An optional RFC 8707 `resource` parameter must exactly match the audiences implied by all requested resource scopes. Unknown, disabled, inconsistent, or mixed administrative/business resources are rejected. Code redemption and refresh recompute current permissions and cap them by the original token permissions and audiences. Introspection also uses current mappings/grants, requires a resource grant for the caller, and never widens the token’s original authority.

Audience and resource scope are immutable; create a new resource to change them. Edits require `applications:write`, include the observed `expectedRevision`, and produce resource/grant audit records. Stale writes return HTTP 409 and must be reloaded. The reserved Admin resource (`urn:openidentitystack:admin-api`, scope `ois.admin`, namespace `openidentitystack`) is read-only here and requires the dedicated administrative approval workflow.

The resource API contract is recorded in `contracts/openapi/identity-resource-access.yaml`. See [the migration procedure](applications-migration.md#resource-access-boundary-migration) before deploying this breaking change.

1. Create an Application with the appropriate application profile.
2. Accept the fixed client profile and default grants applied by the selected type.
3. Configure only the options the form keeps visible for that type, such as redirects, scopes, PKCE, consent, or optional refresh tokens.
4. Add credentials only for confidential Applications.
5. Copy client secrets once when they are displayed.
6. Disable or delete Applications that are no longer used.

## Machine-to-machine applications

Machine-to-machine Applications replace Service Accounts. They are confidential Applications with the `client_credentials` grant and no redirect URIs. Manage their client secrets and certificates from the Application detail credentials area.

## Public application credentials

Single Page and Native Applications are public clients. Management Web hides shared-secret and certificate actions for those profiles, and the API rejects credential combinations that require confidential client behavior.

## Screenshot checklist

Capture updated Management Web screenshots whenever the Applications UI changes:

- Applications list with type/status filters visible
- Create Application form for web and machine-to-machine profiles
- Application detail overview tab
- Credentials tab with secret and certificate actions
- One-time secret display after adding or rotating a secret
- 404 page for removed legacy `/clients` and `/service-accounts` routes, if documenting breaking changes

Do not include real secrets, production client IDs, or customer data in screenshots.
