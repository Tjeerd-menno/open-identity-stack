# Compatibility across 1.x

Beginning with product release 1.0.0, a supported integration can upgrade to a later 1.x release and continue using the requests, responses, and deployment settings it already uses. This promise also covers supported interfaces first published in a later 1.x release, starting with that release. Follow the release's upgrade instructions for database migrations, runtime prerequisites, and deployment order. [GitHub release notes](https://github.com/Tjeerd-menno/open-identity-stack/releases)

## Application clients and protected APIs

Use the issuer's discovery document to locate its endpoints and signing keys. OpenIdentityStack supports authorization code with PKCE, refresh token exchange, and client credentials for appropriately registered clients. The documented UserInfo, introspection, revocation, and end-session operations retain their protocol semantics. Clients must allow discovery metadata to grow and keys to rotate. [Standards and flows](standards-and-flows.md) · [Endpoint reference](endpoint-reference.md)

Relying parties can depend on the meaning of documented claims. For example, the issuer and subject (`iss`, `sub`) together identify the authenticated subject, `aud` identifies the intended audience, and `email_verified` reflects verification evidence when that claim is returned. Claim availability depends on the flow, granted scopes, and available user data. Clients should accept additional claims, treat access tokens as opaque when presenting them, and obtain fresh metadata and keys when validating tokens. [Web app integration](../integrations/web-app-oidc.md) · [Email verification evidence](../security/email-verification-evidence.md)

## Administrative integrations

Trusted external tooling and Management Web share the supported HTTP contract under `/api/admin`. Look up the method and path in the Admin API OpenAPI document. The guarantee covers documented request parameters and bodies, supported status codes, response properties and types, and documented authorization and error behavior. Clients should ignore unknown response properties and use documented error codes for program logic. [1.0 Admin API OpenAPI baseline](https://github.com/Tjeerd-menno/open-identity-stack/blob/main/contracts/openapi/baselines/1.0.0/admin-api.json)

For example, a client approved to list users can continue sending `GET /api/admin/users` with its existing parameters and reading the documented response. It still needs the `ois.admin` scope, the dedicated Admin API audience, and current permission to perform the operation. A grant withdrawal or a disabled account can change the same request's authorization outcome. [Administrative client access](../security/administrative-client-access.md)

## Operator configuration

Documented production settings retain their names, accepted formats, and meanings. Examples include the PostgreSQL connection string, trusted proxy settings, certificate and private key paths, and Management Web's authority and API URLs. A new optional setting preserves the existing behavior when omitted. Consult the release notes for supported platform versions, migration steps, and rotation instructions. [Configuration reference](configuration-reference.md) · [Environment variables](../configuration/environment-variables.md) · [Upgrades](../operations/upgrades.md)

## Internal interfaces

The database schema, .NET implementation types, frontend package internals, browser storage, Management Web page markup, log message text, and local Aspire development settings can change during 1.x. Use the Admin API for automation and the migrator for schema updates. Feature design documents and test fixtures record engineering details. The published product references define the supported integration surface.

## Additions, deprecations, and breaking changes

| Change | What consumers can expect |
| --- | --- |
| A minor release adds an optional request field to an existing operation | Existing clients may omit it and retain their previous behavior. |
| A minor release deprecates a setting | The old name continues to work throughout 1.x. The reference and release notes identify the replacement, migration steps, and earliest removal version. Deprecated HTTP operations or fields also carry an OpenAPI deprecation marker where supported. |
| A release removes an operation or requires a new request field | This is a breaking change. Introduce a compatible alternative during 1.x and schedule removal for a later major release, 2.0.0 at the earliest. |

Security fixes can reject credentials or requests that violated the documented trust boundary. For a security correction that affects deployments, publish the affected versions, observable change, required operator or client action, and verification steps in the release notes. Link the security advisory when one is published. A change that breaks a supported, authorized use follows the major-version rule. [Administrative trust boundaries](../adr/0005-identity-and-administrative-trust-boundaries.md)
