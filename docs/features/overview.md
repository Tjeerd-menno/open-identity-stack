# Features overview

OpenIdentityStack gives platform teams a self-hosted identity service with administrator tooling and application-facing standards support.

## Identity and login

- OpenID Connect and OAuth 2.0 authority built on OpenIddict
- authorization code with PKCE for interactive applications
- client credentials for service-to-service workloads
- refresh token support for longer-lived signed-in sessions
- local user accounts and external identity provider federation

## Administration

- user lifecycle management
- role-based access control
- group membership and delegated permission boundaries
- client registration and redirect URI management
- service account creation, secret rotation, and certificate association
- session visibility and revocation

## Operations

- separate database migrator for schema and seed work
- container images for API, admin web, and migrator
- Kubernetes deployment manifests with CNPG and cert-manager examples
- Windows service packaging for API-hosted environments
- readiness and liveness endpoints for operational checks

## Security posture

- signing and encryption certificate support for production tokens
- database-backed data-protection keys
- forwarded header validation support
- rate limiting on login and token endpoints
- environment-specific CORS behavior for admin web access

## Product boundaries

OpenIdentityStack is meant to be the identity system of record and token issuer for your applications. It does not replace application-specific authorization rules, edge routing, or API gateway policy.
