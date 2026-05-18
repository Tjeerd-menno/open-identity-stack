# Standards and flows

OpenIdentityStack is built around OpenID Connect and OAuth 2.0 patterns that application teams already expect.

## Supported standards surface

- OpenID Connect discovery
- JSON Web Key Set publication
- OAuth 2.0 authorization endpoint
- OAuth 2.0 token endpoint
- OAuth 2.0 token revocation
- OAuth 2.0 token introspection
- OpenID Connect UserInfo
- OpenID Connect end-session flow

## Supported flow types

- authorization code
- PKCE for public and browser-based clients
- client credentials
- refresh token

## Operational notes

- interactive browser applications should prefer authorization code with PKCE
- service-to-service workloads should prefer client credentials
- token consumers should validate against the discovery document and JWKS published by the authority

## Related guides

- [Web app OIDC integration](../integrations/web-app-oidc.md)
- [API protection](../integrations/api-protection.md)
- [Machine-to-machine access](../integrations/machine-to-machine.md)
