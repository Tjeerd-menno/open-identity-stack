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

## Common administrator flows

1. Create an Application with the appropriate application type.
2. Configure grants, scopes, redirect URIs, PKCE, and consent requirements.
3. Add credentials only for confidential Applications.
4. Copy client secrets once when they are displayed.
5. Disable or delete Applications that are no longer used.

## Machine-to-machine applications

Machine-to-machine Applications replace Service Accounts. They are confidential Applications with the `client_credentials` grant and no redirect URIs. Manage their client secrets and certificates from the Application detail credentials area.

## Screenshot checklist

Capture updated AdminWeb screenshots whenever the Applications UI changes:

- Applications list with type/status filters visible
- Create Application form for web and machine-to-machine profiles
- Application detail overview tab
- Credentials tab with secret and certificate actions
- One-time secret display after adding or rotating a secret
- 404 page for removed legacy `/clients` and `/service-accounts` routes, if documenting breaking changes

Do not include real secrets, production client IDs, or customer data in screenshots.
