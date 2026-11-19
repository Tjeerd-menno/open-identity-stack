# Endpoint reference

This page lists the public protocol endpoints exposed by the current OpenIdentityStack authority.

## OIDC and OAuth endpoints

| Endpoint | Purpose |
| --- | --- |
| `/.well-known/openid-configuration` | OpenID Connect discovery document |
| `/.well-known/jwks` | JSON Web Key Set used by clients and APIs to validate tokens |
| `/connect/authorize` | Interactive authorization endpoint |
| `/connect/token` | Token endpoint for authorization code, refresh token, and client credentials exchanges |
| `/connect/userinfo` | UserInfo endpoint |
| `/connect/introspect` | Token introspection endpoint |
| `/connect/revoke` | Token revocation endpoint |
| `/connect/logout` | End-session endpoint |

## Health endpoints

| Endpoint | Purpose |
| --- | --- |
| `/health` | Readiness check |
| `/alive` | Liveness check |

## Admin API surface

Management Web and trusted administrative integrations use these resource areas.

- `/api/admin/applications`
- `/api/admin/application-permissions`
- `/api/admin/permissions`
- `/api/admin/users`
- `/api/admin/roles`
- `/api/admin/groups`
- `/api/admin/sessions`
- `/api/admin/providers`
- `/api/admin/authentication-settings`
- `/api/admin/audit-entries`

Use Applications for web clients and machine-to-machine registrations. The 1.x compatibility contract identifies the supported operations and their schemas. [Applications migration](../applications-migration.md) · [Compatibility across 1.x](compatibility.md)

## Root endpoint

`/`

Returns a simple API availability message and is useful only as a quick smoke check.
