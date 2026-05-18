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

The admin API is organized around these resource areas:

- `/api/admin/clients`
- `/api/admin/users`
- `/api/admin/roles`
- `/api/admin/groups`
- `/api/admin/sessions`
- `/api/admin/service-accounts`
- `/api/admin/service-permissions`
- `/api/admin/providers`
- `/api/admin/settings`

These routes are intended for the admin web and other trusted administrative tooling rather than public internet clients.

## Root endpoint

`/`

Returns a simple API availability message and is useful only as a quick smoke check.
